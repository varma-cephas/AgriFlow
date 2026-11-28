using System.Globalization;
using backend.Data;
using backend.Data.Entities;
using backend.Models.Orders;
using backend.Models.Reservations;
using backend.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ReservationService(AppDbContext db) : IReservationService
{
    public async Task<ReserveResult> ReserveAsync(
        int userId, int businessId, ReserveRequest req, CancellationToken ct = default)
    {
        // ---------- Check the request itself ----------
        var address = req.DeliveryAddress?.Trim() ?? "";
        if (address.Length == 0 || address.Length > 250)
            return Bad("Enter a delivery address of up to 250 characters.");

        if (req.Quantity <= 0 || decimal.Round(req.Quantity, 2) != req.Quantity)
            return Bad("The quantity must be more than zero, with at most 2 decimal places.");

        // ---------- Rule 1: the buyer's business must be Verified (BR-6) ----------
        var businessStatus = await db.BuyerBusinesses.AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => b.VerificationStatus)
            .FirstOrDefaultAsync(ct);

        if (businessStatus != "Verified")
            return new ReserveResult(ServiceOutcome.Forbidden,
                "Your business must be verified before you can reserve supply.");

        // ---------- Rule 2: the listing must be Upcoming and open for reservations ----------
        var listing = await db.Listings.AsNoTracking()
            .Where(l => l.Id == req.ListingId
                     && l.Farmer.VerificationStatus == "Verified"
                     && l.Status != "Hidden")
            .Select(l => new
            {
                l.Id,
                l.ListingCode,
                l.Status,
                l.OpenForReservation,
                l.PricePerUnit,
                l.Unit,
                l.MinOrderQty,
                l.EstimatedQty,
                l.ReservedQty,
                CropName = l.Crop.Name,
                ExpectedHarvest = l.Batch.PredictedHarvestDate
            })
            .FirstOrDefaultAsync(ct);

        if (listing is null)
            return new ReserveResult(ServiceOutcome.NotFound, "Listing not found.");

        if (listing.Status != "Upcoming")
            return Bad("Only upcoming harvests can be reserved. This listing is not an upcoming harvest.");

        if (!listing.OpenForReservation)
            return Bad("Reservations are closed for this harvest.");

        // ---------- Rule 3: minimum order (BR-7) and what is still unreserved (BR-8) ----------
        var remaining = (listing.EstimatedQty ?? 0) - listing.ReservedQty;
        if (remaining <= 0)
            return Bad("This harvest is fully reserved.");

        if (req.Quantity < listing.MinOrderQty)
            return Bad($"The minimum order is {Fmt(listing.MinOrderQty)} {listing.Unit}.");

        if (req.Quantity > remaining)
            return Bad($"Only {Fmt(remaining)} {listing.Unit} are still unreserved.");

        // ---------- Rules 4 to 6: one transaction ----------
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Rule 5 first: take the supply in one atomic statement.
            var qty = req.Quantity;
            var id = listing.Id;
            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE listing
                SET ReservedQty = ReservedQty + {qty},
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = {id}
                  AND Status = 'Upcoming'
                  AND OpenForReservation = 1
                  AND ReservedQty + {qty} <= ISNULL(EstimatedQty, 0);", ct);

            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return new ReserveResult(ServiceOutcome.Conflict,
                    "Someone just reserved the last of this supply. Please refresh and try again.");
            }

            // Rule 4: the order and its line, both 'Reserved'. The price comes from the database row.
            var estimatedValue = Math.Round(qty * listing.PricePerUnit, 2);
            var order = new Order
            {
                OrderNumber = "TMP-" + Guid.NewGuid().ToString("N")[..12],   // replaced just below
                BuyerBusinessId = businessId,
                PlacedByUserId = userId,
                DeliveryAddress = address,
                DeliveryDate = null,               // set later, when the harvest is settled
                Status = "Reserved",
                TotalAmount = estimatedValue
            };
            order.Items.Add(new OrderItem
            {
                ListingId = listing.Id,
                QtyRequested = qty,
                UnitPrice = listing.PricePerUnit,
                Status = "Reserved",
                ReservedAt = DateTime.UtcNow       // reservations are filled oldest first at harvest
            });

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            order.OrderNumber = $"AF-{order.Id:00000}";

            // Rule 6: audit log, inside the same transaction
            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                Entity = "Order",
                EntityId = order.Id.ToString(),
                Action = "Reserved upcoming supply",
                Summary = $"{order.OrderNumber}: {Fmt(qty)} {listing.Unit} of {listing.CropName} ({listing.ListingCode})",
                Result = "Success"
            });
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new ReserveResult(ServiceOutcome.Ok, "Reserved.", new ReserveResponse(
                order.OrderNumber, listing.Id, listing.ListingCode, listing.CropName, qty, listing.Unit,
                listing.PricePerUnit, estimatedValue, listing.ExpectedHarvest, "Reserved"));
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // SQL Server already rolled the transaction back.
            return new ReserveResult(ServiceOutcome.Conflict, "The system was busy. Please try again.");
        }
    }

    private static ReserveResult Bad(string message) => new(ServiceOutcome.BadRequest, message);

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool IsDeadlock(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 1205 }) return true;
        return false;
    }
}