using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Admin;
[Authorize(Roles="Admin")]
public class VerificationModel : PageModel
{
    private readonly AdminRepository _repo;
    public IReadOnlyList<PendingFarmerRow> Farmers { get; private set; } = [];
    public IReadOnlyList<PendingBusinessRow> Businesses { get; private set; } = [];
    [BindProperty] public int RecordId { get; set; }
    [BindProperty] public string? RejectionReason { get; set; }
    public VerificationModel(AdminRepository repo) => _repo = repo;
    public async Task OnGetAsync() { await LoadAsync(); }
    public async Task<IActionResult> OnPostFarmerAsync(string decision)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _repo.SetFarmerVerificationAsync(RecordId, adminId, decision == "approve" ? "Verified" : "Rejected", RejectionReason);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostBusinessAsync(string decision)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _repo.SetBusinessVerificationAsync(RecordId, adminId, decision == "approve" ? "Verified" : "Rejected", RejectionReason);
        return RedirectToPage();
    }
    private async Task LoadAsync() { Farmers = await _repo.GetPendingFarmersAsync(); Businesses = await _repo.GetPendingBusinessesAsync(); }
}
