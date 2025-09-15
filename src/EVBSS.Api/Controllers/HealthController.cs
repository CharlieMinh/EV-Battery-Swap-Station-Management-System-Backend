using Microsoft.AspNetCore.Mvc;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { message = "pong", time = DateTime.UtcNow });
}
