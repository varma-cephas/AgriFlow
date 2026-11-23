using System.Data;
using System.Globalization;
using System.Security.Claims;
using AgriFlow.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages;

public class IndexModel : PageModel
{
    private readonly DbConnectionFactory _connectionFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(DbConnectionFactory connectionFactory, ILogger<IndexModel> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public bool IsFarmer => User.IsInRole("Farmer");
    public string UserRole => User.FindFirstValue(ClaimTypes.Role) ?? "User";
    public FarmerDashboardData Dashboard { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsFarmer)
        {
            return;
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var farmerId))
        {
            Dashboard.ErrorMessage = "We couldn't load your farm dashboard. Please sign in again.";
            return;
        }

        try
        {
            Dashboard = await LoadDashboardAsync(farmerId, cancellationToken);
        }
        catch (SqlException exception)
        {
            _logger.LogWarning(exception, "Unable to load dashboard data for farmer {FarmerId}.", farmerId);
            Dashboard.ErrorMessage = "Some dashboard data is temporarily unavailable. Please try again later.";
        }
    }

    private async Task<FarmerDashboardData> LoadDashboardAsync(int farmerId, CancellationToken cancellationToken)
    {
        var dashboard = new FarmerDashboardData();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        dashboard.SupplyWeeks = Enumerable.Range(0, 8)
            .Select(index => new SupplyWeek
            {
                StartDate = weekStart.AddDays(index * 7),
                Label = weekStart.AddDays(index * 7).ToString("dd MMM", CultureInfo.InvariantCulture)
            })
            .ToList();

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var tableCommand = new SqlCommand(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = N'dbo' AND TABLE_TYPE = N'BASE TABLE';",
            connection))
        await using (var tableReader = await tableCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await tableReader.ReadAsync(cancellationToken))
            {
                tables.Add(tableReader.GetString(0));
            }
        }

        if (tables.Contains("farmer_profile"))
        {
            await LoadFarmProfileAsync(connection, farmerId, dashboard, cancellationToken);
        }

        if (tables.Contains("planting_batch"))
        {
            await LoadBatchSummaryAsync(connection, farmerId, dashboard, cancellationToken);
        }

        if (tables.Contains("seed_stock") && tables.Contains("crop"))
        {
            await LoadLowSeedStockAsync(connection, farmerId, dashboard, cancellationToken);
        }

        if (tables.Contains("listing"))
        {
            await LoadListingSummaryAsync(connection, farmerId, dashboard, cancellationToken);

            if (tables.Contains("planting_batch") && tables.Contains("seed_stock") && tables.Contains("crop"))
            {
                await LoadSupplyPipelineAsync(connection, farmerId, weekStart, dashboard, cancellationToken);
                await LoadUpcomingHarvestsAsync(connection, farmerId, dashboard, cancellationToken);
            }

            if (tables.Contains("order_item"))
            {
                await LoadOrderSummaryAsync(connection, farmerId, dashboard, cancellationToken);

                if (tables.Contains("orders") && tables.Contains("buyer_business") && tables.Contains("crop"))
                {
                    await LoadPendingOrdersAsync(connection, farmerId, dashboard, cancellationToken);
                }

                if (tables.Contains("invoice") && tables.Contains("orders"))
                {
                    await LoadSalesAsync(connection, farmerId, today, dashboard, cancellationToken);
                }
            }
        }

        dashboard.LowStockCropNames = dashboard.LowStockCropNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return dashboard;
    }

    private static async Task LoadFarmProfileAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) FarmName, District, Province, VerificationStatus
            FROM dbo.farmer_profile
            WHERE UserId = @FarmerId;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            dashboard.FarmProfileLoaded = true;
            dashboard.FarmName = reader.GetString(0);
            dashboard.District = reader.GetString(1);
            dashboard.Province = reader.GetString(2);
            dashboard.VerificationStatus = reader.GetString(3);
        }
    }

    private static async Task LoadBatchSummaryAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*) AS ActiveCount,
                SUM(CASE WHEN CreatedAt >= @MonthStart AND CreatedAt < @NextMonth THEN 1 ELSE 0 END) AS AddedThisMonth,
                COUNT(DISTINCT NULLIF(LTRIM(RTRIM(FieldName)), N'')) AS FieldCount,
                SUM(CASE WHEN PredictedHarvestDate >= @Today AND PredictedHarvestDate < @AfterFourteenDays THEN 1 ELSE 0 END) AS ReadyWithinFourteenDays,
                SUM(CASE WHEN PredictedHarvestDate <= @Today THEN 1 ELSE 0 END) AS ReadyNow
            FROM dbo.planting_batch
            WHERE FarmerId = @FarmerId AND Status = N'Active';
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        command.Parameters.Add("@MonthStart", SqlDbType.DateTime2).Value = monthStart;
        command.Parameters.Add("@NextMonth", SqlDbType.DateTime2).Value = monthStart.AddMonths(1);
        command.Parameters.Add("@Today", SqlDbType.Date).Value = DateTime.Today;
        command.Parameters.Add("@AfterFourteenDays", SqlDbType.Date).Value = DateTime.Today.AddDays(15);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            dashboard.ActiveBatches = reader.GetInt32(0);
            dashboard.BatchesAddedThisMonth = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            dashboard.FieldCount = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
            dashboard.ReadyWithinFourteenDays = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
            dashboard.ReadyForHarvest = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
        }
    }

    private static async Task LoadLowSeedStockAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Name
            FROM dbo.seed_stock AS ss
            INNER JOIN dbo.crop AS c ON c.Id = ss.CropId
            WHERE ss.FarmerId = @FarmerId AND ss.Quantity <= 10
            ORDER BY c.Name;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dashboard.LowStockCount++;
            dashboard.LowStockCropNames.Add(reader.GetString(0));
        }
    }

    private static async Task LoadListingSummaryAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COALESCE(SUM(l.ReservedQty * ({MassFactorSql})), 0),
                   COALESCE(SUM(CASE WHEN l.Status = N'Upcoming' AND l.OpenForReservation = 1 THEN 1 ELSE 0 END), 0)
            FROM dbo.listing AS l
            WHERE l.FarmerId = @FarmerId AND l.Status <> N'Hidden';
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            dashboard.ReservedTons = reader.GetDecimal(0) / 1000m;
            dashboard.UpcomingListingCount = reader.GetInt32(1);
        }
    }

    private static async Task LoadSupplyPipelineAsync(
        SqlConnection connection, int farmerId, DateTime weekStart, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT pb.PredictedHarvestDate,
                   COALESCE(l.EstimatedQty * ({MassFactorSql}), pb.QuantityPlanted * c.YieldFactor) AS ExpectedKg,
                   COALESCE(l.ReservedQty * ({MassFactorSql}), 0) AS ReservedKg
            FROM dbo.planting_batch AS pb
            INNER JOIN dbo.seed_stock AS ss ON ss.Id = pb.SeedStockId
            INNER JOIN dbo.crop AS c ON c.Id = ss.CropId
            LEFT JOIN dbo.listing AS l ON l.BatchId = pb.Id AND l.FarmerId = pb.FarmerId
            WHERE pb.FarmerId = @FarmerId AND pb.Status = N'Active'
              AND pb.PredictedHarvestDate >= @WeekStart AND pb.PredictedHarvestDate < @WeekEnd;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        command.Parameters.Add("@WeekStart", SqlDbType.Date).Value = weekStart;
        command.Parameters.Add("@WeekEnd", SqlDbType.Date).Value = weekStart.AddDays(56);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var harvestDate = reader.GetDateTime(0).Date;
            var weekIndex = (int)((harvestDate - weekStart).TotalDays / 7);
            if (weekIndex is < 0 or >= 8)
            {
                continue;
            }

            var expectedTons = Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture) / 1000m;
            var reservedTons = Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture) / 1000m;
            dashboard.SupplyWeeks[weekIndex].ReservedTons += reservedTons;
            dashboard.SupplyWeeks[weekIndex].OpenTons += Math.Max(0m, expectedTons - reservedTons);
        }
    }

    private static async Task LoadUpcomingHarvestsAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT TOP (8) pb.Id, pb.BatchCode, c.Name, COALESCE(l.Grade, N'-'),
                   COALESCE(pb.FieldName, N'-'), pb.PredictedHarvestDate,
                   COALESCE(l.EstimatedQty, pb.QuantityPlanted * c.YieldFactor),
                   COALESCE(l.Unit, N'kg'),
                   COALESCE(l.EstimatedQty * ({MassFactorSql}), pb.QuantityPlanted * c.YieldFactor),
                   COALESCE(l.ReservedQty, 0), COALESCE(l.ReservedQty * ({MassFactorSql}), 0)
            FROM dbo.planting_batch AS pb
            INNER JOIN dbo.seed_stock AS ss ON ss.Id = pb.SeedStockId
            INNER JOIN dbo.crop AS c ON c.Id = ss.CropId
            LEFT JOIN dbo.listing AS l ON l.BatchId = pb.Id AND l.FarmerId = pb.FarmerId
            WHERE pb.FarmerId = @FarmerId AND pb.Status = N'Active'
            ORDER BY pb.PredictedHarvestDate, pb.Id;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var harvestDate = reader.GetDateTime(5).Date;
            dashboard.UpcomingHarvests.Add(new UpcomingHarvest
            {
                BatchId = reader.GetInt32(0),
                BatchCode = reader.GetString(1),
                CropName = reader.GetString(2),
                Grade = reader.GetString(3),
                FieldName = reader.GetString(4),
                ExpectedDate = harvestDate,
                EstimatedQuantity = Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture),
                Unit = reader.GetString(7),
                EstimatedTons = Convert.ToDecimal(reader.GetValue(8), CultureInfo.InvariantCulture) / 1000m,
                ReservedQuantity = Convert.ToDecimal(reader.GetValue(9), CultureInfo.InvariantCulture),
                ReservedTons = Convert.ToDecimal(reader.GetValue(10), CultureInfo.InvariantCulture) / 1000m,
                IsReady = harvestDate <= DateTime.Today
            });
        }
    }

    private static async Task LoadOrderSummaryAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                SUM(CASE WHEN oi.Status = N'Placed' THEN 1 ELSE 0 END),
                SUM(CASE WHEN oi.Status = N'Dispatched' THEN 1 ELSE 0 END),
                SUM(CASE WHEN oi.Status = N'Placed' THEN oi.QtyRequested * oi.UnitPrice ELSE 0 END)
            FROM dbo.order_item AS oi
            INNER JOIN dbo.listing AS l ON l.Id = oi.ListingId
            WHERE l.FarmerId = @FarmerId;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            dashboard.OrdersToConfirm = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
            dashboard.DeliveriesAwaitingCheck = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            dashboard.PendingOrdersValueRwf = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
        }
    }

    private static async Task LoadPendingOrdersAsync(
        SqlConnection connection, int farmerId, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (8) oi.Id, o.OrderNumber, bb.Name, c.Name, oi.QtyRequested, l.Unit,
                   oi.QtyRequested * oi.UnitPrice, o.DeliveryDate
            FROM dbo.order_item AS oi
            INNER JOIN dbo.orders AS o ON o.Id = oi.OrderId
            INNER JOIN dbo.listing AS l ON l.Id = oi.ListingId
            INNER JOIN dbo.buyer_business AS bb ON bb.Id = o.BuyerBusinessId
            INNER JOIN dbo.crop AS c ON c.Id = l.CropId
            WHERE l.FarmerId = @FarmerId AND oi.Status = N'Placed'
            ORDER BY o.CreatedAt, oi.Id;
            """;
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dashboard.PendingOrders.Add(new PendingBuyerOrder
            {
                OrderItemId = reader.GetInt32(0),
                OrderNumber = reader.GetString(1),
                BuyerName = reader.GetString(2),
                CropName = reader.GetString(3),
                Quantity = reader.GetDecimal(4),
                Unit = reader.GetString(5),
                TotalRwf = reader.GetDecimal(6),
                DeliveryDate = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            });
        }
    }

    private static async Task LoadSalesAsync(
        SqlConnection connection, int farmerId, DateTime today, FarmerDashboardData dashboard, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(i.Amount), 0), COUNT(DISTINCT i.OrderId)
            FROM dbo.invoice AS i
            WHERE i.Status = N'Paid' AND i.PaidAt >= @MonthStart AND i.PaidAt < @NextMonth
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.order_item AS oi
                  INNER JOIN dbo.listing AS l ON l.Id = oi.ListingId
                  WHERE oi.OrderId = i.OrderId AND l.FarmerId = @FarmerId
              );
            """;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        await using var command = new SqlCommand(sql, connection);
        AddFarmerId(command, farmerId);
        command.Parameters.Add("@MonthStart", SqlDbType.DateTime2).Value = monthStart;
        command.Parameters.Add("@NextMonth", SqlDbType.DateTime2).Value = monthStart.AddMonths(1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            dashboard.SalesThisMonthRwf = reader.GetDecimal(0);
            dashboard.PaidOrdersThisMonth = reader.GetInt32(1);
        }
    }

    private static void AddFarmerId(SqlCommand command, int farmerId) =>
        command.Parameters.Add("@FarmerId", SqlDbType.Int).Value = farmerId;

    private const string MassFactorSql = "CASE WHEN l.UnitSizeKg IS NOT NULL THEN l.UnitSizeKg WHEN LOWER(l.Unit) IN (N't', N'tonne', N'tonnes') THEN 1000 ELSE 1 END";
}

