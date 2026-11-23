using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Farmer;
[Authorize(Roles="Farmer")]
public class PlantingModel : PageModel
{
    private readonly FarmerRepository _repository;
    public IReadOnlyList<FarmerStockRow> Stock { get; private set; } = [];
    public IReadOnlyList<FarmerBatchRow> Batches { get; private set; } = [];
    [BindProperty] public int StockId { get; set; }
    [BindProperty] public decimal Quantity { get; set; }
    [BindProperty] public DateTime PlantingDate { get; set; } = DateTime.Today;
    public string? Error { get; private set; }
    public PlantingModel(FarmerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        try { await _repository.RecordPlantingAsync(UserId(), StockId, Quantity, PlantingDate); TempData["Success"] = "Planting batch recorded and seed stock updated."; return RedirectToPage(); }
        catch (InvalidOperationException ex) { Error = ex.Message; await LoadAsync(); return Page(); }
    }
    private async Task LoadAsync() { var id=UserId(); Stock=await _repository.GetStockAsync(id); Batches=await _repository.GetBatchesAsync(id); }
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
