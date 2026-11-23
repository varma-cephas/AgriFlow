using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Farmer;
[Authorize(Roles="Farmer")]
public class OrdersModel : PageModel
{
    private readonly FarmerRepository _repository;
    public IReadOnlyList<FarmerOrderRow> Orders { get; private set; } = [];
    public OrdersModel(FarmerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => Orders = await _repository.GetOrdersAsync(UserId());
    public async Task<IActionResult> OnPostStatusAsync(int orderId, string status) { if (status is "confirmed" or "dispatched" or "delivered" or "cancelled") await _repository.UpdateOrderStatusAsync(UserId(), orderId, status); return RedirectToPage(); }
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
