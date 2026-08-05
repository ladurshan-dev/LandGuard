using LandGuard.Application.Common.Interfaces;
using LandGuard.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LandGuard.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Automatically stamps CreatedAt / CreatedBy / LastModifiedAt /
/// LastModifiedBy on every BaseAuditableEntity as changes are saved.
///
/// Design decision: this is implemented as an EF Core SaveChangesInterceptor
/// rather than overriding SaveChangesAsync in ApplicationDbContext, and
/// rather than trusting each service to set these fields manually. An
/// interceptor runs for every SaveChanges call regardless of which service
/// triggered it, so audit trail integrity - which matters for a fraud
/// system, where "who touched this listing and when" can itself be
/// evidence - does not depend on every future developer remembering to
/// set four fields by hand.
/// </summary>
public class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public AuditableEntitySaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = _dateTimeService.UtcNow;
                entry.Entity.CreatedBy = _currentUserService.Email;
            }

            if (entry.State is EntityState.Added or EntityState.Modified || HasChangedOwnedEntities(entry))
            {
                entry.Entity.LastModifiedAt = _dateTimeService.UtcNow;
                entry.Entity.LastModifiedBy = _currentUserService.Email;
            }
        }
    }

    /// <summary>
    /// EF Core owned entities (e.g. an Address value object embedded in
    /// Property) don't get their own ChangeTracker entry with
    /// EntityState.Modified on the owner - this walks references to catch
    /// "the owner didn't change but its owned value object did" so
    /// LastModifiedAt still reflects reality.
    /// </summary>
    private static bool HasChangedOwnedEntities(EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry is { State: EntityState.Added or EntityState.Modified } &&
            r.TargetEntry.Metadata.IsOwned());
}
