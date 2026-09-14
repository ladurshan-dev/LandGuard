using LandGuard.API.Authorization;
using LandGuard.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Admin Fraud Dashboard - a single read-only aggregation endpoint,
/// deliberately its own controller rather than an addition to
/// <see cref="AdminController"/> (property moderation: approve/reject) or
/// any fraud-analysis controller. Thin translation from HTTP to
/// <see cref="IAdminDashboardService"/> and back, the same split every
/// other controller in this solution uses; no business logic lives here.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _adminDashboardService;

    public AdminDashboardController(IAdminDashboardService adminDashboardService)
    {
        _adminDashboardService = adminDashboardService;
    }

    /// <summary>
    /// GET /api/admin/dashboard - Admin only (RequireAdmin policy, same as
    /// every other Admin endpoint). Returns the aggregated Users/
    /// Properties/DeedVerification/Risk/RuleTriggers/ReviewProperties
    /// sections - see <see cref="IAdminDashboardService"/>'s own doc
    /// comment for exactly what each one reads and how "latest deed
    /// verification" is computed.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _adminDashboardService.GetDashboardAsync(cancellationToken);

        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }
}
