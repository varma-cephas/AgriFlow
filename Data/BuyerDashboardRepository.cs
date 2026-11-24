using System.Globalization;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record DashBusiness(string Name, string City, string Status);

public record DashKpis(int OpenOrders, decimal ReservedTonnes, decimal SpendThisMonth,
                       int UnpaidCount, decimal UnpaidAmount);

public record DashWeek(DateTime WeekStart, decimal ConfirmedTonnes, decimal ReservedTonnes);

public record DashUpcoming(int ListingId, string ListingCode, string CropName, string FarmName,
                           string District, DateTime ExpectedDate, decimal Remaining, string Unit);

public record DashDelivery(string OrderNumber, string CropName, string FarmName, string Status);

public record DashInvoice(string InvoiceNumber, string OrderNumber, decimal Amount, DateTime DueDate);

public record DashPartial(string OrderNumber, string CropName, decimal Confirmed, decimal Requested, string Unit);

public record DashSupply(string ListingCode, string CropName, string FarmName, string District,
                         string Status, bool Nearby);

public record DashOrder(string OrderNumber, string CropName, string FarmName, decimal Qty, string Unit,
                        decimal Total, string Status, DateTime CreatedAt);

public record BuyerDashboardData(
    DashBusiness Business, DashKpis Kpis, List<DashWeek> Weeks, List<DashUpcoming> Upcoming,
    List<DashDelivery> Deliveries, List<DashInvoice> InvoicesDue, List<DashPartial> PartlyFilled,
    List<DashSupply> NewSupply, List<DashOrder> RecentOrders);

public class BuyerDashboardRepository
{
    private const string TonnesExpr =
        "COALESCE(l.UnitSizeKg, CASE WHEN l.Unit = 'tonne' THEN 1000 END)";

    private readonly DbConnectionFactory _db;

    public BuyerDashboardRepository(DbConnectionFactory db) => _db = db;

    public async Task<BuyerDashboardData?> GetAsync(int businessId, DateTime today)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        var todayText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var monthText = new DateTime(today.Year, today.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var business = (await QueryAsync(conn,
            "SELECT Name, City, VerificationStatus FROM buyer_business WHERE Id = @Id;",
            r => new DashBusiness(r.GetString(0), r.GetString(1), r.GetString(2)),
            ("@Id", businessId))).FirstOrDefault();
        if (business is null) return null;

        // ---------- KPIs ----------
        var openOrders = (await QueryAsync(conn, @"
            SELECT COUNT(*) FROM orders
            WHERE BuyerBusinessId = @Id AND Status IN ('Placed','Confirmed','Dispatched');",
            r => r.GetInt32(0), ("@Id", businessId))).First();

        var reservedTonnes = (await QueryAsync(conn, $@"
            SELECT ISNULL(SUM(oi.QtyRequested * ISNULL({TonnesExpr}, 0)), 0) / 1000.0
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN listing l ON l.Id = oi.ListingId
            WHERE o.BuyerBusinessId = @Id AND oi.Status = 'Reserved';",
            r => Convert.ToDecimal(r.GetValue(0), CultureInfo.InvariantCulture),
            ("@Id", businessId))).First();

        var spend = (await QueryAsync(conn, @"
            SELECT ISNULL(SUM(TotalAmount), 0) FROM orders
            WHERE BuyerBusinessId = @Id AND Status NOT IN ('Reserved','Cancelled')
              AND CreatedAt >= CAST(@MonthStart AS date);",
            r => Convert.ToDecimal(r.GetValue(0), CultureInfo.InvariantCulture),
            ("@Id", businessId), ("@MonthStart", monthText))).First();

        var unpaid = (await QueryAsync(conn, @"
            SELECT COUNT(*), ISNULL(SUM(i.Amount), 0)
            FROM invoice i INNER JOIN orders o ON o.Id = i.OrderId
            WHERE o.BuyerBusinessId = @Id AND i.Status = 'Unpaid';",
            r => (Count: r.GetInt32(0), Amount: Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture)),
            ("@Id", businessId))).First();

        var kpis = new DashKpis(openOrders, reservedTonnes, spend, unpaid.Count, unpaid.Amount);

