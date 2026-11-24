using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public record MarketplaceListing(
    int Id,
    string CropName,
    string FarmName,
    string District,
    string Status,          // "Available" or "Upcoming"
    string Grade,
    string Unit,
    decimal? UnitSizeKg,
    decimal PricePerUnit,
    decimal Quantity,       // available qty, or estimated qty still open to reserve
    decimal MinOrderQty,
    DateTime DisplayDate,   // harvested date (Available) or expected date (Upcoming)
    bool OpenForReservation);

public record MarketplaceFilter(string? Search, int? CropId, string? Province, string? Grade, string? Sort);

public record CropOption(int Id, string Name);

public record MarketplaceFilterOptions(List<CropOption> Crops, List<string> Provinces);

public class MarketplaceRepository
{
    private readonly DbConnectionFactory _db;

    public MarketplaceRepository(DbConnectionFactory db) => _db = db;

    // BR-14: only listings from verified farms. Hidden and sold-out listings are
    // left out because only 'Available' and 'Upcoming' statuses are included.
    private const string VisibleListings = @"
        FROM listing l
        INNER JOIN farmer_profile fp ON fp.UserId = l.FarmerId
        INNER JOIN crop c ON c.Id = l.CropId
        INNER JOIN planting_batch pb ON pb.Id = l.BatchId
        WHERE fp.VerificationStatus = 'Verified'
          AND ( (l.Status = 'Available' AND l.AvailableQty > 0)
             OR (l.Status = 'Upcoming' AND ISNULL(l.EstimatedQty, 0) - l.ReservedQty > 0) )";

    public async Task<List<MarketplaceListing>> GetListingsAsync(MarketplaceFilter filter)
    {
        var sql = @"
            SELECT l.Id, c.Name, fp.FarmName, fp.District, l.Status, l.Grade, l.Unit, l.UnitSizeKg,
                   l.PricePerUnit,
                   CASE WHEN l.Status = 'Available' THEN l.AvailableQty
                        ELSE ISNULL(l.EstimatedQty, 0) - l.ReservedQty END AS Quantity,
                   l.MinOrderQty,
                   CASE WHEN l.Status = 'Available' THEN ISNULL(pb.HarvestDate, pb.PredictedHarvestDate)
                        ELSE pb.PredictedHarvestDate END AS DisplayDate,
                   l.OpenForReservation " + VisibleListings;

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand { Connection = conn };

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            sql += @" AND (c.Name LIKE @Search ESCAPE '\' OR fp.FarmName LIKE @Search ESCAPE '\')";
            cmd.Parameters.AddWithValue("@Search", "%" + EscapeLike(filter.Search.Trim()) + "%");
        }
        if (filter.CropId.HasValue)
        {
            sql += " AND l.CropId = @CropId";
            cmd.Parameters.AddWithValue("@CropId", filter.CropId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Province))
        {
            sql += " AND fp.Province = @Province";
            cmd.Parameters.AddWithValue("@Province", filter.Province);
        }
        if (!string.IsNullOrWhiteSpace(filter.Grade))
        {
            sql += " AND l.Grade = @Grade";
            cmd.Parameters.AddWithValue("@Grade", filter.Grade);
        }

        // Only these fixed values are ever used for sorting (never raw user text).
        var orderBy = filter.Sort switch
        {
            "price_asc" => "l.PricePerUnit ASC, l.Id DESC",
            "price_desc" => "l.PricePerUnit DESC, l.Id DESC",
            "date" => "DisplayDate ASC, l.Id DESC",
            _ => "l.CreatedAt DESC, l.Id DESC"
        };
        cmd.CommandText = sql + " ORDER BY " + orderBy + ";";

        var list = new List<MarketplaceListing>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new MarketplaceListing(
                r.GetInt32(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.GetString(6),
                r.IsDBNull(7) ? null : r.GetDecimal(7),
                r.GetDecimal(8),
                r.GetDecimal(9),
                r.GetDecimal(10),
                r.GetDateTime(11),
                r.GetBoolean(12)));
        }
        return list;
    }

    // Fills the Crop and Province dropdowns from the listings buyers can actually see.
    public async Task<MarketplaceFilterOptions> GetFilterOptionsAsync()
    {
        var crops = new List<CropOption>();
        var provinces = new List<string>();

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();

        await using (var cmd = new SqlCommand("SELECT DISTINCT c.Id, c.Name " + VisibleListings + " ORDER BY c.Name;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) crops.Add(new CropOption(r.GetInt32(0), r.GetString(1)));
        }

        await using (var cmd = new SqlCommand("SELECT DISTINCT fp.Province " + VisibleListings + " ORDER BY fp.Province;", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) provinces.Add(r.GetString(0));
        }

        return new MarketplaceFilterOptions(crops, provinces);
    }

    // 'Verified', 'Pending' or 'Rejected'. Pending buyers can browse but not order.
    public async Task<string?> GetBuyerStatusAsync(int buyerBusinessId)
    {
        await using var conn = _db.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT VerificationStatus FROM buyer_business WHERE Id = @Id;", conn);
        cmd.Parameters.AddWithValue("@Id", buyerBusinessId);
        return await cmd.ExecuteScalarAsync() as string;
    }

    // Makes %, _ and [ in the search box behave as normal characters.
    private static string EscapeLike(string s) =>
        s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");
}