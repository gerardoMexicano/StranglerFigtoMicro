using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace UnitTests.Infrastructure;

public sealed class WwiSqlConnectionFactoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateConnection_WhenWwiConnectionStringIsEmpty_ShouldThrowArgumentException(string invalidConnectionString)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var factory = new WwiSqlConnectionFactory(invalidConnectionString);
            using var _ = factory.CreateConnection();
        });
    }

    [Fact]
    public void CreateConnection_WithValidWwiConnectionString_ShouldReturnSqlConnection()
    {
        // Arrange
        const string connectionString = "Server=localhost,1433;Database=WideWorldImporters;User Id=app_lector;Password=Secret123!;TrustServerCertificate=True";
        var factory = new WwiSqlConnectionFactory(connectionString);

        // Act
        using var connection = factory.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.IsType<SqlConnection>(connection);
    }
}
