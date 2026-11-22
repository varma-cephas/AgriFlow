using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Farmer;

[Authorize(Roles = "Farmer")]
public class EditProfileModel : PageModel
{
    public void OnGet() { }
}