using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Buyers go straight to their dashboard.
            if (User.IsInRole("Buyer")) return RedirectToPage("/Buyer/Dashboard");

            return Page();
        }
    }
}