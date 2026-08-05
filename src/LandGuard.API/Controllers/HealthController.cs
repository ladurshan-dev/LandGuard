using Microsoft.AspNetCore.Mvc;

namespace LandGuard.API.Controllers;

/// <summary>
/// Liveness/readiness endpoint. Used by deployment tooling and, during
/// development, to verify the API is reachable from the separately
/// hosted static frontend (i.e. CORS and networking are configured
/// correctly) before any business endpoints exist to test against.
/// Anonymous by design - a health check must work before a caller has a
/// JWT.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "Healthy",
        service = "LandGuard.API",
        timestampUtc = DateTime.UtcNow
    });
}
