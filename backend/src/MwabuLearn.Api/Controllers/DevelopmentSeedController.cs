using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MwabuLearn.Api.Security;
using MwabuLearn.Application.Education;
using MwabuLearn.Application.Identity;
namespace MwabuLearn.Api.Controllers;
[ApiController, Authorize, RequireHttps, Route("api/development/seed")]
public sealed class DevelopmentSeedController(IDevelopmentSeed seed, IWebHostEnvironment environment, IConfiguration configuration) : ControllerBase
{
    [HttpPost, RequirePermission(PermissionCodes.UsersManage, PermissionScope.Platform)]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DevelopmentSeed:Enabled")) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(await seed.RunAsync(ct));
    }
}
