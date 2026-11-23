using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Agent;

[Authorize(Roles = "Agent")]
public class DashboardModel : PageModel
{
    private readonly AgentRepository _repository;

    public AgentOverview Overview { get; private set; } = new(0, 0, 0, 0);
    public IReadOnlyList<AgentFarmerRow> Farmers { get; private set; } = [];

    public DashboardModel(AgentRepository repository) => _repository = repository;

    public async Task OnGetAsync()
    {
        var agentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Overview = await _repository.GetOverviewAsync(agentId);
        Farmers = await _repository.GetFarmersAsync(agentId);
    }
}
