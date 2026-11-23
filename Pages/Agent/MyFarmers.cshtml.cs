using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Agent;

[Authorize(Roles = "Agent")]
public class MyFarmersModel : PageModel
{
    private readonly AgentRepository _repository;
    public IReadOnlyList<AgentFarmerRow> Farmers { get; private set; } = [];

    public MyFarmersModel(AgentRepository repository) => _repository = repository;

    public async Task OnGetAsync()
    {
        var agentId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        Farmers = await _repository.GetFarmersAsync(agentId);
    }
}
