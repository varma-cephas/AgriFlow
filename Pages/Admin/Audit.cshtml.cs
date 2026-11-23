using AgriFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace AgriFlow.Pages.Admin;

public sealed record AdminAuditRow(DateTime CreatedAt, string Actor, string Entity, string Action, string Summary, string Result);

[Authorize(Roles = "Admin")]
public class AuditModel : PageModel
{
    private readonly DbConnectionFactory _db;
    public IReadOnlyList<AdminAuditRow> Entries { get; private set; } = [];
    public AuditModel(DbConnectionFactory db) => _db = db;
    public async Task OnGetAsync()
    {
        const string sql = @"SELECT TOP (200) l.CreatedAt,ISNULL(u.FullName,'System'),l.Entity,l.Action,ISNULL(l.Summary,''),l.Result
            FROM audit_log l LEFT JOIN app_user u ON u.Id=l.ActorUserId ORDER BY l.CreatedAt DESC;";
        var rows = new List<AdminAuditRow>();
        await using var connection = _db.CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new AdminAuditRow(reader.GetDateTime(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        Entries = rows;
    }
}
