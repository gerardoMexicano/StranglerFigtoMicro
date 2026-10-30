using Application.Common.Models;
using Application.Features.Taxes.Abstractions;
using Application.Features.Taxes.Dtos;
using Dapper;

namespace Infrastructure.Persistence.Repositories;

public sealed class TaxesReadRepository(ITaxesDbConnectionFactory connectionFactory) : ITaxesReadRepository
{
    private const int DefaultCommandTimeoutSeconds = 300;
    private readonly ITaxesDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<PagedResult<ConsolidatedReportItemDto>> GetConsolidatedReportAsync(
        DateTime startDate,
        DateTime endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM CfdiCore c
            LEFT JOIN CfdiComplemento comp ON c.UUID = comp.UUID
            LEFT JOIN NominaResumen nr ON c.UUID = nr.UUID
            LEFT JOIN NominaMovimiento nm ON c.UUID = nm.UUID
            WHERE c.Fecha >= @StartDate AND c.Fecha <= @EndDate;

            SELECT
                c.UUID,
                c.EmisorRFC,
                c.ReceptorRFC,
                c.Total,
                c.Fecha,
                comp.TipoComplemento,
                nr.NumEmpleado,
                nr.Departamento,
                nr.Puesto,
                nr.TotalPercepciones,
                nr.TotalDeducciones,
                nm.TipoMovimiento,
                nm.Concepto,
                nm.Importe
            FROM CfdiCore c
            LEFT JOIN CfdiComplemento comp ON c.UUID = comp.UUID
            LEFT JOIN NominaResumen nr ON c.UUID = nr.UUID
            LEFT JOIN NominaMovimiento nm ON c.UUID = nm.UUID
            WHERE c.Fecha >= @StartDate AND c.Fecha <= @EndDate
            ORDER BY c.Fecha DESC, c.Id DESC, nm.Id ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var parameters = new
        {
            StartDate = startDate,
            EndDate = endDate,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        };

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        using var multi = await connection.QueryMultipleAsync(command);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ConsolidatedReportItemDto>()).AsList();

        return new PagedResult<ConsolidatedReportItemDto>(items, page, pageSize, totalCount);
    }
}
