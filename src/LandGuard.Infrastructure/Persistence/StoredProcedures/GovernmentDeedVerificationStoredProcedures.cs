using System.Data;
using Dapper;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;
using LandGuard.Application.DTOs.DeedComparison;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of
/// <see cref="IGovernmentDeedVerificationStoredProcedures"/> (Government
/// Registry module, Phase 5B). Follows the same per-area wrapper pattern
/// <c>PropertyStoredProcedures</c>/<c>FraudStoredProcedures</c> establish
/// (inject <see cref="IStoredProcedureExecutor"/>, pass exact
/// stored-procedure parameter names, map straight into plain DTOs), with
/// one addition this class needs and no other Dapper wrapper in this
/// solution has needed before: <see cref="PersistAsync"/> writes to three
/// tables and must be atomic, so this class also injects
/// <see cref="ApplicationDbContext"/> directly (a concrete Infrastructure
/// type - Infrastructure is allowed to depend on it; only Application must
/// not) purely to call <c>Database.BeginTransactionAsync</c>/<c>CommitAsync</c>/
/// <c>RollbackAsync</c> around the three <see cref="IStoredProcedureExecutor"/>
/// calls below.
///
/// <b>Why this works without a second connection or a new abstraction:</b>
/// <c>DapperStoredProcedureExecutor</c> already reuses
/// <c>ApplicationDbContext</c>'s own ADO.NET connection and automatically
/// attaches <c>Database.CurrentTransaction</c> to every command it builds
/// (see that class's own doc comment). Both this class and
/// <c>DapperStoredProcedureExecutor</c> are registered <c>AddScoped</c>,
/// and <c>ApplicationDbContext</c> is registered via <c>AddDbContext</c>
/// (scoped by default) - within one DI scope (one request/service call)
/// they all resolve the same <c>ApplicationDbContext</c> instance, so a
/// transaction begun here is automatically visible to every
/// <see cref="IStoredProcedureExecutor"/> call this class makes
/// afterwards, with zero change to <c>IStoredProcedureExecutor</c> itself
/// and no second connection ever opened. This is the one and only place in
/// this class - and the only place in the whole Government Registry
/// module - any EF Core transaction type is used; Application never sees
/// one (see <see cref="IGovernmentDeedVerificationStoredProcedures"/>'s own
/// doc comment for the correction this replaced).
/// </summary>
public class GovernmentDeedVerificationStoredProcedures : IGovernmentDeedVerificationStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;
    private readonly ApplicationDbContext _context;

    public GovernmentDeedVerificationStoredProcedures(IStoredProcedureExecutor executor, ApplicationDbContext context)
    {
        _executor = executor;
        _context = context;
    }

    public async Task<int> PersistAsync(
        GovernmentDeedFraudDetectionResult result, int submittedByUserId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var deedVerificationId = await CreateVerificationAsync(result, submittedByUserId, cancellationToken);

            foreach (var field in result.Evidence)
            {
                var fieldParameters = new
                {
                    DeedVerificationID = deedVerificationId,
                    FieldName = field.FieldName,
                    GovernmentValue = field.GovernmentValue,
                    SellerValue = field.SellerValue,
                    IsMatch = field.Match,
                    Message = field.Message
                };

                await _executor.ExecuteAsync("dbo.usp_DeedVerificationField_Add", fieldParameters, cancellationToken);
            }

            foreach (var reason in result.Reasons)
            {
                var reasonParameters = new { DeedVerificationID = deedVerificationId, Reason = reason.ToString() };

                await _executor.ExecuteAsync("dbo.usp_DeedVerificationReason_Add", reasonParameters, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return deedVerificationId;
        }
        catch
        {
            // Nothing partially persists - the parent DeedVerification
            // insert and every field/reason child insert above all roll
            // back together, satisfying Phase 5B's own "never leave a
            // parent record with only some evidence/reasons" requirement.
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> CreateVerificationAsync(
        GovernmentDeedFraudDetectionResult result, int submittedByUserId, CancellationToken cancellationToken)
    {
        // usp_DeedVerification_Create has an OUTPUT parameter
        // (@NewDeedVerificationID), the same reason usp_Property_Create
        // needed DynamicParameters.
        var parameters = new DynamicParameters();
        parameters.Add("@PropertyID", result.PropertyId);
        parameters.Add("@SubmittedByUserID", submittedByUserId);
        parameters.Add("@GovernmentRecordID", result.GovernmentRecordId);
        parameters.Add("@GovernmentRecordStatus", result.GovernmentRecordStatus);
        parameters.Add("@VerificationStatus", result.Status.ToString());
        parameters.Add("@Summary", result.Summary);
        // Phase D: the seller's actual storage reference, carried through
        // from GovernmentDeedComparisonReport.SellerDocumentReference via
        // GovernmentDeedFraudDetectionResult.SellerDocumentReference (see
        // both types' own doc comments). No schema/procedure change needed
        // here - usp_DeedVerification_Create has always accepted this
        // parameter; only this call site was hardcoding null.
        parameters.Add("@SellerDocumentReference", result.SellerDocumentReference);
        parameters.Add("@VerifiedDate", result.GeneratedDate);
        parameters.Add("@NewDeedVerificationID", dbType: DbType.Int32, direction: ParameterDirection.Output);

        // The procedure's own final SELECT (the freshly-created row) isn't
        // needed here - only the OUTPUT parameter is - so this uses
        // ExecuteAsync rather than QuerySingleOrDefaultAsync (Dapper still
        // populates DynamicParameters' OUTPUT value either way).
        await _executor.ExecuteAsync("dbo.usp_DeedVerification_Create", parameters, cancellationToken);

        return parameters.Get<int>("@NewDeedVerificationID");
    }

    public async Task<IReadOnlyList<DeedVerificationHistoryEntry>> GetHistoryAsync(
        int propertyId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId };

        // Read-only - no transaction needed. usp_DeedVerification_GetHistory
        // returns 3 result sets in order: parent verification runs, then
        // their field evidence, then their reasons - reading each off the
        // same GridReader in that exact order mirrors
        // PropertyStoredProcedures.GetByIdAsync's own use of
        // QueryMultipleAsync.
        using var multi = await _executor.QueryMultipleAsync("dbo.usp_DeedVerification_GetHistory", parameters, cancellationToken);

        var records = (await multi.ReadAsync<DeedVerificationRecord>()).AsList();
        var fields = (await multi.ReadAsync<DeedVerificationFieldRecord>()).AsList();
        var reasons = (await multi.ReadAsync<DeedVerificationReasonRecord>()).AsList();

        return records
            .Select(record => new DeedVerificationHistoryEntry
            {
                Record = record,
                Fields = fields.Where(f => f.DeedVerificationId == record.DeedVerificationId).ToList(),
                Reasons = reasons.Where(r => r.DeedVerificationId == record.DeedVerificationId).ToList()
            })
            .ToList();
    }
}
