using Dapper;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Thin Dapper wrapper around the EF Core connection. Used exclusively by
/// the per-area stored-procedure wrapper classes in this folder (starting
/// with <see cref="NotificationStoredProcedures"/>, and - module by
/// module - future PropertyStoredProcedures, FraudStoredProcedures,
/// AdminStoredProcedures, UserStoredProcedures, BuyerFeatureStoredProcedures,
/// PodcastStoredProcedures).
///
/// Deliberately <b>not</b> exposed through an Application-layer interface:
/// Application depends on I*StoredProcedures contracts that speak only in
/// plain DTOs/entities, never in Dapper types
/// (<see cref="SqlMapper.GridReader"/> etc.), so swapping Dapper for
/// something else later would only ever touch this one folder.
///
/// Why Dapper alongside EF Core at all: EF Core's <c>FromSqlRaw</c> /
/// <c>ExecuteSqlRaw</c> cannot read a stored procedure's 2nd or 3rd result
/// set (several LandGuardDB procedures, e.g. <c>usp_Property_GetById</c>,
/// return 2-3), and output parameters are awkward without dropping to raw
/// ADO.NET anyway. Dapper's <c>QueryMultipleAsync</c> and its native
/// support for output parameters make this the least amount of plumbing
/// code for the amount of stored procedures this schema has (30).
/// </summary>
public interface IStoredProcedureExecutor
{
    /// <summary>Executes a stored procedure that returns one result set, mapped to <typeparamref name="T"/>.</summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Executes a stored procedure expected to return zero or one row.</summary>
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>Executes a stored procedure with no meaningful result set (e.g. an UPDATE-only procedure), returning the affected row count.</summary>
    Task<int> ExecuteAsync(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a stored procedure that returns multiple result sets (e.g.
    /// <c>usp_Property_GetById</c>: listing, images, fraud report).
    /// Caller is responsible for reading each result set in order via the
    /// returned <see cref="SqlMapper.GridReader"/> and disposing it.
    /// </summary>
    Task<SqlMapper.GridReader> QueryMultipleAsync(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default);
}
