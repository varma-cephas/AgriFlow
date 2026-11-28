using System.Security.Claims;
using backend.Models.Orders;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Roles = "Buyer")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !int.TryParse(User.FindFirstValue("buyer_business_id"), out var businessId))
            return Forbid();

        var result = await orderService.PlaceOrderAsync(userId, businessId, request, ct);

        return result.Outcome switch
        {
            ServiceOutcome.Ok => StatusCode(StatusCodes.Status201Created, result.Data),
            ServiceOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Message }),
            ServiceOutcome.NotFound => NotFound(new { message = result.Message }),
            ServiceOutcome.Conflict => Conflict(new { message = result.Message }),
            _ => BadRequest(new { message = result.Message })
        };
    }
}