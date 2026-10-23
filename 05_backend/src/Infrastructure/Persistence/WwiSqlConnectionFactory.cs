using System.Data;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

public sealed class WwiSqlConnectionFactory : IWwiDbConnectionFactory
{
    private readonly string _connectionString;

    public WwiSqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
