using AgriFlow.Data;
using AgriFlow.Models.ViewModels;
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

        TempData["SuccessMessage"] = "Your business account was submitted for verification. You can sign in after an administrator approves it.";
        return RedirectToPage("/Account/Login");
    }
}