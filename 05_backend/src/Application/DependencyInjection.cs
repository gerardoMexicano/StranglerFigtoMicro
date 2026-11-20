using Application.Common.Messaging;
using Application.Common.Models;
using Application.Features.Attendance.Commands;
using Application.Features.Attendance.Dtos;
using Application.Features.Attendance.Queries;

using Application.Features.Reports;
using Application.Features.Dashboard.Dtos;
using Application.Features.Dashboard.Queries;
using Application.Features.RoomBookings.Commands;
using Application.Features.RoomBookings.Dtos;
using Application.Features.RoomBookings.Queries;
using Application.Features.Sales.Dtos;
using Application.Features.Sales.Queries;
using Application.Features.Taxes.Commands;
using Application.Features.Taxes.Dtos;
using Application.Features.Taxes.Queries;
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

        // Casos de uso extraídos de /taxes/PaginaAntigua, /taxes/Home/PaginaAntiguaNuevoBackend y /taxes/Home/PaginaAngular
        services.AddScoped<IQueryHandler<GetConsolidatedReportQuery, PagedResult<ConsolidatedReportItemDto>>, GetConsolidatedReportQueryHandler>();
        services.AddScoped<IQueryHandler<GetPayrollSummaryQuery, PayrollSummaryReportDto>, GetPayrollSummaryQueryHandler>();
        services.AddScoped<ICommandHandler<UploadBulkXmlCommand, BulkXmlUploadResultDto>, UploadBulkXmlCommandHandler>();
        services.AddScoped<IQueryHandler<GetSeismicComparisonQuery, SeismicComparisonResultDto>, GetSeismicComparisonQueryHandler>();


        // Reportes CFDI 4.0 -> PDF en ZIP extraído de /legacy/Reportes.aspx
        services.AddScoped<ICommandHandler<GenerateCfdiPdfBatchCommand, CfdiPdfBatchResultDto>, GenerateCfdiPdfBatchCommandHandler>();

        // Dashboard Ejecutivo migrado de /legacy/Dashboard.aspx (WideWorldImporters)
        services.AddScoped<IQueryHandler<GetExecutiveDashboardQuery, ExecutiveDashboardDto>, GetExecutiveDashboardQueryHandler>();


        // Checador de Asistencia migrado de /legacy/Usuarios.aspx
        services.AddScoped<ICommandHandler<RegisterCheckInCommand, CheckInDto>, RegisterCheckInCommandHandler>();
        services.AddScoped<IQueryHandler<GetMyCheckInsQuery, IReadOnlyList<CheckInDto>>, GetMyCheckInsQueryHandler>();

        // Reservación de salas: funcionalidad nueva de /legacy/AngularPage
        services.AddScoped<IQueryHandler<GetRoomsQuery, IReadOnlyList<RoomDto>>, GetRoomsQueryHandler>();
        services.AddScoped<IQueryHandler<GetBookingsByDayQuery, IReadOnlyList<BookingDto>>, GetBookingsByDayQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBookingCommand, BookingDto>, CreateBookingCommandHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand>, CancelBookingCommandHandler>();

        return services;
    }
}
