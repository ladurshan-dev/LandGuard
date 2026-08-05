using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Infrastructure.Persistence.StoredProcedures;

/// <summary>
/// Default <see cref="IStoredProcedureExecutor"/> implementation. Reuses
/// <see cref="ApplicationDbContext"/>'s own ADO.NET connection
/// (<c>Database.GetDbConnection()</c>) rather than opening a second
/// connection, so a stored-procedure call and any surrounding EF Core work
/// share the same connection pool slot and can participate in the same
/// ambient transaction if one is ever started via
/// <c>Database.BeginTransactionAsync()</c>.
/// </summary>
public class DapperStoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly ApplicationDbContext _context;

    public DapperStoredProcedureExecutor(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var command = await BuildCommandAsync(procedureName, parameters, cancellationToken);
        var connection = _context.Database.GetDbConnection();
        var results = await connection.QueryAsync<T>(command);
        return results.AsList();
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var command = await BuildCommandAsync(procedureName, parameters, cancellationToken);
        var connection = _context.Database.GetDbConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(command);
    }

    public async Task<int> ExecuteAsync(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var command = await BuildCommandAsync(procedureName, parameters, cancellationToken);
        var connection = _context.Database.GetDbConnection();
        return await connection.ExecuteAsync(command);
    }

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(
        string procedureName, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var command = await BuildCommandAsync(procedureName, parameters, cancellationToken);
        var connection = _context.Database.GetDbConnection();
        return await connection.QueryMultipleAsync(command);
    }

    private async Task<CommandDefinition> BuildCommandAsync(
        string procedureName, object? parameters, CancellationToken cancellationToken)
    {
        if (_context.Database.GetDbConnection().State != ConnectionState.Open)
        {
            await _context.Database.OpenConnectionAsync(cancellationToken);
        }

        return new CommandDefinition(
            procedureName,
            parameters,
            transaction: _context.Database.CurrentTransaction?.GetDbTransaction(),
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);
    }
}
