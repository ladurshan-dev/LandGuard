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
using Microsoft.Extensions.Options;

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

        // Module 3 (Authentication): JwtSettings binds the "Jwt" config
        // section once here, shared by JwtTokenGenerator (signing) and
        // Program.cs's TokenValidationParameters (validation) so the two
        // never drift apart. IUserStoredProcedures follows the same
        // per-area wrapper pattern as INotificationStoredProcedures above.
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserStoredProcedures, UserStoredProcedures>();

        // Module 4 (Property Management): IPropertyStoredProcedures follows
        // the same per-area wrapper pattern as Notifications/Users above.
        // IGeocodingService is registered as a typed HttpClient
        // (AddHttpClient<TInterface, TImplementation>) rather than a plain
        // AddScoped so it gets IHttpClientFactory's pooled-handler lifetime
        // management for free instead of this class opening a raw
        // HttpClient per request. IFileStorageService/FileStorageSettings
        // are the swappable-storage seam documented on FileStorageSettings.
        services.AddScoped<IPropertyStoredProcedures, PropertyStoredProcedures>();

        services.Configure<GeocodingSettings>(configuration.GetSection("Geocoding"));
        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<GeocodingSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
        });

        services.Configure<FileStorageSettings>(configuration.GetSection("FileStorage"));
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // Module 5A (Fraud Detection Foundation): IFraudStoredProcedures
        // wraps only the one new procedure this module needed
        // (usp_Fraud_GetHistory) - analysis and reporting reuse
        // IPropertyStoredProcedures/IPropertyService, registered above,
        // rather than a duplicate wrapper here.
        services.AddScoped<IFraudStoredProcedures, FraudStoredProcedures>();

        // Module 5B (OCR Integration): OcrSettings binds Tesseract's
        // language/tessdata path; TesseractOcrService runs entirely
        // locally (no HttpClient/typed-client registration needed, unlike
        // IGeocodingService, since there is no remote endpoint here at
        // all).
        services.Configure<OcrSettings>(configuration.GetSection("Ocr"));
        services.AddScoped<IOcrService, TesseractOcrService>();

        return services;
    }
}
