using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Farmer;
[Authorize(Roles="Farmer")]
public class SeedStockModel : PageModel
{
    private readonly FarmerRepository _repository;
    public IReadOnlyList<FarmerStockRow> Stock { get; private set; } = [];
    [BindProperty] public StockInput Input { get; set; } = new();
    public SeedStockModel(FarmerRepository repository) => _repository = repository;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        await _repository.AddStockAsync(UserId(), Input.CropName, Input.MaturityDays, Input.Quantity, Input.Unit);
        TempData["Success"] = "Seed stock added successfully.";
        return RedirectToPage();
    }
    private async Task LoadAsync() => Stock = await _repository.GetStockAsync(UserId());
    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public sealed class StockInput
    {
        [Required, StringLength(100)] public string CropName { get; set; } = "";
        [Range(1, 2000)] public int MaturityDays { get; set; } = 90;
        [Range(typeof(decimal), "0.01", "1000000")] public decimal Quantity { get; set; }
        [Required] public string Unit { get; set; } = "kg";
    }
}
