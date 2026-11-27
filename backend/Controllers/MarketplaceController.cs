using backend.Models.Marketplace;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/marketplace")]
[Authorize] // any signed-in user
public class MarketplaceController(IMarketplaceService marketplaceService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] MarketplaceQuery query, CancellationToken ct)
    {
        var (success, message, data) = await marketplaceService.BrowseAsync(query, ct);
        return success ? Ok(data) : BadRequest(new { message });
    }
}