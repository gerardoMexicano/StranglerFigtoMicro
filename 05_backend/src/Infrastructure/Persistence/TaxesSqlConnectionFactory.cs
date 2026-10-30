using System.Data;
using Microsoft.Data.SqlClient;

namespace Infrastructure.Persistence;

public sealed class TaxesSqlConnectionFactory : ITaxesDbConnectionFactory
{
    private readonly string _connectionString;

    public TaxesSqlConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
