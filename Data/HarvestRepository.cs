using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record HarvestBatch(
    int BatchId, string BatchCode, int FarmerId, string Status, string CropName,
    DateTime PlantingDate, DateTime PredictedHarvestDate, DateTime? HarvestDate,
    decimal? ActualYield, string? YieldUnit,
    int? ListingId, string? ListingCode, string? Grade, string? Unit, decimal? PricePerUnit);

public record HarvestReservation(
    int ItemId, string OrderNumber, string BuyerName,
    decimal QtyRequested, decimal? QtyConfirmed, string Status, DateTime ReservedAt);

public record HarvestView(HarvestBatch Batch, List<HarvestReservation> Reservations);

public record RecordHarvestRequest(
    int BatchId, int FarmerId, decimal ActualYield, DateTime HarvestDate, string Unit, string Grade);

public record RecordHarvestResult(
    bool Success, string? ErrorMessage = null, decimal Yield = 0, decimal Reserved = 0,
    decimal Leftover = 0, int Full = 0, int Partial = 0, int Cancelled = 0, string Unit = "");

public class HarvestRepository
{
    private readonly DbConnectionFactory _db;

    public HarvestRepository(DbConnectionFactory db) => _db = db;

    // The batch, its listing, and every reservation made on that listing (oldest first).
    public async Task<HarvestView?> GetHarvestViewAsync(int batchId)
    {
        const string batchSql = @"
            SELECT pb.Id, pb.BatchCode, pb.FarmerId, pb.Status, c.Name,
                   pb.PlantingDate, pb.PredictedHarvestDate, pb.HarvestDate, pb.ActualYield, pb.YieldUnit,
                   l.Id, l.ListingCode, l.Grade, l.Unit, l.PricePerUnit
            FROM planting_batch pb
            INNER JOIN seed_stock ss ON ss.Id = pb.SeedStockId
            INNER JOIN crop c ON c.Id = ss.CropId
            LEFT JOIN listing l ON l.BatchId = pb.Id
            WHERE pb.Id = @Id;";

        const string reservationsSql = @"
            SELECT oi.Id, o.OrderNumber, bb.Name, oi.QtyRequested, oi.QtyConfirmed, oi.Status, oi.ReservedAt
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN buyer_business bb ON bb.Id = o.BuyerBusinessId
            WHERE oi.ListingId = @ListingId AND oi.ReservedAt IS NOT NULL
            ORDER BY oi.ReservedAt, oi.Id;";

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        HarvestBatch batch;
        await using (var cmd = new SqlCommand(batchSql, conn))
        {
            cmd.Parameters.AddWithValue("@Id", batchId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            batch = new HarvestBatch(
                r.GetInt32(0),
                r.GetString(1),
                r.GetInt32(2),
                r.GetString(3),
                r.GetString(4),
                r.GetDateTime(5),
                r.GetDateTime(6),
                r.IsDBNull(7) ? null : r.GetDateTime(7),
                r.IsDBNull(8) ? null : r.GetDecimal(8),
                r.IsDBNull(9) ? null : r.GetString(9),
                r.IsDBNull(10) ? null : r.GetInt32(10),
                r.IsDBNull(11) ? null : r.GetString(11),
                r.IsDBNull(12) ? null : r.GetString(12),
                r.IsDBNull(13) ? null : r.GetString(13),
                r.IsDBNull(14) ? null : r.GetDecimal(14));
        }

        var reservations = new List<HarvestReservation>();
        if (batch.ListingId.HasValue)
        {
            await using var cmd = new SqlCommand(reservationsSql, conn);
            cmd.Parameters.AddWithValue("@ListingId", batch.ListingId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                reservations.Add(new HarvestReservation(
                    r.GetInt32(0),
                    r.GetString(1),
                    r.GetString(2),
                    r.GetDecimal(3),
                    r.IsDBNull(4) ? null : r.GetDecimal(4),
                    r.GetString(5),
                    r.GetDateTime(6)));
            }
        }

        return new HarvestView(batch, reservations);
    }

    // One transaction: batch -> Harvested, listing -> Available (or SoldOut if nothing is left),
    // reservations settled oldest first.
    public async Task<RecordHarvestResult> RecordHarvestAsync(RecordHarvestRequest req)
    {
        if (req.ActualYield <= 0)
            return new RecordHarvestResult(false, "The harvest amount must be more than zero.");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

        async Task<RecordHarvestResult> Fail(string message)
        {
            await tx.RollbackAsync();
            return new RecordHarvestResult(false, message);
        }

        try
        {
            // Lock the batch and its listing, so no new reservation can slip in while we settle.
            const string lockSql = @"
                SELECT pb.Status, pb.FarmerId, pb.PlantingDate, l.Id, l.Status, l.Unit, c.Name, pb.BatchCode
                FROM planting_batch pb WITH (UPDLOCK, ROWLOCK)
                INNER JOIN seed_stock ss ON ss.Id = pb.SeedStockId
                INNER JOIN crop c ON c.Id = ss.CropId
                LEFT JOIN listing l WITH (UPDLOCK, ROWLOCK) ON l.BatchId = pb.Id
                WHERE pb.Id = @Id;";

            bool found, hasListing = false;
            string batchStatus = "", batchCode = "", cropName = "", listingStatus = "", listingUnit = "";
            int farmerId = 0, listingId = 0;
            DateTime planted = DateTime.MinValue;

            await using (var cmd = new SqlCommand(lockSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", req.BatchId);
                await using var r = await cmd.ExecuteReaderAsync();
                found = await r.ReadAsync();
                if (found)
                {
                    batchStatus = r.GetString(0);
                    farmerId = r.GetInt32(1);
                    planted = r.GetDateTime(2);
                    hasListing = !r.IsDBNull(3);
                    if (hasListing)
                    {
                        listingId = r.GetInt32(3);
                        listingStatus = r.GetString(4);
                        listingUnit = r.GetString(5);
                    }
                    cropName = r.GetString(6);
                    batchCode = r.GetString(7);
                }
            }

            // Agents only work for their own farmers; a farmer only for their own batches.
            if (!found || farmerId != req.FarmerId)
                return await Fail("This planting batch could not be found.");
            if (batchStatus != "Active")
                return await Fail("A harvest has already been recorded for this batch.");
            if (!hasListing || listingStatus != "Upcoming")
                return await Fail("This batch has no open listing to settle.");
            if (req.HarvestDate.Date < planted.Date)
                return await Fail("The harvest date cannot be before the planting date (" +
                                  planted.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + ").");

            // Reservations on this listing, oldest first.
            var reservations = new List<(int ItemId, int OrderId, decimal Qty, decimal Price, string OrderNumber)>();
            await using (var cmd = new SqlCommand(@"
                SELECT oi.Id, oi.OrderId, oi.QtyRequested, oi.UnitPrice, o.OrderNumber
                FROM order_item oi WITH (UPDLOCK, ROWLOCK)
                INNER JOIN orders o ON o.Id = oi.OrderId
                WHERE oi.ListingId = @ListingId AND oi.Status = 'Reserved'
                ORDER BY oi.ReservedAt, oi.Id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@ListingId", listingId);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    reservations.Add((r.GetInt32(0), r.GetInt32(1), r.GetDecimal(2), r.GetDecimal(3), r.GetString(4)));
                }
            }

            // Buyers reserved in the listing's unit, so the unit can only change when nobody reserved.
            if (reservations.Count > 0 && req.Unit != listingUnit)
                return await Fail($"The selling unit cannot change because buyers already reserved in {listingUnit}.");

            var totalReserved = reservations.Sum(x => x.Qty);
            var remaining = req.ActualYield;
            int full = 0, partial = 0, cancelled = 0;

            foreach (var res in reservations)
            {
                var filled = Math.Min(remaining, res.Qty);
                remaining -= filled;

                string action, summary;
                if (filled == res.Qty)
                {
                    full++;
                    await ExecAsync(conn, tx, "UPDATE order_item SET Status = 'Placed' WHERE Id = @Id;",
                        ("@Id", res.ItemId));
                    await ExecAsync(conn, tx,
                        "UPDATE orders SET Status = 'Placed', UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id;",
                        ("@Id", res.OrderId));
                    action = "Reservation filled in full";
                    summary = $"{res.OrderNumber}: {Fmt(res.Qty)} {Plural(req.Unit, res.Qty)} of {cropName}";
                }
                else if (filled > 0)
                {
                    partial++;
                    var newTotal = Math.Round(filled * res.Price, 2);
                    await ExecAsync(conn, tx,
                        "UPDATE order_item SET Status = 'Placed', QtyConfirmed = @Filled WHERE Id = @Id;",
                        ("@Filled", filled), ("@Id", res.ItemId));
                    await ExecAsync(conn, tx,
                        "UPDATE orders SET Status = 'Placed', TotalAmount = @Total, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id;",
                        ("@Total", newTotal), ("@Id", res.OrderId));
                    action = "Reservation partly filled";
                    summary = $"{res.OrderNumber}: {Fmt(filled)} of {Fmt(res.Qty)} {Plural(req.Unit, res.Qty)} of {cropName}";
                }
                else
                {
                    cancelled++;
                    var reason = $"Harvest shortfall: only {Fmt(req.ActualYield)} {Plural(req.Unit, req.ActualYield)} " +
                                 $"were harvested against {Fmt(totalReserved)} reserved, and reservations are filled oldest first.";
                    await ExecAsync(conn, tx,
                        "UPDATE order_item SET Status = 'Cancelled', CancelType = 'Shortfall' WHERE Id = @Id;",
                        ("@Id", res.ItemId));
                    await ExecAsync(conn, tx,
                        "UPDATE orders SET Status = 'Cancelled', CancelledByUserId = @Farmer, CancelReason = @Reason, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id;",
                        ("@Farmer", req.FarmerId), ("@Reason", reason), ("@Id", res.OrderId));
                    action = "Reservation cancelled (shortfall)";
                    summary = $"{res.OrderNumber}: {Fmt(res.Qty)} {Plural(req.Unit, res.Qty)} of {cropName}";
                }

                await InsertAuditAsync(conn, tx, req.FarmerId, "Order", res.OrderId.ToString(), action, summary);
            }

            var leftover = remaining;

            await ExecAsync(conn, tx, @"
                UPDATE planting_batch
                SET Status = 'Harvested', ActualYield = @Yield, YieldUnit = @Unit, HarvestDate = @Date
                WHERE Id = @Id;",
                ("@Yield", req.ActualYield), ("@Unit", req.Unit), ("@Date", req.HarvestDate.Date), ("@Id", req.BatchId));

            await ExecAsync(conn, tx, @"
                UPDATE listing
                SET Status = CASE WHEN @Leftover > 0 THEN 'Available' ELSE 'SoldOut' END,
                    AvailableQty = @Leftover,
                    ReservedQty = 0,
                    Grade = @Grade,
                    UnitSizeKg = CASE WHEN @Unit = Unit THEN UnitSizeKg WHEN @Unit = 'tonne' THEN 1000 ELSE NULL END,
                    Unit = @Unit,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @Id;",
                ("@Leftover", leftover), ("@Grade", req.Grade), ("@Unit", req.Unit), ("@Id", listingId));

            await InsertAuditAsync(conn, tx, req.FarmerId, "PlantingBatch", req.BatchId.ToString(), "Recorded harvest",
                $"{batchCode}: {cropName} {Fmt(req.ActualYield)} {Plural(req.Unit, req.ActualYield)} harvested, " +
                $"{Fmt(totalReserved)} reserved, {Fmt(leftover)} on sale");

            await tx.CommitAsync();
            return new RecordHarvestResult(true, null, req.ActualYield, totalReserved, leftover, full, partial, cancelled, req.Unit);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            // Deadlock victim: SQL Server already rolled the transaction back.
            return new RecordHarvestResult(false, "The system was busy. Please try again.");
        }
    }

    private static async Task ExecAsync(SqlConnection conn, SqlTransaction tx, string sql,
        params (string Name, object Value)[] args)
    {
        await using var cmd = new SqlCommand(sql, conn, tx);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Task InsertAuditAsync(SqlConnection conn, SqlTransaction tx, int actorId,
        string entity, string entityId, string action, string summary) =>
        ExecAsync(conn, tx, @"
            INSERT INTO audit_log (ActorUserId, Entity, EntityId, Action, Summary, Result)
            VALUES (@ActorId, @Entity, @EntityId, @Action, @Summary, 'Success');",
            ("@ActorId", actorId), ("@Entity", entity), ("@EntityId", entityId),
            ("@Action", action), ("@Summary", summary));

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Plural(string unit, decimal qty) => qty == 1 ? unit : unit + "s";
}