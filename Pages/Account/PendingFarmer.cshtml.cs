using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Account;

[Authorize(Roles = "Farmer")]
public class PendingFarmerModel : PageModel
{
    private readonly AuthRepository _authRepo;
    public PendingFarmerModel(AuthRepository authRepo) => _authRepo = authRepo;

    public string FarmName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LandUpi { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string CreatedAtText { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var info = await _authRepo.GetPendingFarmerAsync(userId);
        if (info is null) return RedirectToPage("/Index");

        if (info.Status == "Verified") return RedirectToPage("/Index");

        FarmName = info.FarmName;
        FirstName = info.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? info.FullName;
        LandUpi = info.LandUpi;
        District = info.District;
        CreatedAtText = info.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
        return Page();
    }
}