using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Farmer;

[Authorize(Roles = "Farmer")]
public class DashboardModel : PageModel
{
    private readonly FarmerRepository _repository;

    public FarmerOverview Overview { get; private set; } = new(0, 0, 0, 0, 0);

    public DashboardModel(FarmerRepository repository) => _repository = repository;

    public async Task OnGetAsync()
    {
        var farmerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Overview = await _repository.GetOverviewAsync(farmerId);
    }
}