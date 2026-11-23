using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages
{
[Authorize]
public class IndexModel : PageModel
    {
    public IActionResult OnGet()
        {
        if (User.IsInRole("Agent")) return RedirectToPage("/Agent/Dashboard");
        if (User.IsInRole("Farmer")) return RedirectToPage("/Farmer/Dashboard");
        if (User.IsInRole("Buyer")) return RedirectToPage("/Buyer/Dashboard");
        if (User.IsInRole("Admin")) return RedirectToPage("/Admin/Dashboard");

        // Admin and any future roles can use the general landing page until
        // their dedicated workspace is added.
        return Page();
        }
    }
}
