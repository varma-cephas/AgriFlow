using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages
{
    public class IndexModel : PageModel
    {
        private readonly SeedStockRepository _seedStockRepository;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(SeedStockRepository seedStockRepository, ILogger<IndexModel> logger)
        {
            _seedStockRepository = seedStockRepository;
            _logger = logger;
        }

        public int? LowStockCount { get; private set; }
        public string SummaryMessage { get; private set; } = string.Empty;

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            ViewData["UserRole"] = role;

            if (!SeedStockRepository.CanViewInventory(role))
            {
                SummaryMessage = "Low-stock information is not available for this account.";
                return;
            }

            var actorIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? actorId = int.TryParse(actorIdValue, out var parsedActorId) ? parsedActorId : null;
            if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !actorId.HasValue)
            {
                SummaryMessage = "Your account could not be verified to load low-stock information.";
                return;
            }

            try
            {
                LowStockCount = await _seedStockRepository.GetLowStockCountAsync(role, actorId, cancellationToken);
            }
            catch (SqlException exception)
            {
                _logger.LogError(exception, "Could not load the low-stock summary for role {Role} and user {UserId}.", role, actorId);
                SummaryMessage = "The low-stock summary could not be loaded. Please try again.";
            }
        }
    }
}
