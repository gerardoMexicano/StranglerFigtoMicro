using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Attendance.Commands;
using Application.Features.Attendance.Dtos;
using Application.Features.Attendance.Queries;
using Application.Features.Sales.Dtos;
using Application.Features.Sales.Queries;
using Application.Features.Users.GetCurrentUser;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>, GetCurrentUserQueryHandler>();

        // Casos de uso extraídos de /legacy/LEgFrontNEWBACK (WideWorldImporters.Sales)
        services.AddScoped<IQueryHandler<GetCustomerTransactionsQuery, PagedResult<CustomerTransactionDto>>, GetCustomerTransactionsQueryHandler>();
        services.AddScoped<IQueryHandler<GetInvoiceLinesQuery, PagedResult<InvoiceLineDto>>, GetInvoiceLinesQueryHandler>();
        services.AddScoped<IQueryHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>, GetInvoicesQueryHandler>();
        services.AddScoped<IQueryHandler<GetOrderLinesQuery, PagedResult<OrderLineDto>>, GetOrderLinesQueryHandler>();
        services.AddScoped<IQueryHandler<GetOrdersQuery, PagedResult<OrderDto>>, GetOrdersQueryHandler>();

        // Checador de Asistencia migrado de /legacy/Usuarios.aspx
        services.AddScoped<ICommandHandler<RegisterCheckInCommand, CheckInDto>, RegisterCheckInCommandHandler>();
        services.AddScoped<IQueryHandler<GetMyCheckInsQuery, IReadOnlyList<CheckInDto>>, GetMyCheckInsQueryHandler>();

        return services;
    }
}
