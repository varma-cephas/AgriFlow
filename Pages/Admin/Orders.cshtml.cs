using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using AgriFlow.Data;

namespace AgriFlow.Pages.Admin;

public sealed record AdminOrderRow(int Id, string CropName, string BuyerName, string FarmerName, decimal Quantity, decimal Total, string Status, DateTime CreatedAt);

[Authorize(Roles = "Admin")]
public class OrdersModel : PageModel
{
    private readonly DbConnectionFactory _db;
    public IReadOnlyList<AdminOrderRow> Orders { get; private set; } = [];
    public OrdersModel(DbConnectionFactory db) => _db = db;
    public async Task OnGetAsync()
    {
        const string sql = @"SELECT o.Id,hl.CropName,ISNULL(b.FullName,''),ISNULL(f.FullName,''),oi.QuantityOrdered,o.TotalAmount,o.Status,o.CreatedAt
            FROM orders o INNER JOIN order_items oi ON oi.OrderId=o.Id INNER JOIN harvest_listing hl ON hl.Id=oi.HarvestListingId
            LEFT JOIN app_user b ON b.Id=o.CustomerId LEFT JOIN app_user f ON f.Id=hl.FarmerId ORDER BY o.CreatedAt DESC;";
        var rows = new List<AdminOrderRow>();
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new AdminOrderRow(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetDateTime(7)));
        Orders = rows;
    }
}
