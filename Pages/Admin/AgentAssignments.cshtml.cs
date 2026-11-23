using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AgriFlow.Pages.Admin;
[Authorize(Roles="Admin")]
public class AgentAssignmentsModel : PageModel
{
    private readonly AdminRepository _repo;
    public IReadOnlyList<AdminAgentRow> Agents { get; private set; } = [];
    public IReadOnlyList<AdminFarmerRow> Farmers { get; private set; } = [];
    [BindProperty] public int SelectedAgentId { get; set; }
    [BindProperty] public int[] SelectedFarmerIds { get; set; } = [];
    public AgentAssignmentsModel(AdminRepository repo) => _repo = repo;
    public async Task OnGetAsync() { Agents=await _repo.GetAgentsAsync(); Farmers=await _repo.GetFarmersAsync(); }
    public async Task<IActionResult> OnPostAssignAsync() { if (SelectedAgentId > 0 && SelectedFarmerIds.Length > 0) await _repo.AssignFarmersAsync(SelectedAgentId, SelectedFarmerIds); return RedirectToPage(); }
}
