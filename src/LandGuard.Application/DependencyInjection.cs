using System.Reflection;
using FluentValidation;
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
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
