using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/marketplace")]
[Authorize] // any signed-in user
public class ListingDetailsController(IListingDetailService detailService) : ControllerBase
{
    [HttpGet("{listingId:int}")]
    public async Task<IActionResult> Get(int listingId, CancellationToken ct)
    {
        var listing = await detailService.GetAsync(listingId, ct);
        return listing is null ? NotFound(new { message = "Listing not found." }) : Ok(listing);
    }
}