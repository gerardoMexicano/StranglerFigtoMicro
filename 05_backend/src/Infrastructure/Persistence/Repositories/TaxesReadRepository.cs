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

    public async Task<IReadOnlyList<PayrollReceiptItemDto>> GetPayrollReceiptsAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                c.UUID,
                c.Fecha,
                c.ReceptorNombre,
                c.ReceptorRFC,
                ISNULL(c.Total, 0) AS Total,
                n.FechaPago,
                ISNULL(n.TotalPercepciones, 0) AS TotalPercepciones,
                ISNULL(n.TotalDeducciones, 0) AS TotalDeducciones,
                n.NumEmpleado,
                n.Departamento,
                n.Puesto
            FROM CfdiCore c
            INNER JOIN NominaResumen n ON c.UUID = n.UUID
            WHERE (@StartDate IS NULL OR c.Fecha >= @StartDate)
              AND (@EndDate IS NULL OR c.Fecha <= @EndDate)
            ORDER BY c.Fecha DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { StartDate = startDate, EndDate = endDate },
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<PayrollReceiptItemDto>(command);
        return rows.AsList();
    }
}

public sealed class TaxesWriteRepository(ITaxesDbConnectionFactory connectionFactory) : ITaxesWriteRepository
{
    private const int DefaultCommandTimeoutSeconds = 300;
    private readonly ITaxesDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<int> UpsertBatchAsync(
        IReadOnlyList<ParsedCfdiCoreRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return 0;
        }

        const string sqlCore = """
            IF NOT EXISTS (SELECT 1 FROM CfdiCore WHERE UUID = @UUID)
            BEGIN
                INSERT INTO CfdiCore (
                    UUID, TipoComprobante, Serie, Folio, Fecha, Moneda, SubTotal, Descuento, Total,
                    MetodoPago, LugarExpedicion, EmisorRFC, EmisorNombre, ReceptorRFC, ReceptorNombre,
                    UsoCFDI, FechaTimbrado, RfcProvCertif, XmlOriginal
                ) VALUES (
                    @UUID, @TipoComprobante, @Serie, @Folio, @Fecha, @Moneda, @SubTotal, @Descuento, @Total,
                    @MetodoPago, @LugarExpedicion, @EmisorRFC, @EmisorNombre, @ReceptorRFC, @ReceptorNombre,
                    @UsoCFDI, @FechaTimbrado, @RfcProvCertif, @XmlOriginal
                );
            END
            """;

        const string sqlComp = """
            IF NOT EXISTS (SELECT 1 FROM CfdiComplemento WHERE UUID = @UUID)
            BEGIN
                INSERT INTO CfdiComplemento (UUID, TipoComplemento, JsonData, XmlFragment)
                VALUES (@UUID, @TipoComplemento, @JsonData, @XmlFragment);
            END
            """;

        const string sqlNomina = """
            IF NOT EXISTS (SELECT 1 FROM NominaResumen WHERE UUID = @UUID)
            BEGIN
                INSERT INTO NominaResumen (
                    UUID, TipoNomina, FechaPago, FechaInicialPago, FechaFinalPago, NumDiasPagados,
                    TotalPercepciones, TotalDeducciones, TotalGravado, TotalExento, NumEmpleado, Departamento, Puesto,
                    TipoContrato, TipoRegimen, PeriodicidadPago, SalarioBaseCotApor, SalarioDiarioIntegrado
                ) VALUES (
                    @UUID, @TipoNomina, @FechaPago, @FechaInicialPago, @FechaFinalPago, @NumDiasPagados,
                    @TotalPercepciones, @TotalDeducciones, @TotalGravado, @TotalExento, @NumEmpleado, @Departamento, @Puesto,
                    @TipoContrato, @TipoRegimen, @PeriodicidadPago, @SalarioBaseCotApor, @SalarioDiarioIntegrado
                );
            END
            """;

        const string sqlCheckMovs = "SELECT COUNT(1) FROM NominaMovimiento WHERE UUID = @UUID;";

        const string sqlMov = """
            INSERT INTO NominaMovimiento (UUID, TipoMovimiento, TipoCodigo, Clave, Concepto, ImporteGravado, ImporteExento, Importe)
            VALUES (@UUID, @TipoMovimiento, @TipoCodigo, @Clave, @Concepto, @ImporteGravado, @ImporteExento, @Importe);
            """;

        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var processed = 0;
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await connection.ExecuteAsync(new CommandDefinition(
                commandText: sqlCore,
                parameters: new
                {
                    record.UUID,
                    record.TipoComprobante,
                    record.Serie,
                    record.Folio,
                    record.Fecha,
                    record.Moneda,
                    record.SubTotal,
                    record.Descuento,
                    record.Total,
                    record.MetodoPago,
                    record.LugarExpedicion,
                    record.EmisorRFC,
                    record.EmisorNombre,
                    record.ReceptorRFC,
                    record.ReceptorNombre,
                    record.UsoCFDI,
                    record.FechaTimbrado,
                    record.RfcProvCertif,
                    record.XmlOriginal
                },
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

            if (record.Complemento is { } comp)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    commandText: sqlComp,
                    parameters: new
                    {
                        record.UUID,
                        comp.TipoComplemento,
                        comp.JsonData,
                        comp.XmlFragment
                    },
                    commandTimeout: DefaultCommandTimeoutSeconds,
                    cancellationToken: cancellationToken));
            }

            if (record.Nomina is { } nomina)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    commandText: sqlNomina,
                    parameters: new
                    {
                        record.UUID,
                        nomina.TipoNomina,
                        nomina.FechaPago,
                        nomina.FechaInicialPago,
                        nomina.FechaFinalPago,
                        nomina.NumDiasPagados,
                        nomina.TotalPercepciones,
                        nomina.TotalDeducciones,
                        nomina.TotalGravado,
                        nomina.TotalExento,
                        nomina.NumEmpleado,
                        nomina.Departamento,
                        nomina.Puesto,
                        nomina.TipoContrato,
                        nomina.TipoRegimen,
                        nomina.PeriodicidadPago,
                        nomina.SalarioBaseCotApor,
                        nomina.SalarioDiarioIntegrado
                    },
                    commandTimeout: DefaultCommandTimeoutSeconds,
                    cancellationToken: cancellationToken));

                if (nomina.Movimientos.Count > 0)
                {
                    var existingMovs = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                        commandText: sqlCheckMovs,
                        parameters: new { record.UUID },
                        commandTimeout: DefaultCommandTimeoutSeconds,
                        cancellationToken: cancellationToken));

                    if (existingMovs == 0)
                    {
                        foreach (var mov in nomina.Movimientos)
                        {
                            await connection.ExecuteAsync(new CommandDefinition(
                                commandText: sqlMov,
                                parameters: new
                                {
                                    record.UUID,
                                    mov.TipoMovimiento,
                                    mov.TipoCodigo,
                                    mov.Clave,
                                    mov.Concepto,
                                    mov.ImporteGravado,
                                    mov.ImporteExento,
                                    mov.Importe
                                },
                                commandTimeout: DefaultCommandTimeoutSeconds,
                                cancellationToken: cancellationToken));
                        }
                    }
                }
            }

            processed++;
        }

        return processed;
    }
}
