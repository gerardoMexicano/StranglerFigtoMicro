using Api.Extensions;
using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Sales.Dtos;
using Application.Features.Sales.Queries;

namespace Api.Endpoints;

public sealed class SalesEndpoints : IEndpoint
{
    private static readonly DateTime DefaultStartDate = new(2013, 1, 1);
    private static readonly DateTime DefaultEndDate = new(2016, 12, 31);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Sales (LEgFrontNEWBACK)")
            .RequireAuthorization("Lectura");

        group.MapGet("/customer-transactions", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetCustomerTransactionsQuery, PagedResult<CustomerTransactionDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetCustomerTransactionsQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetCustomerTransactions")
        .WithSummary("Consulta paginada de CustomerTransactions en WideWorldImporters.Sales por rango de fechas.")
        .Produces<PagedResult<CustomerTransactionDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/invoice-lines", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetInvoiceLinesQuery, PagedResult<InvoiceLineDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetInvoiceLinesQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetInvoiceLines")
        .WithSummary("Consulta paginada de InvoiceLines en WideWorldImporters.Sales por rango de fechas.")
        .Produces<PagedResult<InvoiceLineDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/invoices", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetInvoicesQuery, PagedResult<InvoiceDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetInvoicesQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetInvoices")
        .WithSummary("Consulta paginada de Invoices en WideWorldImporters.Sales por rango de fechas.")
        .Produces<PagedResult<InvoiceDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/order-lines", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetOrderLinesQuery, PagedResult<OrderLineDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrderLinesQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetOrderLines")
        .WithSummary("Consulta paginada de OrderLines en WideWorldImporters.Sales por rango de fechas.")
        .Produces<PagedResult<OrderLineDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/orders", async (
            DateTime? startDate,
            DateTime? endDate,
            int? page,
            int? pageSize,
            IQueryHandler<GetOrdersQuery, PagedResult<OrderDto>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetOrdersQuery(
                StartDate: startDate ?? DefaultStartDate,
                EndDate: endDate ?? DefaultEndDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await handler.HandleAsync(query, cancellationToken);
            return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
        })
        .WithName("GetOrders")
        .WithSummary("Consulta paginada de Orders en WideWorldImporters.Sales por rango de fechas.")
        .Produces<PagedResult<OrderDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
