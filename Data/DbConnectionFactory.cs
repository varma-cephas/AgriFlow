using Microsoft.Data.SqlClient;

namespace AgriFlow.Data;

public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is missing.");
    }

    public SqlConnection CreateConnection() => new SqlConnection(_connectionString);
}