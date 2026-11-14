using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace PharmaCore.Persistence;

public interface ISqlConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct = default);
}

public sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public async Task<DbConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
