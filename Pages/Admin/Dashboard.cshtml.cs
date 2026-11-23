using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Admin;
[Authorize(Roles="Admin")]
public class DashboardModel : PageModel
{
    private readonly AdminRepository _repo;
    public AdminStats Stats { get; private set; } = new(0,0,0,0,0,0,0);
    public DashboardModel(AdminRepository repo) => _repo = repo;
    public async Task OnGetAsync() => Stats = await _repo.GetStatsAsync();
}
