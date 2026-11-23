using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AgriFlow.Data;
using AgriFlow.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthRepository _authRepo;

    public LoginModel(AuthRepository authRepo) => _authRepo = authRepo;

    [BindProperty]
    public LoginVm Input { get; set; } = new();

    [BindProperty]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string? ResetEmail { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Remove(nameof(ResetEmail));
        if (!ModelState.IsValid) return Page();

        var result = await _authRepo.AuthenticateAsync(Input.Email, Input.Password);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage!);
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.FullName),
            new(ClaimTypes.Role, result.Role)
        };
        if (result.BuyerBusinessId.HasValue)
        {
            claims.Add(new Claim("BuyerBusinessId", result.BuyerBusinessId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = Input.RememberMe });

        return result.Role switch
        {
            "Agent" => RedirectToPage("/Agent/Dashboard"),
            "Farmer" => RedirectToPage("/Farmer/Dashboard"),
            "Buyer" => RedirectToPage("/Buyer/Dashboard"),
            "Admin" => RedirectToPage("/Admin/Dashboard"),
            _ => RedirectToPage("/Index")
        };
    }

    public IActionResult OnPostForgotPassword()
    {
        if (string.IsNullOrWhiteSpace(ResetEmail))
        {
            TempData["ResetError"] = "Please enter the email address you signed up with.";
            return RedirectToPage();
        }

        TempData["SuccessMessage"] = $"If an account exists for {ResetEmail}, a password reset link has been sent.";
        return RedirectToPage();
    }
}