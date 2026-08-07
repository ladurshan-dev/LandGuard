using System.Data;
using Dapper;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Application.Common.Models;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Infrastructure implementation of <see cref="IDocumentComparisonStoredProcedures"/>,
/// following the same pattern <c>FraudStoredProcedures</c>/
/// <c>PropertyStoredProcedures</c> established: inject
/// <see cref="IStoredProcedureExecutor"/>, pass exact stored-procedure
/// parameter names, map straight into plain DTOs. No comparison logic
/// lives here - that is entirely <c>FieldComparer</c>/
/// <c>DocumentComparisonService</c> (Application layer); this class only
/// moves already-computed rows in and out of
/// <c>dbo.DocumentComparison</c>/<c>dbo.DocumentComparisonField</c>.
///
/// <see cref="SaveAsync"/> passes every field row to
/// <c>usp_DocumentComparison_Save</c>'s
/// <c>@Fields dbo.DocumentComparisonFieldType READONLY</c> parameter as a
/// single table-valued parameter, rather than one INSERT per field - the
/// reason that table type exists (see
/// <c>database/Module5C_DocumentComparison.sql</c>). Dapper's
/// <c>AsTableValuedParameter</c> only accepts a <see cref="DataTable"/> (or
/// <c>IEnumerable&lt;Microsoft.SqlServer.Server.SqlDataRecord&gt;</c>) - not
/// an arbitrary <c>IEnumerable&lt;T&gt;</c> of a plain POCO, since Dapper
/// has no reflection-based row mapper for TVPs the way it does for normal
/// result sets - so <see cref="BuildFieldsTable"/> converts the row list
/// into a <see cref="DataTable"/> whose columns match
/// <c>dbo.DocumentComparisonFieldType</c> exactly (name, order and type)
/// before it is handed to Dapper.
/// </summary>
public class DocumentComparisonStoredProcedures : IDocumentComparisonStoredProcedures
{
    private const string FieldTableTypeName = "dbo.DocumentComparisonFieldType";

    private readonly IStoredProcedureExecutor _executor;

    public DocumentComparisonStoredProcedures(IStoredProcedureExecutor executor)
    {
        _executor = executor;
    }

    public async Task<DocumentComparisonRecord> SaveAsync(
        int propertyId,
        int comparedByUserId,
        string? documentReference,
        decimal overallMatchPercentage,
        string? summary,
        IReadOnlyList<DocumentComparisonFieldRow> fields,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@PropertyID", propertyId);
        parameters.Add("@ComparedByUserID", comparedByUserId);
        parameters.Add("@DocumentReference", documentReference);
        parameters.Add("@OverallMatchPercentage", overallMatchPercentage);
        parameters.Add("@Summary", summary);
        parameters.Add("@Fields", BuildFieldsTable(fields).AsTableValuedParameter(FieldTableTypeName));
        // Declared for parity with usp_DocumentComparison_Save's OUTPUT
        // parameter (same reason usp_User_Register/usp_Property_Create
        // needed DynamicParameters) - not read back here, since the
        // procedure's own final SELECT returns the exact row it just
        // inserted (WHERE ComparisonID = @NewComparisonID), the same
        // reasoning UserStoredProcedures.RegisterAsync documents.
        parameters.Add("@NewComparisonID", dbType: DbType.Int32, direction: ParameterDirection.Output);

        using var reader = await _executor.QueryMultipleAsync("dbo.usp_DocumentComparison_Save", parameters, cancellationToken);

        var header = await reader.ReadSingleAsync<DocumentComparisonHeader>();
        var fieldRows = (await reader.ReadAsync<DocumentComparisonFieldRow>()).ToList();

        return new DocumentComparisonRecord { Header = header, Fields = fieldRows };
    }

    public async Task<DocumentComparisonRecord?> GetLatestAsync(int propertyId, CancellationToken cancellationToken = default)
    {
        var parameters = new { PropertyID = propertyId };

        using var reader = await _executor.QueryMultipleAsync("dbo.usp_DocumentComparison_GetLatest", parameters, cancellationToken);

        var header = await reader.ReadSingleOrDefaultAsync<DocumentComparisonHeader>();
        if (header is null)
        {
            return null;
        }

        var fieldRows = (await reader.ReadAsync<DocumentComparisonFieldRow>()).ToList();

        return new DocumentComparisonRecord { Header = header, Fields = fieldRows };
    }

    /// <summary>
    /// Builds the <see cref="DataTable"/> Dapper's <c>AsTableValuedParameter</c>
    /// needs for <c>@Fields</c>. Column names/order/types must match
    /// <c>dbo.DocumentComparisonFieldType</c> exactly - SQL Server maps a
    /// TVP by ordinal position, not by column name, so the order here is
    /// load-bearing, not just cosmetic.
    /// </summary>
    private static DataTable BuildFieldsTable(IReadOnlyList<DocumentComparisonFieldRow> fields)
    {
        var table = new DataTable();
        table.Columns.Add("FieldName", typeof(string));
        table.Columns.Add("OcrValue", typeof(string));
        table.Columns.Add("DatabaseValue", typeof(string));
        table.Columns.Add("Matched", typeof(bool));
        table.Columns.Add("SimilarityPercentage", typeof(decimal));
        table.Columns.Add("Message", typeof(string));

        foreach (var field in fields)
        {
            table.Rows.Add(
                field.FieldName,
                (object?)field.OcrValue ?? DBNull.Value,
                (object?)field.DatabaseValue ?? DBNull.Value,
                field.Matched,
                field.SimilarityPercentage,
                (object?)field.Message ?? DBNull.Value);
        }

        return table;
    }
}
