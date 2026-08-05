using LandGuard.Domain.Common;

namespace LandGuard.Application.Common.Interfaces;

/// <summary>
/// Generic Repository Pattern abstraction. Every future entity-specific
/// repository (IPropertyRepository, IUserRepository,
/// ILandRegistryRepository, IFraudReportRepository, ...) extends this
/// with its own domain-specific query methods (e.g.
/// "GetByDeedNumberAsync", "GetPendingReviewAsync") instead of
/// duplicating basic CRUD.
///
/// Why a repository at all, given EF Core's DbSet already looks
/// repository-shaped: services in the Application layer must not
/// reference EF Core types (IQueryable&lt;T&gt; leaking through public
/// service signatures would let LINQ-to-SQL-specific query shapes leak
/// into business logic, and would make unit-testing services require a
/// real or in-memory DbContext). The repository interface is the seam
/// that keeps persistence swappable and services independently testable.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}
