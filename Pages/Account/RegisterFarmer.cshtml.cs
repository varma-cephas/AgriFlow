using System.Security.Claims;
using AgriFlow.Data;
using AgriFlow.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Account;

public class RegisterFarmerModel : PageModel
{
    private readonly AuthRepository _authRepo;

    public RegisterFarmerModel(AuthRepository authRepo) => _authRepo = authRepo;

    [BindProperty]
    public FarmerRegisterVm Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _authRepo.RegisterFarmerAsync(Input);
        if (!result.Success)
        {
            ModelState.AddModelError(result.ErrorField ?? string.Empty, result.ErrorMessage!);
            return Page();
        }

        await SignInUserAsync(result, isPersistent: true);
        return RedirectToPage("/Account/PendingFarmer");
    }

    private async Task SignInUserAsync(AuthResult user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = isPersistent });
    }
}