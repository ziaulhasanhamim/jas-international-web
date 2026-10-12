using Microsoft.AspNetCore.Mvc;
namespace JASIntWeb.Controllers;

[ApiController, Route("health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return await db.Database.CanConnectAsync(cancellationToken) ? Ok(new
            {
                status = "healthy"
            }) : StatusCode(503, new
            {
                status = "unavailable"
            });
    }
}
