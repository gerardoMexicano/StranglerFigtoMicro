using Application.Features.Attendance.Abstractions;
using Application.Features.Attendance.Dtos;
using Dapper;

namespace Infrastructure.Persistence.Repositories;

/// <summary>dbo.asistencias en Laboratory (sqlserver/db/05_asistencias.sql).</summary>
public sealed class CheckInRepository(IDbConnectionFactory connectionFactory) : ICheckInRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<CheckInDto> AddAsync(NewCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        // La hora la pone SQL Server: el reloj del navegador no cuenta para la asistencia
        const string sql = """
            INSERT INTO [dbo].[asistencias] ([user_id], [username], [latitud], [longitud], [precision_metros])
            OUTPUT
                INSERTED.[id] AS [Id],
                INSERTED.[latitud] AS [Latitude],
                INSERTED.[longitud] AS [Longitude],
                INSERTED.[precision_metros] AS [AccuracyMeters],
                INSERTED.[registrado_en] AS [RegisteredAt]
            VALUES (@UserId, @UserName, @Latitude, @Longitude, @AccuracyMeters);
            """;

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, checkIn, cancellationToken: cancellationToken);
        return await connection.QuerySingleAsync<CheckInDto>(command);
    }

    public async Task<IReadOnlyList<CheckInDto>> GetRecentByUserAsync(
        string userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@Take)
                [id] AS [Id],
                [latitud] AS [Latitude],
                [longitud] AS [Longitude],
                [precision_metros] AS [AccuracyMeters],
                [registrado_en] AS [RegisteredAt]
            FROM [dbo].[asistencias]
            WHERE [user_id] = @UserId
            ORDER BY [registrado_en] DESC, [id] DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, Take = take },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<CheckInDto>(command)).AsList();
    }
}
