using Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace UnitTests.Infrastructure;

public sealed class SqlConnectionFactoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateConnection_WhenConnectionStringIsEmpty_ShouldThrowArgumentException(string invalidConnectionString)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var factory = new SqlConnectionFactory(invalidConnectionString);
            using var _ = factory.CreateConnection();
        });
    }

    [Fact]
    public void CreateConnection_WithValidConnectionString_ShouldReturnSqlConnection()
    {
        // Arrange
        const string connectionString = "Server=localhost,1433;Database=Laboratory;User Id=app_lector;Password=Secret123!;TrustServerCertificate=True";
        var factory = new SqlConnectionFactory(connectionString);

        // Act
        using var connection = factory.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.IsType<SqlConnection>(connection);
    }
}
