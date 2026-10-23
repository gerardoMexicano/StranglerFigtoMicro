using System.Data;

namespace Infrastructure.Persistence;

/// <summary>
/// Factoría de conexiones dedicada a la base de datos WideWorldImporters
/// (ConnectionStrings:WideWorldImporters / WWI_DB_CONNECTION).
/// </summary>
public interface IWwiDbConnectionFactory
{
    IDbConnection CreateConnection();
}
