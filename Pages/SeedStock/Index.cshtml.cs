using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.SeedStock;

[Authorize(Roles = "Farmer,Agent,Admin")]
public sealed class IndexModel : PageModel
{
    private readonly SeedStockRepository _seedStockRepository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(SeedStockRepository seedStockRepository, ILogger<IndexModel> logger)
    {
        _seedStockRepository = seedStockRepository;
        _logger = logger;
    }

    public IReadOnlyList<SeedStockItem> Items { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var actorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? actorId = int.TryParse(actorIdValue, out var parsedActorId) ? parsedActorId : null;

        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !actorId.HasValue)
        {
            ErrorMessage = "Your account could not be verified to load seed stock.";
            return;
        }

        try
        {
            Items = await _seedStockRepository.GetInventoryAsync(role, actorId, cancellationToken);
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Could not load seed stock for role {Role} and user {UserId}.", role, actorId);
            ErrorMessage = "Seed stock could not be loaded. Please try again.";
        }
    }
}
