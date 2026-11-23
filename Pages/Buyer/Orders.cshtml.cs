using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class OrdersModel : PageModel
{
    private readonly BuyerRepository _repository;
    public IReadOnlyList<BuyerOrderRow> Orders { get; private set; } = [];
    public OrdersModel(BuyerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => Orders = await _repository.GetOrdersAsync(UserId());
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
