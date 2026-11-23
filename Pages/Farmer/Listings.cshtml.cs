using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Farmer;
[Authorize(Roles="Farmer")]
public class ListingsModel : PageModel
{
    private readonly FarmerRepository _repository;
    public IReadOnlyList<FarmerListingRow> Listings { get; private set; } = [];
    public IReadOnlyList<FarmerBatchRow> Batches { get; private set; } = [];
    [BindProperty] public int BatchId { get; set; }
    [BindProperty] public decimal Quantity { get; set; }
    [BindProperty] public decimal PricePerUnit { get; set; }
    [BindProperty] public string Unit { get; set; } = "kg";
    public string? Error { get; private set; }
    public ListingsModel(FarmerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostCreateAsync()
    {
        try { await _repository.CreateListingAsync(UserId(), BatchId, Quantity, PricePerUnit, Unit); TempData["Success"]="Harvest listing created."; return RedirectToPage(); }
        catch (InvalidOperationException ex) { Error=ex.Message; await LoadAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostToggleAsync(int listingId, bool active) { await _repository.SetListingStatusAsync(UserId(), listingId, active); return RedirectToPage(); }
    private async Task LoadAsync() { var id=UserId(); Listings=await _repository.GetListingsAsync(id); Batches=await _repository.GetBatchesAsync(id); }
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
