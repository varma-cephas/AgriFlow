using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record ReserveRequest(
    int ListingId, int UserId, int BuyerBusinessId, decimal Quantity, string DeliveryAddress);

public record ReserveResult(
    bool Success, string? ErrorMessage = null, string? OrderNumber = null, decimal Total = 0,
    string CropName = "", string Unit = "", string FarmName = "");

public class ReservationRepository
{
    private readonly DbConnectionFactory _db;

    public ReservationRepository(DbConnectionFactory db) => _db = db;

    // One transaction: lock listing -> validate -> create order + order_item (Reserved)
    // -> increase the listing's reserved total. No stock is deducted yet.
    public async Task<ReserveResult> ReserveAsync(ReserveRequest req)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        try
        {
            // Only verified businesses can reserve.
            string? buyerStatus;
            await using (var cmd = new SqlCommand(
                "SELECT VerificationStatus FROM buyer_business WHERE Id = @Id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.BuyerBusinessId);
                buyerStatus = await cmd.ExecuteScalarAsync() as string;
            }
            if (buyerStatus != "Verified")
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, "Your business must be verified by an AgriFlow admin before you can reserve.");
            }

            // Lock the listing row so two buyers can't push the reserved total past the estimate.
            const string lockSql = @"
                SELECT l.Status, l.EstimatedQty, l.ReservedQty, l.MinOrderQty, l.PricePerUnit, l.Unit,
                       l.OpenForReservation, fp.VerificationStatus, fp.FarmName, c.Name
                FROM listing l WITH (UPDLOCK, ROWLOCK)
                INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
                INNER JOIN crop c ON c.Id = l.CropId
                WHERE l.Id = @Id;";

            bool found;
            string status = "", unit = "", farmStatus = "", farmName = "", cropName = "";
            decimal estimated = 0, reserved = 0, minQty = 0, price = 0;
            bool open = false;

            await using (var cmd = new SqlCommand(lockSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.ListingId);
                await using var r = await cmd.ExecuteReaderAsync();
                found = await r.ReadAsync();
                if (found)
                {
                    status = r.GetString(0);
                    estimated = r.IsDBNull(1) ? 0m : r.GetDecimal(1);
                    reserved = r.GetDecimal(2);
                    minQty = r.GetDecimal(3);
                    price = r.GetDecimal(4);
                    unit = r.GetString(5);
                    open = r.GetBoolean(6);
                    farmStatus = r.GetString(7);
                    farmName = r.GetString(8);
                    cropName = r.GetString(9);
                }
            }

            if (!found || farmStatus != "Verified" || status != "Upcoming")
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, "This harvest is no longer open for reservations.");
            }
            if (!open)
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, "The farmer has closed reservations for this harvest.");
            }

            var remaining = estimated - reserved;
            if (remaining <= 0)
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, "This harvest is fully reserved.");
            }
            if (req.Quantity < minQty)
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, $"The minimum reservation is {Fmt(minQty)} {Plural(unit, minQty)}.");
            }
            if (req.Quantity > remaining)
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, $"Only {Fmt(remaining)} {Plural(unit, remaining)} can still be reserved. Please lower your quantity.");
            }

            // The price comes from the locked database row, never from the form.
            var total = Math.Round(req.Quantity * price, 2);

            // 1. Order with a temporary unique number, status Reserved. No delivery date yet.
            int orderId;
            await using (var cmd = new SqlCommand(@"
                INSERT INTO orders (OrderNumber, BuyerBusinessId, PlacedByUserId, DeliveryAddress, DeliveryDate, Status, TotalAmount)
                OUTPUT INSERTED.Id
                VALUES (LEFT(CONVERT(NVARCHAR(36), NEWID()), 20), @BuyerId, @UserId, @Address, NULL, 'Reserved', @Total);", conn, tx))
            {
                cmd.Parameters.AddWithValue("@BuyerId", req.BuyerBusinessId);
                cmd.Parameters.AddWithValue("@UserId", req.UserId);
                cmd.Parameters.AddWithValue("@Address", req.DeliveryAddress);
                cmd.Parameters.AddWithValue("@Total", total);
                orderId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 2. Real order number, e.g. AF-00008
            string orderNumber;
            await using (var cmd = new SqlCommand(@"
                UPDATE orders SET OrderNumber = N'AF-' + FORMAT(Id, '00000')
                OUTPUT INSERTED.OrderNumber
                WHERE Id = @Id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", orderId);
                orderNumber = (string)(await cmd.ExecuteScalarAsync())!;
            }

            // 3. Order line: status Reserved. ReservedAt is how reservations are filled oldest first.
            await using (var cmd = new SqlCommand(@"
                INSERT INTO order_item (OrderId, ListingId, QtyRequested, UnitPrice, Status, ReservedAt)
                VALUES (@OrderId, @ListingId, @Qty, @Price, 'Reserved', SYSUTCDATETIME());", conn, tx))
            {
                cmd.Parameters.AddWithValue("@OrderId", orderId);
                cmd.Parameters.AddWithValue("@ListingId", req.ListingId);
                cmd.Parameters.AddWithValue("@Qty", req.Quantity);
                cmd.Parameters.AddWithValue("@Price", price);
                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Listing's reserved total goes up. Stock is NOT deducted.
            int updated;
            await using (var cmd = new SqlCommand(@"
                UPDATE listing
                SET ReservedQty = ReservedQty + @Qty, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id AND Status = 'Upcoming'
                  AND ISNULL(EstimatedQty, 0) - ReservedQty >= @Qty;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.ListingId);
                cmd.Parameters.AddWithValue("@Qty", req.Quantity);
                updated = await cmd.ExecuteNonQueryAsync();
            }
            if (updated != 1)
            {
                await tx.RollbackAsync();
                return new ReserveResult(false, "Someone else just reserved part of this harvest. Please try again.");
            }

            // 5. Audit log
            await using (var cmd = new SqlCommand(@"
                INSERT INTO audit_log (ActorUserId, Entity, EntityId, Action, Summary, Result)
                VALUES (@ActorId, 'Order', @EntityId, 'Reserved supply', @Summary, 'Success');", conn, tx))
            {
                cmd.Parameters.AddWithValue("@ActorId", req.UserId);
                cmd.Parameters.AddWithValue("@EntityId", orderId.ToString());
                cmd.Parameters.AddWithValue("@Summary",
                    $"{orderNumber}: reserved {cropName} {Fmt(req.Quantity)} {Plural(unit, req.Quantity)} from {farmName}");
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return new ReserveResult(true, null, orderNumber, total, cropName, unit, farmName);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            // Deadlock victim: SQL Server already rolled the transaction back.
            return new ReserveResult(false, "The system was busy. Please try again.");
        }
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Plural(string unit, decimal qty) => qty == 1 ? unit : unit + "s";
}