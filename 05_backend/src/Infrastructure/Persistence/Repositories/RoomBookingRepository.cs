using Application.Features.RoomBookings.Abstractions;
using Application.Features.RoomBookings.Dtos;
using Dapper;

namespace Infrastructure.Persistence.Repositories;

/// <summary>dbo.salas y dbo.reservaciones en Laboratory (sqlserver/db/06_salas.sql).</summary>
public sealed class RoomBookingRepository(IDbConnectionFactory connectionFactory) : IRoomBookingRepository
{
    private const string BookingColumns = """
        [id] AS [Id],
        [sala_id] AS [RoomId],
        [inicia_en] AS [StartsAt],
        [termina_en] AS [EndsAt],
        [asunto] AS [Subject],
        [user_id] AS [UserId],
        [username] AS [UserName]
        """;

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<RoomDto>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT [id] AS [Id], [nombre] AS [Name], [capacidad] AS [Capacity], [ubicacion] AS [Location]
            FROM [dbo].[salas]
            WHERE [activa] = 1
            ORDER BY [nombre];
            """;

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<RoomDto>(command)).AsList();
    }

    public async Task<bool> RoomExistsAsync(int roomId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(1) FROM [dbo].[salas] WHERE [id] = @RoomId AND [activa] = 1;";

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { RoomId = roomId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<IReadOnlyList<BookingRecord>> GetBookingsByDayAsync(
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {BookingColumns}
            FROM [dbo].[reservaciones]
            WHERE [inicia_en] >= @From AND [inicia_en] < @To
            ORDER BY [inicia_en], [sala_id];
            """;

        var from = day.ToDateTime(TimeOnly.MinValue);
        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { From = from, To = from.AddDays(1) },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<BookingRecord>(command)).AsList();
    }

    public async Task<BookingRecord?> GetBookingAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {BookingColumns} FROM [dbo].[reservaciones] WHERE [id] = @Id;";

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BookingRecord>(command);
    }

    public async Task<BookingRecord?> AddIfFreeAsync(NewBooking booking, CancellationToken cancellationToken = default)
    {
        // Un solo INSERT ... WHERE NOT EXISTS: UPDLOCK + HOLDLOCK bloquean el rango de horario de
        // esa sala hasta que termina la instrucción, así dos personas que reservan el mismo hueco
        // al mismo tiempo no entran las dos. Si se empalma no inserta nada y OUTPUT no devuelve filas.
        const string sql = """
            INSERT INTO [dbo].[reservaciones] ([sala_id], [inicia_en], [termina_en], [asunto], [user_id], [username])
            OUTPUT
                INSERTED.[id] AS [Id],
                INSERTED.[sala_id] AS [RoomId],
                INSERTED.[inicia_en] AS [StartsAt],
                INSERTED.[termina_en] AS [EndsAt],
                INSERTED.[asunto] AS [Subject],
                INSERTED.[user_id] AS [UserId],
                INSERTED.[username] AS [UserName]
            SELECT @RoomId, @StartsAt, @EndsAt, @Subject, @UserId, @UserName
            WHERE NOT EXISTS (
                SELECT 1
                FROM [dbo].[reservaciones] WITH (UPDLOCK, HOLDLOCK)
                WHERE [sala_id] = @RoomId
                  AND [inicia_en] < @EndsAt
                  AND [termina_en] > @StartsAt);
            """;

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, booking, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BookingRecord>(command);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM [dbo].[reservaciones] WHERE [id] = @Id;";

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }
}
