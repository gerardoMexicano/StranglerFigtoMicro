using Api.Extensions;
using Api.Middleware;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Registro de capas de Clean Architecture
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// OpenAPI nativo (.NET 10) + ProblemDetails RFC 7807 + HealthChecks + Minimal APIs modulares
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpoints(typeof(Program).Assembly);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Healthcheck anónimo para contenedor Docker y orquestación
app.MapHealthChecks("/health").AllowAnonymous();

// Mapeo automático de todos los módulos IEndpoint
app.MapEndpoints();

app.Run();

public partial class Program;
