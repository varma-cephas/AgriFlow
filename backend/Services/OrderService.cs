using System.Globalization;
using backend.Data;
using backend.Data.Entities;
using backend.Models.Orders;
using backend.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class OrderService(AppDbContext db) : IOrderService
{
    public async Task<PlaceOrderResult> PlaceOrderAsync(
        int userId, int businessId, PlaceOrderRequest req, CancellationToken ct = default)
    {
        // ---------- Check the request itself ----------
        var address = req.DeliveryAddress?.Trim() ?? "";
        if (address.Length == 0 || address.Length > 250)
            return Bad("Enter a delivery address of up to 250 characters.");

        if (req.Quantity <= 0 || decimal.Round(req.Quantity, 2) != req.Quantity)
            return Bad("The quantity must be more than zero, with at most 2 decimal places.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)); // Rwanda is UTC+2
        if (req.DeliveryDate < today)
            return Bad("The delivery date cannot be in the past.");

        // ---------- Rule 1: the buyer's business must be Verified (BR-6) ----------
        var businessStatus = await db.BuyerBusinesses.AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => b.VerificationStatus)
            .FirstOrDefaultAsync(ct);

        if (businessStatus != "Verified")
            return new PlaceOrderResult(ServiceOutcome.Forbidden,
                "Your business must be verified before you can place orders.");

        // ---------- Rule 2: the listing must be Available, and the quantity must fit ----------
        var listing = await db.Listings.AsNoTracking()
            .Where(l => l.Id == req.ListingId
                     && l.Farmer.VerificationStatus == "Verified"
                     && l.Status != "Hidden")
            .Select(l => new
            {
                l.Id,
                l.ListingCode,
                l.Status,
                l.PricePerUnit,
                l.Unit,
                l.MinOrderQty,
                l.AvailableQty,
                CropName = l.Crop.Name
            })
            .FirstOrDefaultAsync(ct);

        if (listing is null)
            return new PlaceOrderResult(ServiceOutcome.NotFound, "Listing not found.");

        if (listing.Status != "Available")
            return Bad("This listing is not available to order right now.");

        if (req.Quantity < listing.MinOrderQty)
            return Bad($"The minimum order is {Fmt(listing.MinOrderQty)} {listing.Unit}.");

        if (req.Quantity > listing.AvailableQty)
            return Bad($"Only {Fmt(listing.AvailableQty)} {listing.Unit} are available.");

        // ---------- Rules 3 to 5: one transaction ----------
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Rule 4 first: take the units in one atomic statement (BR-9).
            // The price always comes from the database row, never from the request.
            var qty = req.Quantity;
            var id = listing.Id;
            var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE listing
                SET AvailableQty = AvailableQty - {qty},
                    Status = CASE WHEN AvailableQty - {qty} = 0 THEN 'SoldOut' ELSE Status END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = {id} AND Status = 'Available' AND AvailableQty >= {qty};", ct);

            if (rows == 0)
            {
                await tx.RollbackAsync(ct);
                return new PlaceOrderResult(ServiceOutcome.Conflict,
                    "Someone just bought the last units. Please refresh and try again.");
            }

            // Rule 3: the order and its line
            var total = Math.Round(qty * listing.PricePerUnit, 2);
            var order = new Order
            {
                OrderNumber = "TMP-" + Guid.NewGuid().ToString("N")[..12],   // replaced just below
                BuyerBusinessId = businessId,
                PlacedByUserId = userId,
                DeliveryAddress = address,
                DeliveryDate = req.DeliveryDate,
                Status = "Placed",
                TotalAmount = total
            };
            order.Items.Add(new OrderItem
            {
                ListingId = listing.Id,
                QtyRequested = qty,
                UnitPrice = listing.PricePerUnit,
                Status = "Placed"
            });

            db.Orders.Add(order);
            await db.SaveChangesAsync(ct);

            order.OrderNumber = $"AF-{order.Id:00000}";

            // Rule 5: audit log, inside the same transaction
            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = userId,
                Entity = "Order",
                EntityId = order.Id.ToString(),
                Action = "Placed order",
                Summary = $"{order.OrderNumber}: {Fmt(qty)} {listing.Unit} of {listing.CropName} ({listing.ListingCode})",
                Result = "Success"
            });
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new PlaceOrderResult(ServiceOutcome.Ok, "Order placed.", new PlaceOrderResponse(
                order.OrderNumber, listing.Id, listing.ListingCode, listing.CropName, qty,
                listing.Unit, listing.PricePerUnit, total, req.DeliveryDate, "Placed"));
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // SQL Server already rolled the transaction back.
            return new PlaceOrderResult(ServiceOutcome.Conflict, "The system was busy. Please try again.");
        }
    }

    private static PlaceOrderResult Bad(string message) => new(ServiceOutcome.BadRequest, message);

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool IsDeadlock(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 1205 }) return true;
        return false;
    }
}