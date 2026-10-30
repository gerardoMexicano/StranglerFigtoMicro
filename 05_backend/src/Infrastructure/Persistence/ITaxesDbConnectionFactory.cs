using System.Data;

namespace Infrastructure.Persistence;

/// <summary>
/// Factoría de conexiones dedicada a la base de datos taxes_proto
/// (ConnectionStrings:TaxesProto / TAXES_DB_CONNECTION).
/// </summary>
public interface ITaxesDbConnectionFactory
{
    IDbConnection CreateConnection();
}
