using backend.Data;
using backend.Models.Agent;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace backend.Services
{
    public class AgentService(AppDbContext db) : IAgentService
    {
        private const decimal LowSeedThreshold = 10m;

        public async Task<AgentOverviewResponse?> GetOverviewAsync(int agentUserId, CancellationToken ct = default)
        {
            var agent = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == agentUserId && u.Role == "Agent", ct);

            if (agent is null)
                return null;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var in14Days = today.AddDays(14);
            var in7Days = today.AddDays(7);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // 1. Assigned farmers strictly scoped to this agent (BR-4)
            var assignments = await db.AgentFarmerAssignments
                .AsNoTracking()
                .Where(a => a.AgentId == agentUserId)
                .Include(a => a.Farmer)
                    .ThenInclude(fp => fp.User)
                .OrderBy(a => a.Farmer.UserId)
                .ToListAsync(ct);

            var farmerIds = assignments.Select(a => a.FarmerId).ToList();

            // 2. Scheduled farm visits for this agent
            var upcomingVisits = await db.FarmVisitRequests
                .AsNoTracking()
                .Where(v => v.AgentId == agentUserId && v.Status == "Requested")
                .Include(v => v.Farmer)
                    .ThenInclude(fp => fp.User)
                .OrderBy(v => v.VisitBy)
                .ToListAsync(ct);

            // 3. Seed stock for assigned farmers
            var allSeeds = await db.SeedStocks
                .AsNoTracking()
                .Where(s => farmerIds.Contains(s.FarmerId))
                .Include(s => s.Crop)
                .ToListAsync(ct);

            // 4. Planting batches for assigned farmers
            var allBatches = await db.PlantingBatches
                .AsNoTracking()
                .Where(b => farmerIds.Contains(b.FarmerId))
                .Include(b => b.SeedStock)
                    .ThenInclude(s => s.Crop)
                .ToListAsync(ct);

            // 5. Pending order items for assigned farmers' listings
            var pendingOrders = await db.OrderItems
                .AsNoTracking()
                .Where(oi => farmerIds.Contains(oi.Listing.FarmerId)
                          && (oi.Status == "Placed" || oi.Status == "Confirmed"))
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.BuyerBusiness)
                .Include(oi => oi.Listing)
                    .ThenInclude(l => l.Crop)
                .OrderBy(oi => oi.Order.CreatedAt)
                .ToListAsync(ct);

            // Extract district label from JobTitle (e.g., "Field agent · Nyagatare" -> "Nyagatare")
            var districtLabel = ExtractDistrict(agent.JobTitle, assignments.FirstOrDefault()?.Farmer.District);
            var roleTitle = string.IsNullOrWhiteSpace(agent.JobTitle)
                ? $"Field agent · {districtLabel}"
                : agent.JobTitle;

            // Next scheduled visit card
            NextVisitDto? nextVisit = null;
            var firstVisit = upcomingVisits.FirstOrDefault();
            if (firstVisit is not null)
            {
                nextVisit = new NextVisitDto(
                    VisitRequestId: firstVisit.Id,
                    FarmerId: firstVisit.FarmerId,
                    FarmerName: firstVisit.Farmer.User.FullName,
                    VisitBy: firstVisit.VisitBy.ToString("yyyy-MM-dd"),
                    RelativeLabel: FormatRelativeVisitDate(today, firstVisit.VisitBy),
                    Note: firstVisit.Note
                );
            }

            // KPI metrics
            var activeBatches = allBatches.Where(b => b.Status == "Active").ToList();
            var harvestsDue = activeBatches.Where(b => b.PredictedHarvestDate <= in14Days).ToList();

            var kpis = new AgentKpisDto(
                AssignedFarmersTotal: assignments.Count,
                NonDigitalFarmersCount: assignments.Count(a => string.IsNullOrEmpty(a.Farmer.User.PasswordHash)),
                NewFarmersThisMonth: assignments.Count(a => a.AssignedAt >= monthStart),
                ActiveBatchesTotal: activeBatches.Count,
                HarvestsDueWithin14Days: harvestsDue.Count,
                OrdersToHandleTotal: pendingOrders.Count,
                VisitsThisWeekCount: upcomingVisits.Count(v => v.VisitBy <= in7Days)
            );

            // Build "Your farmers at a glance" & "My farmers" table rows
            var glanceList = new List<FarmerGlanceDto>();
            var farmerRows = new List<AssignedFarmerRowDto>();

            foreach (var assignment in assignments)
            {
                var fp = assignment.Farmer;
                var u = fp.User;
                var initials = GetInitials(u.FullName);
                var nonDigitalCount = assignments.Count(a => a.Farmer.RegisteredByAgentId != null);
                var isNonDigital = assignment.Farmer.RegisteredByAgentId != null;

                var fBatches = allBatches.Where(b => b.FarmerId == u.Id).ToList();
                var fActiveBatches = fBatches.Where(b => b.Status == "Active").ToList();
                var fHarvestDueCount = fActiveBatches.Count(b => b.PredictedHarvestDate <= in14Days);
                var fLowSeedCount = allSeeds.Count(s => s.FarmerId == u.Id && s.Quantity <= LowSeedThreshold);
                var fOrdersToConfirm = pendingOrders.Count(oi => oi.Listing.FarmerId == u.Id && oi.Status == "Placed");
                var fHasVisit = upcomingVisits.Any(v => v.FarmerId == u.Id);

                // Compute season progress from elapsed days on active batches + completed batches
                var progressPercent = CalculateSeasonProgress(fBatches, today);

                var statusBadge = fOrdersToConfirm > 0 ? "Order to confirm"
                    : fHarvestDueCount > 0 ? "Harvest due"
                    : fLowSeedCount > 0 ? "Low seed"
                    : fHasVisit ? "Visit planned"
                    : "On track";

                glanceList.Add(new FarmerGlanceDto(
                    FarmerId: u.Id,
                    Initials: initials,
                    FullName: u.FullName,
                    FarmName: fp.FarmName,
                    District: fp.District,
                    SeasonProgressPercent: progressPercent,
                    StatusBadge: statusBadge
                ));

                farmerRows.Add(new AssignedFarmerRowDto(
                    FarmerId: u.Id,
                    Initials: initials,
                    FullName: u.FullName,
                    IsNonDigital: isNonDigital,
                    AccountBadge: isNonDigital ? "Non-digital" : "Self-manages too",
                    FarmName: fp.FarmName,
                    District: fp.District,
                    Phone: string.IsNullOrWhiteSpace(u.Phone) ? "No phone" : u.Phone,
                    ActiveBatchesCount: fActiveBatches.Count,
                    LowSeedCount: fLowSeedCount,
                    OrdersToConfirmCount: fOrdersToConfirm
                ));
            }

            // Build "Needs your attention" feed from real database conditions
            var attentionItems = new List<AttentionItemDto>();

            // 1. Harvests due or overdue grouped by farmer
            foreach (var group in harvestsDue.GroupBy(b => b.FarmerId))
            {
                var farmer = assignments.First(a => a.FarmerId == group.Key).Farmer.User;
                var earliest = group.OrderBy(b => b.PredictedHarvestDate).First();
                var count = group.Count();
                var readyDate = earliest.PredictedHarvestDate.ToString("dd MMM");

                attentionItems.Add(new AttentionItemDto(
                    Id: $"harvest-{group.Key}",
                    Category: "HarvestDue",
                    Title: count == 1 ? "1 harvest to record" : $"{count} harvests to record",
                    Subtitle: $"{farmer.FullName} · {earliest.SeedStock.Crop.Name} {earliest.SeedStock.Variety}, ready since {readyDate}",
                    ActionLabel: "Record",
                    FarmerId: group.Key
                ));
            }

            // 2. Low seed stock alerts (BR-5: Quantity <= 10)
            var lowSeeds = allSeeds
                .Where(s => s.Quantity <= LowSeedThreshold)
                .OrderBy(s => s.Quantity)
                .Take(2);

            foreach (var seed in lowSeeds)
            {
                var farmer = assignments.First(a => a.FarmerId == seed.FarmerId).Farmer.User;
                attentionItems.Add(new AttentionItemDto(
                    Id: $"seed-{seed.Id}",
                    Category: "LowSeed",
                    Title: $"{farmer.FullName} is low on seed",
                    Subtitle: $"{seed.Crop.Name} seed: {seed.Quantity:0.##} {seed.Unit} left",
                    ActionLabel: "Restock",
                    FarmerId: seed.FarmerId
                ));
            }

            // 3. Orders awaiting confirmation
            foreach (var item in pendingOrders.Where(oi => oi.Status == "Placed").Take(2))
            {
                var farmer = assignments.First(a => a.FarmerId == item.Listing.FarmerId).Farmer.User;
                attentionItems.Add(new AttentionItemDto(
                    Id: $"order-{item.Id}",
                    Category: "OrderToConfirm",
                    Title: $"{farmer.FullName} has an order to confirm",
                    Subtitle: $"{item.Order.BuyerBusiness.Name} · {item.QtyRequested:0.##} {item.Listing.Unit}s of {item.Listing.Crop.Name}",
                    ActionLabel: "Confirm",
                    FarmerId: item.Listing.FarmerId
                ));
            }

            return new AgentOverviewResponse(
                Agent: new AgentIdentityDto(agent.Id, agent.FullName, districtLabel, roleTitle),
                NextVisit: nextVisit,
                Kpis: kpis,
                FarmersAtAGlance: glanceList,
                AttentionItems: attentionItems,
                MyFarmers: farmerRows
            );
        }

        private static string ExtractDistrict(string? jobTitle, string? fallbackDistrict)
        {
            if (!string.IsNullOrWhiteSpace(jobTitle))
            {
                var parts = jobTitle.Split(['·', '-', '|'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    return parts[^1].Trim();
            }
            return fallbackDistrict ?? "Rwanda";
        }

        private static string FormatRelativeVisitDate(DateOnly today, DateOnly visitDate)
        {
            var diff = visitDate.DayNumber - today.DayNumber;
            return diff switch
            {
                < 0 => $"overdue ({visitDate:dd MMM})",
                0 => "today",
                1 => "tomorrow",
                <= 7 => $"in {diff} days ({visitDate:ddd, dd MMM})",
                _ => visitDate.ToString("dd MMM yyyy")
            };
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "AG";
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }

        private static int CalculateSeasonProgress(List<Data.Entities.PlantingBatch> batches, DateOnly today)
        {
            if (batches.Count == 0) return 0;

            double totalRatio = 0;
            foreach (var b in batches)
            {
                if (b.Status == "Harvested")
                {
                    totalRatio += 1.0;
                    continue;
                }

                var totalDays = Math.Max(1, b.PredictedHarvestDate.DayNumber - b.PlantingDate.DayNumber);
                var elapsedDays = Math.Clamp(today.DayNumber - b.PlantingDate.DayNumber, 0, totalDays);
                totalRatio += (double)elapsedDays / totalDays;
            }

            return (int)Math.Round((totalRatio / batches.Count) * 100);
        }

        public async Task<List<AssignedFarmerRowDto>> GetMyFarmersAsync(int agentUserId, CancellationToken ct = default)
        {
            var assignments = await db.AgentFarmerAssignments
                .AsNoTracking()
                .Where(a => a.AgentId == agentUserId)
                .Include(a => a.Farmer)
                    .ThenInclude(fp => fp.User)
                .OrderBy(a => a.Farmer.UserId)
                .ToListAsync(ct);

            var farmerIds = assignments.Select(a => a.FarmerId).ToList();

            var activeBatchCounts = await db.PlantingBatches
                .AsNoTracking()
                .Where(b => farmerIds.Contains(b.FarmerId) && b.Status == "Active")
                .GroupBy(b => b.FarmerId)
                .Select(g => new { FarmerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FarmerId, x => x.Count, ct);

            var lowSeedCounts = await db.SeedStocks
                .AsNoTracking()
                .Where(s => farmerIds.Contains(s.FarmerId) && s.Quantity <= LowSeedThreshold)
                .GroupBy(s => s.FarmerId)
                .Select(g => new { FarmerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FarmerId, x => x.Count, ct);

            var pendingOrderCounts = await db.OrderItems
                .AsNoTracking()
                .Where(oi => farmerIds.Contains(oi.Listing.FarmerId) && oi.Status == "Placed")
                .GroupBy(oi => oi.Listing.FarmerId)
                .Select(g => new { FarmerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FarmerId, x => x.Count, ct);

            return assignments.Select(a =>
            {
                var u = a.Farmer.User;
                var isNonDigital = a.Farmer.RegisteredByAgentId != null;
                return new AssignedFarmerRowDto(
                    FarmerId: u.Id,
                    Initials: GetInitials(u.FullName),
                    FullName: u.FullName,
                    IsNonDigital: isNonDigital,
                    AccountBadge: isNonDigital ? "Non-digital" : "Self-manages too",
                    FarmName: a.Farmer.FarmName,
                    District: a.Farmer.District,
                    Phone: string.IsNullOrWhiteSpace(u.Phone) ? "No phone" : u.Phone,
                    ActiveBatchesCount: activeBatchCounts.GetValueOrDefault(u.Id, 0),
                    LowSeedCount: lowSeedCounts.GetValueOrDefault(u.Id, 0),
                    OrdersToConfirmCount: pendingOrderCounts.GetValueOrDefault(u.Id, 0)
                );
            }).ToList();
        }

        private async Task<ProxyCheckOutcome> VerifyFarmerAssignmentAsync(int agentUserId, int farmerId, CancellationToken ct)
        {
            var farmer = await db.FarmerProfiles
                .AsNoTracking()
                .Include(fp => fp.User)
                .FirstOrDefaultAsync(fp => fp.UserId == farmerId, ct);

            if (farmer is null)
                return new ProxyCheckOutcome(ProxyAccessResult.FarmerNotFound);

            var isAssigned = await db.AgentFarmerAssignments
                .AsNoTracking()
                .AnyAsync(a => a.AgentId == agentUserId && a.FarmerId == farmerId, ct);

            return isAssigned
                ? new ProxyCheckOutcome(ProxyAccessResult.Allowed, farmer.User.FullName)
                : new ProxyCheckOutcome(ProxyAccessResult.NotAssigned, farmer.User.FullName);
        }

        public async Task<(ProxyCheckOutcome Access, FarmerSeedStockWorkspaceResponse? Data)> GetFarmerSeedStockAsync(
            int agentUserId, int farmerId, CancellationToken ct = default)
        {
            var access = await VerifyFarmerAssignmentAsync(agentUserId, farmerId, ct);
            if (access.Status != ProxyAccessResult.Allowed)
                return (access, null);

            var farmerProfile = await db.FarmerProfiles
                .AsNoTracking()
                .Include(fp => fp.User)
                .FirstAsync(fp => fp.UserId == farmerId, ct);

            var seeds = await db.SeedStocks
                .AsNoTracking()
                .Where(s => s.FarmerId == farmerId)
                .Include(s => s.Crop)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync(ct);

            var seedIds = seeds.Select(s => s.Id).ToList();
            var seedIdStrings = seedIds.Select(id => id.ToString()).ToList();

            var batchUsageMap = await db.PlantingBatches
                .AsNoTracking()
                .Where(b => seedIds.Contains(b.SeedStockId) && b.Status == "Active")
                .GroupBy(b => b.SeedStockId)
                .Select(g => new { SeedStockId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SeedStockId, x => x.Count, ct);

            // Fetch latest AuditLog entry per SeedStock item to show who updated it
            var latestLogs = await db.AuditLogs
                .AsNoTracking()
                .Where(l => l.Entity == "SeedStock"
                         && l.EntityId != null
                         && seedIdStrings.Contains(l.EntityId)
                         && l.Result == "Success")
                .OrderByDescending(l => l.At)
                .ToListAsync(ct);

            var actorIds = latestLogs.Select(l => l.ActorUserId).Distinct().ToList();
            var actors = await db.Users
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ct);

            var items = seeds.Select(s =>
            {
                var log = latestLogs.FirstOrDefault(l => l.EntityId == s.Id.ToString());
                var updatedByName = farmerProfile.User.FullName;
                var updatedByIsAgent = false;

                if (log is not null && actors.TryGetValue(log.ActorUserId, out var actor))
                {
                    updatedByName = actor.FullName;
                    updatedByIsAgent = actor.Role == "Agent";
                }

                return new SeedStockItemDto(
                    Id: s.Id,
                    CropId: s.CropId,
                    CropName: s.Crop.Name,
                    Variety: s.Variety,
                    Supplier: s.Supplier,
                    Quantity: s.Quantity,
                    Unit: s.Unit,
                    UsedByBatchesCount: batchUsageMap.GetValueOrDefault(s.Id, 0),
                    IsLowStock: s.Quantity <= LowSeedThreshold,
                    LastUpdatedDate: s.UpdatedAt.ToString("dd MMM"),
                    LastUpdatedByName: updatedByName,
                    LastUpdatedByIsAgent: updatedByIsAgent
                );
            }).ToList();

            var crops = await db.Crops
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CropOptionDto(c.Id, c.Name, c.MaturityDays))
                .ToListAsync(ct);

            var response = new FarmerSeedStockWorkspaceResponse(
                FarmerId: farmerId,
                FarmerName: farmerProfile.User.FullName,
                FarmerInitials: GetInitials(farmerProfile.User.FullName),
                FarmName: farmerProfile.FarmName,
                District: farmerProfile.District,
                Items: items,
                AvailableCrops: crops
            );

            return (access, response);
        }

        public async Task<(ProxyCheckOutcome Access, bool Success, string Message, SeedStockItemDto? Item)> AddFarmerSeedStockAsync(
            int agentUserId, int farmerId, UpsertSeedStockRequest req, CancellationToken ct = default)
        {
            var access = await VerifyFarmerAssignmentAsync(agentUserId, farmerId, ct);
            if (access.Status != ProxyAccessResult.Allowed)
                return (access, false, "Not authorized to act for this farmer.", null);

            var crop = await db.Crops.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CropId, ct);
            if (crop is null)
                return (access, false, "Selected crop does not exist.", null);

            var varietyTrimmed = req.Variety.Trim();
            var duplicate = await db.SeedStocks.AnyAsync(
                s => s.FarmerId == farmerId && s.CropId == req.CropId && s.Variety == varietyTrimmed, ct);

            if (duplicate)
                return (access, false, $"Seed stock for {crop.Name} ({varietyTrimmed}) already exists. Edit the existing row instead.", null);

            var agent = await db.Users.AsNoTracking().FirstAsync(u => u.Id == agentUserId, ct);

            var entity = new Data.Entities.SeedStock
            {
                FarmerId = farmerId,
                CropId = req.CropId,
                Variety = varietyTrimmed,
                Supplier = string.IsNullOrWhiteSpace(req.Supplier) ? null : req.Supplier.Trim(),
                Quantity = req.Quantity,
                Unit = req.Unit.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.SeedStocks.Add(entity);
            await db.SaveChangesAsync(ct);

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = farmerId,
                Entity = "SeedStock",
                EntityId = entity.Id.ToString(),
                Action = "CreateSeedStock",
                Summary = $"Added {entity.Quantity:0.##} {entity.Unit} of {crop.Name} ({entity.Variety})",
                Result = "Success"
            });
            await db.SaveChangesAsync(ct);

            var dto = new SeedStockItemDto(
                Id: entity.Id,
                CropId: crop.Id,
                CropName: crop.Name,
                Variety: entity.Variety,
                Supplier: entity.Supplier,
                Quantity: entity.Quantity,
                Unit: entity.Unit,
                UsedByBatchesCount: 0,
                IsLowStock: entity.Quantity <= LowSeedThreshold,
                LastUpdatedDate: entity.UpdatedAt.ToString("dd MMM"),
                LastUpdatedByName: agent.FullName,
                LastUpdatedByIsAgent: true
            );

            return (access, true, "Seed stock added.", dto);
        }

        public async Task<(ProxyCheckOutcome Access, bool Success, string Message, SeedStockItemDto? Item)> UpdateFarmerSeedStockAsync(
            int agentUserId, int farmerId, int seedStockId, UpsertSeedStockRequest req, CancellationToken ct = default)
        {
            var access = await VerifyFarmerAssignmentAsync(agentUserId, farmerId, ct);
            if (access.Status != ProxyAccessResult.Allowed)
                return (access, false, "Not authorized to act for this farmer.", null);

            var entity = await db.SeedStocks
                .Include(s => s.Crop)
                .FirstOrDefaultAsync(s => s.Id == seedStockId && s.FarmerId == farmerId, ct);

            if (entity is null)
                return (access, false, "Seed stock item not found.", null);

            var crop = await db.Crops.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CropId, ct);
            if (crop is null)
                return (access, false, "Selected crop does not exist.", null);

            var varietyTrimmed = req.Variety.Trim();
            var duplicate = await db.SeedStocks.AnyAsync(
                s => s.Id != seedStockId && s.FarmerId == farmerId && s.CropId == req.CropId && s.Variety == varietyTrimmed, ct);

            if (duplicate)
                return (access, false, $"Another seed stock entry for {crop.Name} ({varietyTrimmed}) already exists.", null);

            entity.CropId = req.CropId;
            entity.Variety = varietyTrimmed;
            entity.Supplier = string.IsNullOrWhiteSpace(req.Supplier) ? null : req.Supplier.Trim();
            entity.Quantity = req.Quantity;
            entity.Unit = req.Unit.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            var agent = await db.Users.AsNoTracking().FirstAsync(u => u.Id == agentUserId, ct);

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = farmerId,
                Entity = "SeedStock",
                EntityId = entity.Id.ToString(),
                Action = "UpdateSeedStock",
                Summary = $"Updated {crop.Name} ({entity.Variety}) to {entity.Quantity:0.##} {entity.Unit}",
                Result = "Success"
            });

            await db.SaveChangesAsync(ct);

            var usedBy = await db.PlantingBatches.CountAsync(b => b.SeedStockId == entity.Id && b.Status == "Active", ct);

            var dto = new SeedStockItemDto(
                Id: entity.Id,
                CropId: crop.Id,
                CropName: crop.Name,
                Variety: entity.Variety,
                Supplier: entity.Supplier,
                Quantity: entity.Quantity,
                Unit: entity.Unit,
                UsedByBatchesCount: usedBy,
                IsLowStock: entity.Quantity <= LowSeedThreshold,
                LastUpdatedDate: entity.UpdatedAt.ToString("dd MMM"),
                LastUpdatedByName: agent.FullName,
                LastUpdatedByIsAgent: true
            );

            return (access, true, "Seed stock updated.", dto);
        }

        public async Task<(ProxyCheckOutcome Access, bool Success, string Message)> DeleteFarmerSeedStockAsync(
            int agentUserId, int farmerId, int seedStockId, CancellationToken ct = default)
        {
            var access = await VerifyFarmerAssignmentAsync(agentUserId, farmerId, ct);
            if (access.Status != ProxyAccessResult.Allowed)
                return (access, false, "Not authorized to act for this farmer.");

            var entity = await db.SeedStocks
                .Include(s => s.Crop)
                .FirstOrDefaultAsync(s => s.Id == seedStockId && s.FarmerId == farmerId, ct);

            if (entity is null)
                return (access, false, "Seed stock item not found.");

            var inUse = await db.PlantingBatches.AnyAsync(b => b.SeedStockId == seedStockId, ct);
            if (inUse)
                return (access, false, "Cannot delete seed stock that is already linked to planting batches.");

            db.SeedStocks.Remove(entity);

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = farmerId,
                Entity = "SeedStock",
                EntityId = seedStockId.ToString(),
                Action = "DeleteSeedStock",
                Summary = $"Deleted {entity.Crop.Name} ({entity.Variety}) seed stock",
                Result = "Success"
            });

            await db.SaveChangesAsync(ct);
            return (access, true, "Seed stock deleted.");
        }

        public async Task<(bool Success, string Message)> RequestFarmerAssignmentAsync(
            int agentUserId, int farmerId, CancellationToken ct = default)
        {
            var farmer = await db.FarmerProfiles
                .AsNoTracking()
                .Include(fp => fp.User)
                .FirstOrDefaultAsync(fp => fp.UserId == farmerId, ct);

            if (farmer is null)
                return (false, "Farmer profile not found.");

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = farmerId,
                Entity = "AgentFarmerAssignment",
                EntityId = farmerId.ToString(),
                Action = "RequestAssignment",
                Summary = $"Agent requested assignment to act for {farmer.User.FullName} ({farmer.FarmName})",
                Result = "Success"
            });

            await db.SaveChangesAsync(ct);
            return (true, $"Assignment request for {farmer.User.FullName} has been logged for an administrator.");
        }

        public async Task<FarmVisitWorkspaceResponse> GetFarmVisitRequestsAsync(
            int agentUserId, CancellationToken ct = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var inSevenDays = today.AddDays(7);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Fetch verification visits for unverified self-registered farms (plus historical Done/Cancelled visits)
            var visits = await db.FarmVisitRequests
                .AsNoTracking()
                .Where(v => v.AgentId == agentUserId
                         && (v.Status != "Requested" || v.Farmer.VerificationStatus == "Unverified"))
                .Include(v => v.Farmer)
                    .ThenInclude(fp => fp.User)
                .OrderBy(v => v.Status == "Requested" ? 0 : 1)
                .ThenBy(v => v.VisitBy)
                .ToListAsync(ct);

            var requesterIds = visits.Select(v => v.RequestedByUserId).Distinct().ToList();
            var requesters = await db.Users
                .AsNoTracking()
                .Where(u => requesterIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            var waitingList = visits.Where(v => v.Status == "Requested").ToList();
            var dueThisWeek = waitingList.Count(v => v.VisitBy <= inSevenDays);
            var doneThisMonth = visits.Count(v => v.Status == "Done" && v.CompletedAt >= monthStart);
            var couldNotVerify = visits.Count(v => v.Status == "Cancelled");

            var rows = visits.Select(v =>
            {
                var fp = v.Farmer;
                var farmerName = fp.User.FullName;
                var districtSector = string.IsNullOrWhiteSpace(fp.Sector)
                    ? fp.District
                    : $"{fp.District} · {fp.Sector}";

                var cellVillageParts = new[] { fp.Cell, fp.Village }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                var cellVillage = string.Join(" · ", cellVillageParts);

                return new FarmVisitRowDto(
                    Id: v.Id,
                    FarmerId: v.FarmerId,
                    FarmerName: farmerName,
                    FarmerInitials: GetInitials(farmerName),
                    FarmName: fp.FarmName,
                    DistrictSector: districtSector,
                    CellVillage: string.IsNullOrWhiteSpace(cellVillage) ? "—" : cellVillage,
                    LandUpi: string.IsNullOrWhiteSpace(fp.LandUpi) ? "Not provided" : fp.LandUpi,
                    VisitByDate: v.VisitBy.ToString("dd MMM"),
                    RequestedByName: requesters.GetValueOrDefault(v.RequestedByUserId, "Administrator"),
                    Status: v.Status,
                    Note: v.Note,
                    CompletedDate: v.CompletedAt?.ToString("dd MMM yyyy")
                );
            }).ToList();

            var kpis = new FarmVisitKpisDto(
                WaitingCount: waitingList.Count,
                DueThisWeekCount: dueThisWeek,
                DoneThisMonthCount: doneThisMonth,
                CouldNotVerifyCount: couldNotVerify
            );

            return new FarmVisitWorkspaceResponse(kpis, rows);
        }

        public async Task<(bool Success, string Message)> ConfirmFarmVisitAsync(
            int agentUserId, int visitId, ConfirmFarmVisitRequest req, CancellationToken ct = default)
        {
            var visit = await db.FarmVisitRequests
                .Include(v => v.Farmer)
                    .ThenInclude(fp => fp.User)
                .FirstOrDefaultAsync(v => v.Id == visitId && v.AgentId == agentUserId, ct);

            if (visit is null)
                return (false, "Farm visit request not found.");

            if (visit.Status != "Requested")
                return (false, "This farm visit has already been processed.");

            var findings = req.Findings.Trim();
            visit.Status = "Done";
            visit.Note = findings;
            visit.CompletedAt = req.VisitDate.ToDateTime(TimeOnly.FromDateTime(DateTime.UtcNow), DateTimeKind.Utc);

            // Flip self-registered farmer_profile status to Verified and record verifier
            visit.Farmer.VerificationStatus = "Verified";
            visit.Farmer.VerifiedByUserId = agentUserId;
            visit.Farmer.VerifiedAt = DateTime.UtcNow;

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = visit.FarmerId,
                Entity = "FarmVisitRequest",
                EntityId = visit.Id.ToString(),
                Action = "ConfirmFarmVisit",
                Summary = $"Verified {visit.Farmer.FarmName} ({visit.Farmer.User.FullName}) on {req.VisitDate:yyyy-MM-dd}: {findings}",
                Result = "Success"
            });

            await db.SaveChangesAsync(ct);
            return (true, $"{visit.Farmer.FarmName} has been verified.");
        }
        public async Task<(bool Success, string Message)> RejectFarmVisitAsync(
            int agentUserId, int visitId, RejectFarmVisitRequest req, CancellationToken ct = default)
        {
            var visit = await db.FarmVisitRequests
                .Include(v => v.Farmer)
                    .ThenInclude(fp => fp.User)
                .FirstOrDefaultAsync(v => v.Id == visitId && v.AgentId == agentUserId, ct);

            if (visit is null)
                return (false, "Farm visit request not found.");

            if (visit.Status != "Requested")
                return (false, "This farm visit has already been processed.");

            var reason = req.Reason.Trim();
            visit.Status = "Cancelled";
            visit.Note = $"Couldn't verify: {reason}";
            visit.CompletedAt = DateTime.UtcNow;

            // Set farmer_profile status to Rejected and store the rejection reason
            visit.Farmer.VerificationStatus = "Rejected";
            visit.Farmer.RejectionReason = reason;
            visit.Farmer.VerifiedByUserId = null;
            visit.Farmer.VerifiedAt = null;

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = visit.FarmerId,
                Entity = "FarmVisitRequest",
                EntityId = visit.Id.ToString(),
                Action = "RejectFarmVisit",
                Summary = $"Rejected verification for {visit.Farmer.FarmName} ({visit.Farmer.User.FullName}): {reason}",
                Result = "Failed"
            });

            await db.SaveChangesAsync(ct);
            return (true, $"Verification rejected for {visit.Farmer.FarmName} and sent back to admin.");
        }


        public async Task<(bool Success, string Message, RegisterNonDigitalFarmerResponse? Data)> RegisterNonDigitalFarmerAsync(
        int agentUserId, RegisterNonDigitalFarmerRequest req, CancellationToken ct = default)
        {
            if (!req.ConsentConfirmed)
                return (false, "In-person farmer consent is required before registering.", null);

            var fullName = req.FullName.Trim();
            var village = req.Village.Trim();
            var farmName = req.FarmName.Trim();
            var province = req.Province.Trim();
            var district = req.District.Trim();
            var language = string.IsNullOrWhiteSpace(req.PreferredLanguage) ? "Kinyarwanda" : req.PreferredLanguage.Trim();
            var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneExists = await db.Users.AnyAsync(u => u.Phone == phone, ct);
                if (phoneExists)
                    return (false, "A user with this phone number already exists.", null);
            }

            var now = DateTime.UtcNow;
            var uniqueToken = Guid.NewGuid().ToString("N");
            var placeholderEmail = $"nondigital.{uniqueToken[..12]}@agriflow.local";
            var placeholderHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            // Unique synthetic NID hash & UPI for non-digital field onboarding
            var syntheticNidRaw = $"ND-{uniqueToken}";
            var nidHash = SHA256.HashData(Encoding.UTF8.GetBytes(syntheticNidRaw));
            var nidEncrypted = RandomNumberGenerator.GetBytes(32);
            var syntheticUpi = $"ND/{district.ToUpperInvariant()[..Math.Min(3, district.Length)]}/{uniqueToken[..8].ToUpperInvariant()}";

            decimal? farmSizeHa = req.FarmSizeAcres is > 0
                ? Math.Round(req.FarmSizeAcres.Value * 0.404686m, 2)
                : null;

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var user = new Data.Entities.AppUser
            {
                FullName = fullName,
                Email = placeholderEmail,
                Phone = phone,
                PasswordHash = placeholderHash,
                Role = "Farmer",
                IsActive = true,
                CreatedAt = now
            };

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var profile = new Data.Entities.FarmerProfile
            {
                UserId = user.Id,
                FarmName = farmName,
                NationalIdEncrypted = nidEncrypted,
                NationalIdHash = nidHash,
                NationalIdMasked = "Non-digital (Agent Verified)",
                LandUpi = syntheticUpi,
                Province = province,
                District = district,
                Sector = village,
                Cell = village,
                Village = village,
                MainCrops = "Mixed crops",
                FarmSizeHa = farmSizeHa,
                PreferredLanguage = language,
                VerificationStatus = "Verified",
                VerifiedByUserId = agentUserId,
                VerifiedAt = now,
                RegisteredByAgentId = agentUserId,
                CreatedAt = now
            };

            db.FarmerProfiles.Add(profile);
            await db.SaveChangesAsync(ct);

            db.AgentFarmerAssignments.Add(new Data.Entities.AgentFarmerAssignment
            {
                AgentId = agentUserId,
                FarmerId = user.Id,
                AssignedByUserId = null,
                AssignedAt = now,
                ConsentRecordedAt = now,
                ConsentPlace = $"{village}, {district}"
            });

            db.AuditLogs.Add(new Data.Entities.AuditLog
            {
                ActorUserId = agentUserId,
                OnBehalfOfFarmerId = user.Id,
                Entity = "FarmerProfile",
                EntityId = user.Id.ToString(),
                Action = "RegisterNonDigitalFarmer",
                Summary = $"Registered non-digital farmer {fullName} ({farmName}, {district} · {village}) with in-person consent.",
                Result = "Success"
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;

            var response = new RegisterNonDigitalFarmerResponse(
                FarmerId: user.Id,
                FullName: fullName,
                FirstName: firstName,
                FarmName: farmName,
                Message: $"{fullName} has been registered and assigned to you."
            );

            return (true, response.Message, response);
        }
    }
}
