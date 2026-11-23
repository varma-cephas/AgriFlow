using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Account;

[Authorize(Roles = "Buyer")]
public class PendingBusinessModel : PageModel
{
    private readonly AuthRepository _authRepo;
    public PendingBusinessModel(AuthRepository authRepo) => _authRepo = authRepo;

    public string BusinessName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string RdbCompanyCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CreatedAtText { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var info = await _authRepo.GetPendingBusinessAsync(userId);
        if (info is null) return RedirectToPage("/Index");

        if (info.Status == "Verified") return RedirectToPage("/Index");

        BusinessName = info.BusinessName;
        FirstName = info.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? info.FullName;
        RdbCompanyCode = info.RdbCompanyCode;
        Email = info.Email;
        CreatedAtText = info.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
        return Page();
    }
}