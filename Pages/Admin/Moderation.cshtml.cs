using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgriFlow.Pages.Admin;

[Authorize(Roles = "Admin")]
public class ModerationModel : PageModel
{
    private readonly AdminRepository _repository;
    public IReadOnlyList<PendingFarmerRow> Farmers { get; private set; } = [];
    public IReadOnlyList<PendingBusinessRow> Businesses { get; private set; } = [];
    public ModerationModel(AdminRepository repository) => _repository = repository;
    public async Task OnGetAsync()
    {
        Farmers = await _repository.GetPendingFarmersAsync();
        Businesses = await _repository.GetPendingBusinessesAsync();
    }
}
