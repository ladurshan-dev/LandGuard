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

        // Government Registry module, Phase 4 (deed comparison).
        // DeedFieldComparer, like DocumentFieldExtractor above, is a
        // static helper with no external dependency or state - no DI
        // registration needed for it either.
        services.AddScoped<IGovernmentDeedComparisonService, GovernmentDeedComparisonService>();

        // Government Registry module, Phase 5A (deed fraud classification).
        // No controller consumes this yet (that's Phase 5C) - registered
        // now anyway, alongside IGovernmentDeedComparisonService directly
        // above, so the composition root already lists every business
        // service this module exposes in one place, the same reason this
        // method's own doc comment gives for registering services
        // explicitly rather than waiting until something calls them.
        services.AddScoped<IGovernmentDeedFraudDetectionService, GovernmentDeedFraudDetectionService>();

        // Government Registry module, Phase 5B (deed verification
        // persistence orchestrator). No controller consumes this yet
        // (that's Phase 5C) - registered now anyway, for the same reason
        // IGovernmentDeedFraudDetectionService was in Phase 5A (see that
        // registration's own comment, directly above).
        services.AddScoped<IGovernmentDeedVerificationService, GovernmentDeedVerificationService>();

        // Phase B2 (Admin Property Moderation API): the manual
        // approve/reject override path wrapping the existing
        // usp_Admin_ApproveProperty/usp_Admin_RejectProperty procedures -
        // see IAdminModerationService's own doc comment for why this
        // exists alongside, not instead of, the automatic score-driven
        // Property.Status transition.
        services.AddScoped<IAdminModerationService, AdminModerationService>();

        return services;
    }
}
