using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class InvoicesModel : PageModel
{
    private readonly BuyerRepository _repository;
    public IReadOnlyList<BuyerInvoiceRow> Invoices { get; private set; } = [];
    public InvoicesModel(BuyerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => Invoices = await _repository.GetInvoicesAsync(UserId());
    public async Task<IActionResult> OnPostPayAsync(int orderId)
    {
        try { await _repository.PayInvoiceAsync(UserId(), orderId); TempData["SuccessMessage"] = "Payment recorded successfully. This payment is simulated."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToPage();
    }
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
