using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="IFraudStoredProcedures"/>,
/// following the same pattern <c>NotificationStoredProcedures</c>,
/// <c>UserStoredProcedures</c> and <c>PropertyStoredProcedures</c>
/// established: inject <see cref="IStoredProcedureExecutor"/>, pass exact
/// stored-procedure parameter names as an anonymous object, map straight
/// into a plain DTO. No business logic lives here - the fraud rules, the
/// weighted score and the risk banding are all computed inside
/// <c>usp_Fraud_AnalyseProperty</c>/<c>usp_Risk_GenerateReport</c>
/// (Module 2), never in this class.
/// </summary>
public class FraudStoredProcedures : IFraudStoredProcedures
{
    private readonly IStoredProcedureExecutor _executor;

    public FraudStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public Task<IReadOnlyList<FraudHistoryEntry>> GetHistoryAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId };

        return _executor.QueryAsync<FraudHistoryEntry>("dbo.usp_Fraud_GetHistory", parameters, cancellationToken);
    }
}