        // ---------- Chart: next 6 weeks ----------
        var confirmedRows = await QueryAsync(conn, $@"
            SELECT o.DeliveryDate,
                   ISNULL(COALESCE(oi.QtyConfirmed, oi.QtyRequested) * {TonnesExpr} / 1000.0, 0)
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN listing l ON l.Id = oi.ListingId
            WHERE o.BuyerBusinessId = @Id
              AND o.Status IN ('Placed','Confirmed','Dispatched')
              AND oi.Status IN ('Placed','Confirmed','Dispatched')
              AND o.DeliveryDate >= CAST(@Today AS date)
              AND o.DeliveryDate < DATEADD(DAY, 42, CAST(@Today AS date));",
            r => (Date: r.GetDateTime(0), Tonnes: Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture)),
            ("@Id", businessId), ("@Today", todayText));

        var reservedRows = await QueryAsync(conn, $@"
            SELECT pb.PredictedHarvestDate,
                   ISNULL(oi.QtyRequested * {TonnesExpr} / 1000.0, 0)
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN listing l ON l.Id = oi.ListingId
            INNER JOIN planting_batch pb ON pb.Id = l.BatchId
            WHERE o.BuyerBusinessId = @Id AND oi.Status = 'Reserved'
              AND pb.PredictedHarvestDate < DATEADD(DAY, 42, CAST(@Today AS date));",
            r => (Date: r.GetDateTime(0), Tonnes: Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture)),
            ("@Id", businessId), ("@Today", todayText));

        var confirmed = new decimal[6];
        var reserved = new decimal[6];
        foreach (var row in confirmedRows)
        {
            var idx = Math.Max(0, (row.Date.Date - today.Date).Days / 7);
            if (idx <= 5) confirmed[idx] += row.Tonnes;
        }
        foreach (var row in reservedRows)
        {
            // A harvest date already in the past is shown in the first week.
            var idx = Math.Max(0, (row.Date.Date - today.Date).Days / 7);
            if (idx <= 5) reserved[idx] += row.Tonnes;
        }
        var weeks = Enumerable.Range(0, 6)
            .Select(i => new DashWeek(today.Date.AddDays(i * 7), confirmed[i], reserved[i]))
            .ToList();

        // ---------- Upcoming harvests to reserve ----------
        var upcoming = await QueryAsync(conn, @"
            SELECT TOP 3 l.Id, l.ListingCode, c.Name, fp.FarmName, fp.District, pb.PredictedHarvestDate,
                   ISNULL(l.EstimatedQty, 0) - l.ReservedQty, l.Unit
            FROM listing l
            INNER JOIN crop c ON c.Id = l.CropId
            INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
            INNER JOIN planting_batch pb ON pb.Id = l.BatchId
            WHERE l.Status = 'Upcoming' AND l.OpenForReservation = 1
              AND fp.VerificationStatus = 'Verified'
              AND ISNULL(l.EstimatedQty, 0) - l.ReservedQty > 0
            ORDER BY pb.PredictedHarvestDate, l.Id;",
            r => new DashUpcoming(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                                  r.GetString(4), r.GetDateTime(5), r.GetDecimal(6), r.GetString(7)));

        // ---------- Needs your attention ----------
        var deliveries = await QueryAsync(conn, @"
            SELECT TOP 5 o.OrderNumber, c.Name, fp.FarmName, oi.Status
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN listing l ON l.Id = oi.ListingId
            INNER JOIN crop c ON c.Id = l.CropId
            INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
            WHERE o.BuyerBusinessId = @Id AND oi.Status IN ('Dispatched','Delivered') AND oi.QtyAccepted IS NULL
            ORDER BY o.UpdatedAt DESC;",
            r => new DashDelivery(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)),
            ("@Id", businessId));

        var invoicesDue = await QueryAsync(conn, @"
            SELECT TOP 5 i.InvoiceNumber, o.OrderNumber, i.Amount, i.DueDate
            FROM invoice i INNER JOIN orders o ON o.Id = i.OrderId
            WHERE o.BuyerBusinessId = @Id AND i.Status = 'Unpaid'
              AND i.DueDate <= DATEADD(DAY, 7, CAST(@Today AS date))
            ORDER BY i.DueDate;",
            r => new DashInvoice(r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetDateTime(3)),
            ("@Id", businessId), ("@Today", todayText));

        var partlyFilled = await QueryAsync(conn, @"
            SELECT TOP 5 o.OrderNumber, c.Name, oi.QtyConfirmed, oi.QtyRequested, l.Unit
            FROM order_item oi
            INNER JOIN orders o ON o.Id = oi.OrderId
            INNER JOIN listing l ON l.Id = oi.ListingId
            INNER JOIN crop c ON c.Id = l.CropId
            WHERE o.BuyerBusinessId = @Id
              AND oi.QtyConfirmed IS NOT NULL AND oi.QtyConfirmed < oi.QtyRequested
              AND oi.Status IN ('Placed','Confirmed')
              AND o.UpdatedAt >= DATEADD(DAY, -30, SYSUTCDATETIME())
            ORDER BY o.UpdatedAt DESC;",
            r => new DashPartial(r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3), r.GetString(4)),
            ("@Id", businessId));

        var newSupply = await QueryAsync(conn, @"
            SELECT TOP 4 l.ListingCode, c.Name, fp.FarmName, fp.District, l.Status,
                   CASE WHEN fp.District = bb.City THEN 1 ELSE 0 END
            FROM listing l
            INNER JOIN crop c ON c.Id = l.CropId
            INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
            INNER JOIN buyer_business bb ON bb.Id = @Id
            WHERE l.Status IN ('Available','Upcoming') AND fp.VerificationStatus = 'Verified'
              AND (l.Status = 'Upcoming' OR l.AvailableQty > 0)
              AND l.CreatedAt >= DATEADD(DAY, -7, SYSUTCDATETIME())
            ORDER BY CASE WHEN fp.District = bb.City THEN 1 ELSE 0 END DESC, l.CreatedAt DESC;",
            r => new DashSupply(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
                                r.GetString(4), r.GetInt32(5) == 1),
            ("@Id", businessId));

        // ---------- Recent orders ----------
        var recent = await QueryAsync(conn, @"
            SELECT TOP 8 o.OrderNumber, c.Name, fp.FarmName, COALESCE(oi.QtyConfirmed, oi.QtyRequested),
                   l.Unit, o.TotalAmount, o.Status, o.CreatedAt
            FROM orders o
            INNER JOIN order_item oi ON oi.OrderId = o.Id
            INNER JOIN listing l ON l.Id = oi.ListingId
            INNER JOIN crop c ON c.Id = l.CropId
            INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
            WHERE o.BuyerBusinessId = @Id
            ORDER BY o.CreatedAt DESC, o.Id DESC;",
            r => new DashOrder(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3),
                               r.GetString(4), r.GetDecimal(5), r.GetString(6), r.GetDateTime(7)),
            ("@Id", businessId));

        return new BuyerDashboardData(business, kpis, weeks, upcoming, deliveries, invoicesDue,
                                      partlyFilled, newSupply, recent);
    }

    private static async Task<List<T>> QueryAsync<T>(SqlConnection conn, string sql,
        Func<SqlDataReader, T> map, params (string Name, object Value)[] args)
    {
        await using var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);

        var list = new List<T>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(map(r));
        return list;
    }
}