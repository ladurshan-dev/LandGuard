using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Common.Interfaces.StoredProcedures;
using LandGuard.Infrastructure.Persistence;
using LandGuard.Infrastructure.Persistence.Interceptors;
using LandGuard.Infrastructure.Persistence.Repositories;
using LandGuard.Infrastructure.Persistence.StoredProcedures;
using LandGuard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LandGuard.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer: EF Core / SQL Server,
/// the audit interceptor, the generic repository, the Dapper stored-
/// procedure executor, and concrete implementations of every abstraction
/// Application declares. This is the only project in the solution
/// permitted to reference EF Core, Dapper, or HttpContext-related types
/// directly - keeping that knowledge here (and out of Application) is
/// what lets Domain/Application stay portable and unit-testable in
/// isolation.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            // Module 2: connection string key matches the uploaded
            // LandGuardDB documentation exactly ("LandGuardDB", not
            // Module 1's placeholder "DefaultConnection"). No
            // MigrationsAssembly is configured - this DbContext never
            // generates or applies migrations against LandGuardDB; the
            // Database/Scripts/*.sql files are the only thing that owns
            // schema for this database.
            options.UseSqlServer(configuration.GetConnectionString("LandGuardDB"));

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        // Application services depend on IApplicationDbContext, never on
        // ApplicationDbContext directly - this registration is the bridge.
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();

        // Module 2: Dapper-based stored-procedure execution. The executor
        // is internal Infrastructure plumbing (see IStoredProcedureExecutor's
        // doc comment); INotificationStoredProcedures is the first of the
        // per-area wrapper interfaces, added module by module as each
        // feature (Auth, Property, Fraud, Admin, Buyer features, Podcasts)
        // is built.
        services.AddScoped<IStoredProcedureExecutor, DapperStoredProcedureExecutor>();
        services.AddScoped<INotificationStoredProcedures, NotificationStoredProcedures>();

        return services;
    }
}
