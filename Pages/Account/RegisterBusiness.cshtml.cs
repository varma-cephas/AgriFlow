using System.Security.Claims;
using AgriFlow.Data;
using AgriFlow.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Account;

public class RegisterBusinessModel : PageModel
{
    private readonly AuthRepository _authRepo;

    public RegisterBusinessModel(AuthRepository authRepo) => _authRepo = authRepo;

    [BindProperty]
    public BusinessRegisterVm Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _authRepo.RegisterBusinessAsync(Input);
        if (!result.Success)
        {
            ModelState.AddModelError(result.ErrorField ?? string.Empty, result.ErrorMessage!);
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.FullName),
            new(ClaimTypes.Role, result.Role),
            new("BuyerBusinessId", result.BuyerBusinessId!.Value.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return RedirectToPage("/Account/PendingBusiness");
    }
}