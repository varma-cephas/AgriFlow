using backend.Data;
using backend.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = "Agent")]
[Route("api/agent")]
public class AgentController(AppDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();

        var agent = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == agentId, ct);
        if (agent is null) return Unauthorized();

        var farmerIds = await AssignedFarmerIds(agentId.Value, ct);
        var today = DateTime.UtcNow.Date;
        var dueBy = today.AddDays(14);

        var farmers = await FarmerSummaries(farmerIds, today, ct);
        var activeBatches = await db.PlantingBatches.CountAsync(b => farmerIds.Contains(b.FarmerId) && b.Status == "Active", ct);
        var harvestsDue = await db.PlantingBatches.CountAsync(b => farmerIds.Contains(b.FarmerId) && b.Status == "Active" && b.PredictedHarvestDate >= today && b.PredictedHarvestDate <= dueBy, ct);
        var ordersToHandle = await db.OrderItems
            .Join(db.Listings, item => item.ListingId, listing => listing.Id, (item, listing) => new { item, listing })
            .CountAsync(x => farmerIds.Contains(x.listing.FarmerId) && (x.item.Status == "Reserved" || x.item.Status == "Placed"), ct);

        return Ok(new
        {
            agent = new
            {
                id = agent.Id,
                name = agent.FullName,
                district = farmers.Select(f => (string)f.District!).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().SingleOrDefault() ?? ""
            },
            kpis = new
            {
                assignedFarmers = farmerIds.Count,
                activeBatches,
                harvestsDueIn14Days = harvestsDue,
                ordersToHandle
            },
            farmers,
            needsAttention = farmers
                .SelectMany(f => ((IEnumerable<object>)f.Needs).Select(need => new { farmerId = f.FarmerId, farmer = f.Name, need }))
                .ToList()
        });
    }

    [HttpGet("farmers")]
    public async Task<IActionResult> Farmers(CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();
        var farmerIds = await AssignedFarmerIds(agentId.Value, ct);
        return Ok(await FarmerSummaries(farmerIds, DateTime.UtcNow.Date, ct));
    }


    [HttpGet("farmers/{farmerId:int}")]
    public async Task<IActionResult> Farmer(int farmerId, CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();
        if (!await IsAssigned(agentId.Value, farmerId, ct)) return Forbid();

        var summaries = await FarmerSummaries([farmerId], DateTime.UtcNow.Date, ct);
        return summaries.Count == 0 ? NotFound() : Ok(summaries[0]);
    }

    [HttpGet("visit-requests")]
    public async Task<IActionResult> VisitRequests([FromQuery] string status = "Requested", CancellationToken ct = default)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();
        if (!new[] { "Requested", "Done", "All" }.Contains(status, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "status must be Requested, Done, or All." });

        var query = db.FarmVisitRequests.AsNoTracking()
            .Where(r => r.AgentId == agentId.Value)
            .Join(db.FarmerProfiles, r => r.FarmerId, farmer => farmer.UserId, (r, farmer) => new { r, farmer })
            .Join(db.Users, x => x.r.RequestedByUserId, requester => requester.Id, (x, requester) => new { x.r, x.farmer, requester });

        if (!status.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.r.Status == status);

        var result = await query.OrderBy(x => x.r.VisitBy).Select(x => new
        {
            id = x.r.Id,
            farmer = new { id = x.farmer.UserId, name = x.farmer.User.FullName, farm = x.farmer.FarmName, phone = x.farmer.User.Phone },
            location = new { x.farmer.Province, x.farmer.District, x.farmer.Sector, x.farmer.Cell, x.farmer.Village },
            landUpi = x.farmer.LandUpi,
            visitBy = x.r.VisitBy,
            requestedBy = new { id = x.requester.Id, name = x.requester.FullName },
            note = x.r.Note,
            status = x.r.Status,
            completedAt = x.r.CompletedAt
        }).ToListAsync(ct);

        return Ok(result);
    }

    [HttpPost("visit-requests/{id:int}/complete")]
    public async Task<IActionResult> CompleteVisit(int id, [FromBody] Models.Agent.CompleteVisitRequest request, CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();

        var visit = await db.FarmVisitRequests.FirstOrDefaultAsync(r => r.Id == id && r.AgentId == agentId.Value, ct);
        if (visit is null) return NotFound();
        if (visit.Status != "Requested") return Conflict(new { message = "This visit request has already been completed or cancelled." });

        var farmer = await db.FarmerProfiles.FirstOrDefaultAsync(f => f.UserId == visit.FarmerId, ct);
        if (farmer is null) return NotFound(new { message = "The farmer profile no longer exists." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        visit.Status = "Done";
        visit.CompletedAt = DateTime.UtcNow;
        farmer.VerificationStatus = "Verified";
        farmer.VerifiedByUserId = agentId.Value;
        farmer.VerifiedAt = request.VisitDate!.Value.Date;
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = agentId.Value,
            OnBehalfOfFarmerId = farmer.UserId,
            Entity = "FarmVisitRequest",
            EntityId = id.ToString(),
            Action = "Completed farm verification visit",
            Summary = request.Findings.Trim(),
            Result = "Success"
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { id = visit.Id, status = visit.Status, completedAt = visit.CompletedAt, verifiedAt = farmer.VerifiedAt });
    }

    [HttpPost("visit-requests/{id:int}/cannot-verify")]
    public async Task<IActionResult> CannotVerify(int id, [FromBody] Models.Agent.CannotVerifyRequest request, CancellationToken ct)
    {
        var agentId = GetUserId();
        if (agentId is null) return Unauthorized();

        var visit = await db.FarmVisitRequests.FirstOrDefaultAsync(r => r.Id == id && r.AgentId == agentId.Value, ct);
        if (visit is null) return NotFound();
        if (visit.Status != "Requested") return Conflict(new { message = "This visit request has already been completed or cancelled." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        visit.Status = "Cancelled";
        visit.CompletedAt = null;
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = agentId.Value,
            OnBehalfOfFarmerId = visit.FarmerId,
            Entity = "FarmVisitRequest",
            EntityId = id.ToString(),
            Action = "Could not verify farm",
            Summary = request.Reason.Trim(),
            Result = "Success"
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(new { id = visit.Id, status = visit.Status });
    }

    private async Task<List<int>> AssignedFarmerIds(int agentId, CancellationToken ct) =>
        await db.AgentFarmerAssignments.AsNoTracking().Where(a => a.AgentId == agentId).Select(a => a.FarmerId).ToListAsync(ct);

    private async Task<bool> IsAssigned(int agentId, int farmerId, CancellationToken ct) =>
        await db.AgentFarmerAssignments.AnyAsync(a => a.AgentId == agentId && a.FarmerId == farmerId, ct);

    private async Task<List<dynamic>> FarmerSummaries(List<int> farmerIds, DateTime today, CancellationToken ct)
    {
        if (farmerIds.Count == 0) return [];

        var profiles = await db.FarmerProfiles.AsNoTracking().Where(f => farmerIds.Contains(f.UserId)).Include(f => f.User).ToListAsync(ct);
        var batches = await db.PlantingBatches.AsNoTracking().Where(b => farmerIds.Contains(b.FarmerId) && b.Status == "Active").ToListAsync(ct);
        var stocks = await db.SeedStocks.AsNoTracking().Where(s => farmerIds.Contains(s.FarmerId)).ToListAsync(ct);
        var orderRows = await db.OrderItems.AsNoTracking()
            .Join(db.Listings.AsNoTracking(), item => item.ListingId, listing => listing.Id, (item, listing) => new { item, listing })
            .Where(x => farmerIds.Contains(x.listing.FarmerId) && (x.item.Status == "Reserved" || x.item.Status == "Placed"))
            .ToListAsync(ct);

        return profiles.Select(profile =>
        {
            var farmerBatches = batches.Where(b => b.FarmerId == profile.UserId).ToList();
            var farmerStocks = stocks.Where(s => s.FarmerId == profile.UserId).ToList();
            var confirmCount = orderRows.Count(x => x.listing.FarmerId == profile.UserId && x.item.Status == "Placed");
            var needs = new List<object>();
            if (farmerBatches.Any(b => b.PredictedHarvestDate >= today && b.PredictedHarvestDate <= today.AddDays(14))) needs.Add(new { type = "HarvestDue", message = "Harvest due within 14 days" });
            if (farmerStocks.Any(s => s.Quantity <= 10)) needs.Add(new { type = "LowSeed", message = "Seed stock is low" });
            if (confirmCount > 0) needs.Add(new { type = "OrderToConfirm", message = $"{confirmCount} order(s) to confirm" });

            return (dynamic)new
            {
                FarmerId = profile.UserId,
                Name = profile.User.FullName,
                Farm = profile.FarmName,
                District = profile.District,
                Phone = profile.User.Phone,
                ActiveBatches = farmerBatches.Count,
                LowStockCount = farmerStocks.Count(s => s.Quantity <= 10),
                OrdersToConfirm = confirmCount,
                SeasonProgress = CalculateProgress(farmerBatches, today),
                Needs = needs
            };
        }).ToList();
    }

    private static int CalculateProgress(IEnumerable<PlantingBatch> batches, DateTime today)
    {
        var values = batches.Select(b =>
        {
            var totalDays = Math.Max(1, (b.PredictedHarvestDate.Date - b.PlantingDate.Date).Days);
            var elapsed = (today - b.PlantingDate.Date).Days;
            return Math.Clamp((int)Math.Round(elapsed * 100d / totalDays), 0, 100);
        }).ToList();
        return values.Count == 0 ? 0 : (int)Math.Round(values.Average());
    }

    private int? GetUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