public sealed class FarmerDashboardData
{
    public string FarmName { get; set; } = "Your farm";
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public bool FarmProfileLoaded { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public bool IsUnverified => FarmProfileLoaded && string.Equals(VerificationStatus, "Unverified", StringComparison.OrdinalIgnoreCase);
    public string? ErrorMessage { get; set; }
    public int ActiveBatches { get; set; }
    public int BatchesAddedThisMonth { get; set; }
    public int FieldCount { get; set; }
    public int ReadyWithinFourteenDays { get; set; }
    public int ReadyForHarvest { get; set; }
    public decimal ReservedTons { get; set; }
    public int UpcomingListingCount { get; set; }
    public decimal PendingOrdersValueRwf { get; set; }
    public decimal SalesThisMonthRwf { get; set; }
    public int PaidOrdersThisMonth { get; set; }
    public int LowStockCount { get; set; }
    public List<string> LowStockCropNames { get; set; } = [];
    public int OrdersToConfirm { get; set; }
    public int DeliveriesAwaitingCheck { get; set; }
    public List<SupplyWeek> SupplyWeeks { get; set; } = [];
    public List<UpcomingHarvest> UpcomingHarvests { get; set; } = [];
    public List<PendingBuyerOrder> PendingOrders { get; set; } = [];
    public int AttentionCount => OrdersToConfirm + ReadyForHarvest + LowStockCount + DeliveriesAwaitingCheck;
    public string SalesDisplay => SalesThisMonthRwf >= 1_000_000m
        ? $"RWF {SalesThisMonthRwf / 1_000_000m:0.0}M"
        : $"RWF {SalesThisMonthRwf:N0}";
}

public sealed class SupplyWeek
{
    public DateTime StartDate { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal ReservedTons { get; set; }
    public decimal OpenTons { get; set; }
}

public sealed class UpcomingHarvest
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public DateTime ExpectedDate { get; set; }
    public decimal EstimatedQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal EstimatedTons { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal ReservedTons { get; set; }
    public bool IsReady { get; set; }
}

public sealed class PendingBuyerOrder
{
    public int OrderItemId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal TotalRwf { get; set; }
    public DateTime? DeliveryDate { get; set; }
}
