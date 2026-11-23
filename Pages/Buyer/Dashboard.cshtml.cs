using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Buyer;

[Authorize(Roles = "Buyer")]
public class DashboardModel : PageModel
{
    public void OnGet()
    {
    }
}