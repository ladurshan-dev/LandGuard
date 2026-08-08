using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Exposes the (Phase 1, dummy/in-memory) government land registry over
/// HTTP - Government Registry Module, Phase 2. Every action here is a thin
/// translation from HTTP to <see cref="IGovernmentRegistryService"/> and
/// back, the same split every other controller in this solution
/// establishes; this controller injects the interface only, never
/// <c>DummyGovernmentRegistryService</c> directly, so a later phase can
/// swap in a real government registry integration with no change here.
///
/// A government land record carries the same kind of personally
/// identifying data (NIC, owner name, address) a real land registry would,
/// so - unlike <c>PropertyController</c>'s public Search/GetById - these
/// endpoints are not anonymous. They follow <c>OcrController</c>'s
/// RequireSellerOrAdmin convention rather than <c>FraudController</c>'s
/// blanket [Authorize]: this is the same "Seller submitting/verifying
/// their own deed, or an Admin reviewing it" audience as OCR extraction,
/// not the "any authenticated role including Buyer" audience
/// FraudController's GetReport/GetHistory serve (those expose only a
/// listing's already-derived risk summary to buyers browsing that
/// listing, never raw third-party registry data keyed by an arbitrary
/// NIC/deed/property reference).
///
/// Lookups are the only capability exposed here - no OCR comparison, no
/// fraud-engine interaction, no PDF generation. Those remain later phases.
/// </summary>
[ApiController]
[Route("api/government-registry")]
[Authorize(Policy = AuthorizationPolicies.RequireSellerOrAdmin)]
public class GovernmentRegistryController : ControllerBase
{
    private readonly IGovernmentRegistryService _governmentRegistryService;

    public GovernmentRegistryController(IGovernmentRegistryService governmentRegistryService)
    {
        _governmentRegistryService = governmentRegistryService;
    }

    /// <summary>GET /api/government-registry/nic/{nic} - the trusted government record registered against an NIC, if any.</summary>
    [HttpGet("nic/{nic}")]
    public async Task<IActionResult> GetByNic(string nic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nic))
        {
            return BadRequest(new { errors = new[] { "NIC is required." } });
        }

        var record = await _governmentRegistryService.GetByNicAsync(nic, cancellationToken);

        return record is not null
            ? Ok(record)
            : NotFound(new { errors = new[] { "No government record found for the given NIC." } });
    }

    /// <summary>GET /api/government-registry/deed/{deedNumber} - the trusted government record registered against a deed number, if any.</summary>
    [HttpGet("deed/{deedNumber}")]
    public async Task<IActionResult> GetByDeedNumber(string deedNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deedNumber))
        {
            return BadRequest(new { errors = new[] { "Deed number is required." } });
        }

        var record = await _governmentRegistryService.GetByDeedNumberAsync(deedNumber, cancellationToken);

        return record is not null
            ? Ok(record)
            : NotFound(new { errors = new[] { "No government record found for the given deed number." } });
    }

    /// <summary>GET /api/government-registry/property/{propertyReference} - the trusted government record registered against a property reference, if any.</summary>
    [HttpGet("property/{propertyReference}")]
    public async Task<IActionResult> GetByPropertyReference(string propertyReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(propertyReference))
        {
            return BadRequest(new { errors = new[] { "Property reference is required." } });
        }

        var record = await _governmentRegistryService.GetByPropertyReferenceAsync(propertyReference, cancellationToken);

        return record is not null
            ? Ok(record)
            : NotFound(new { errors = new[] { "No government record found for the given property reference." } });
    }
}
