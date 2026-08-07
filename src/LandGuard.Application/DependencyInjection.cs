using System.Reflection;
using FluentValidation;
using LandGuard.Application.Common.Interfaces;
using LandGuard.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LandGuard.Application;

/// <summary>
/// Composition root for the Application layer. Each layer (Domain has
/// none, Application, Infrastructure, API) exposes one
/// "Add&lt;Layer&gt;Services" extension method so that Program.cs stays a
/// short, declarative list of layer registrations instead of a growing
/// pile of individual `services.AddX()` calls that no one owns. As new
/// modules add AutoMapper profiles or FluentValidation validators, they
/// are picked up automatically by the assembly scan below - no change
/// needed here.
///
/// Service Layer classes (AuthService, and whatever PropertyService/
/// FraudDetectionService/AdminService follow) are registered explicitly
/// below, one line per service, rather than scanned - unlike validators,
/// there's no reflection convention for "which interface does this class
/// implement", and being explicit here is also the one place a reader can
/// see the full list of business services the API exposes.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IAuthService, AuthService>();

        // Module 4 (Property Management).
        services.AddScoped<IPropertyService, PropertyService>();

        // Module 5A (Fraud Detection Foundation).
        services.AddScoped<IFraudDetectionService, FraudDetectionService>();

        // Module 5B (OCR Integration). DocumentFieldExtractor is a static
        // helper (no external dependency, no state) - no DI registration
        // needed for it.
        services.AddScoped<IOcrDocumentService, OcrDocumentService>();

        // Module 5C (OCR-Based Fraud Comparison). FieldComparer is a static
        // helper, same reasoning as DocumentFieldExtractor above - no DI
        // registration needed for it.
        services.AddScoped<IDocumentComparisonService, DocumentComparisonService>();

        return services;
    }
}
