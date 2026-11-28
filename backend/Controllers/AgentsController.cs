using backend.Models.Agent;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/agents")]
    [Authorize(Roles = "Agent")]
    public class AgentsController(IAgentService agentService) : ControllerBase
    {
        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private IActionResult MapProxyAccessError(ProxyCheckOutcome outcome, int farmerId)
        {
            if (outcome.Status == ProxyAccessResult.FarmerNotFound)
                return NotFound(new { code = "FARMER_NOT_FOUND", message = "Farmer not found." });

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "NOT_ASSIGNED",
                farmerId,
                farmerName = outcome.FarmerName ?? "this farmer",
                message = "This farmer isn't assigned to you, so you can't view or change their records."
            });
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(CancellationToken ct)
        {
            var overview = await agentService.GetOverviewAsync(GetCurrentUserId(), ct);
            return overview is null ? NotFound(new { message = "Agent profile not found." }) : Ok(overview);
        }

        [HttpGet("my-farmers")]
        public async Task<IActionResult> GetMyFarmers(CancellationToken ct)
        {
            var farmers = await agentService.GetMyFarmersAsync(GetCurrentUserId(), ct);
            return Ok(farmers);
        }

        [HttpGet("farmers/{farmerId:int}/seed-stock")]
        public async Task<IActionResult> GetFarmerSeedStock(int farmerId, CancellationToken ct)
        {
            var (access, data) = await agentService.GetFarmerSeedStockAsync(GetCurrentUserId(), farmerId, ct);
            if (access.Status != ProxyAccessResult.Allowed)
                return MapProxyAccessError(access, farmerId);

            return Ok(data);
        }

        [HttpPost("farmers/{farmerId:int}/seed-stock")]
        public async Task<IActionResult> AddFarmerSeedStock(
            int farmerId, [FromBody] UpsertSeedStockRequest req, CancellationToken ct)
        {
            var (access, success, message, item) =
                await agentService.AddFarmerSeedStockAsync(GetCurrentUserId(), farmerId, req, ct);

            if (access.Status != ProxyAccessResult.Allowed)
                return MapProxyAccessError(access, farmerId);

            return success ? Ok(item) : BadRequest(new { message });
        }

        [HttpPut("farmers/{farmerId:int}/seed-stock/{seedStockId:int}")]
        public async Task<IActionResult> UpdateFarmerSeedStock(
            int farmerId, int seedStockId, [FromBody] UpsertSeedStockRequest req, CancellationToken ct)
        {
            var (access, success, message, item) =
                await agentService.UpdateFarmerSeedStockAsync(GetCurrentUserId(), farmerId, seedStockId, req, ct);

            if (access.Status != ProxyAccessResult.Allowed)
                return MapProxyAccessError(access, farmerId);

            return success ? Ok(item) : BadRequest(new { message });
        }

        [HttpDelete("farmers/{farmerId:int}/seed-stock/{seedStockId:int}")]
        public async Task<IActionResult> DeleteFarmerSeedStock(
            int farmerId, int seedStockId, CancellationToken ct)
        {
            var (access, success, message) =
                await agentService.DeleteFarmerSeedStockAsync(GetCurrentUserId(), farmerId, seedStockId, ct);

            if (access.Status != ProxyAccessResult.Allowed)
                return MapProxyAccessError(access, farmerId);

            return success ? Ok(new { message }) : BadRequest(new { message });
        }

        [HttpPost("farmers/{farmerId:int}/request-assignment")]
        public async Task<IActionResult> RequestAssignment(int farmerId, CancellationToken ct)
        {
            var (success, message) =
                await agentService.RequestFarmerAssignmentAsync(GetCurrentUserId(), farmerId, ct);

            return success ? Ok(new { message }) : NotFound(new { message });
        }

        [HttpGet("farm-visits")]
        public async Task<IActionResult> GetFarmVisits(CancellationToken ct)
        {
            var result = await agentService.GetFarmVisitRequestsAsync(GetCurrentUserId(), ct);
            return Ok(result);
        }

        [HttpPost("farm-visits/{visitId:int}/confirm")]
        public async Task<IActionResult> ConfirmFarmVisit(
            int visitId, [FromBody] ConfirmFarmVisitRequest req, CancellationToken ct)
        {
            var (success, message) = await agentService.ConfirmFarmVisitAsync(GetCurrentUserId(), visitId, req, ct);
            return success ? Ok(new { message }) : BadRequest(new { message });
        }

        [HttpPost("farm-visits/{visitId:int}/reject")]
        public async Task<IActionResult> RejectFarmVisit(
            int visitId, [FromBody] RejectFarmVisitRequest req, CancellationToken ct)
        {
            var (success, message) = await agentService.RejectFarmVisitAsync(GetCurrentUserId(), visitId, req, ct);
            return success ? Ok(new { message }) : BadRequest(new { message });
        }

        [HttpPost("farmers")]
        public async Task<IActionResult> RegisterFarmer(
    [FromBody] RegisterNonDigitalFarmerRequest req, CancellationToken ct)
        {
            var (success, message, data) = await agentService.RegisterNonDigitalFarmerAsync(
                GetCurrentUserId(), req, ct);

            if (!success || data is null)
                return BadRequest(new { message });

            return Ok(data);
        }

        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] string? action,
            [FromQuery] string? dateRange,
            CancellationToken ct)
        {
            var logs = await agentService.GetAgentAuditLogsAsync(
                GetCurrentUserId(), search, role, action, dateRange, ct);

            return Ok(logs);
        }
    }
}
