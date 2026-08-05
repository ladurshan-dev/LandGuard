using LandGuard.Application.Common.Interfaces;
using LandGuard.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace LandGuard.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic EF Core implementation of IRepository&lt;T&gt;. Entity-specific
/// repositories inherit from this to get CRUD for free and add only their
/// own specialized queries - e.g.
///
///   public interface IPropertyRepository : IRepository&lt;Property&gt;
///   {
///       Task&lt;Property?&gt; GetByDeedNumberAsync(string deedNumber, CancellationToken ct);
///   }
///
///   public class PropertyRepository : Repository&lt;Property&gt;, IPropertyRepository
///   {
///       public PropertyRepository(ApplicationDbContext context) : base(context) { }
///       public Task&lt;Property?&gt; GetByDeedNumberAsync(...) => ...;
///   }
///
/// This follows the Open/Closed Principle: a new query need is satisfied
/// by adding a method to a specific repository, never by modifying this
/// base class. GetAllAsync uses AsNoTracking() because read-only listing
/// queries (e.g. "all approved properties" for Buyer search) don't need
/// EF Core's change tracking overhead.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync(new object[] { id }, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().ToListAsync(cancellationToken);

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual void Update(T entity) => DbSet.Update(entity);

    public virtual void Remove(T entity) => DbSet.Remove(entity);
}
