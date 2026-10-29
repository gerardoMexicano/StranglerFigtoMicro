using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace UnitTests.Infrastructure;

public sealed class TaxesSqlConnectionFactoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateConnection_WhenTaxesConnectionStringIsEmpty_ShouldThrowArgumentException(string invalidConnectionString)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var factory = new TaxesSqlConnectionFactory(invalidConnectionString);
            using var _ = factory.CreateConnection();
        });
    }

    [Fact]
    public void CreateConnection_WithValidTaxesConnectionString_ShouldReturnSqlConnection()
    {
        // Arrange
        const string connectionString = "Server=localhost,1433;Database=taxes_proto;User Id=app_lector;Password=Secret123!;TrustServerCertificate=True";
        var factory = new TaxesSqlConnectionFactory(connectionString);

        // Act
        using var connection = factory.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.IsType<SqlConnection>(connection);
    }
}
