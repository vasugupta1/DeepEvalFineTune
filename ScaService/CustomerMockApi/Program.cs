using CustomerMockApi.Models;
using CustomerMockApi.Repositories;
using CustomerMockApi.Services;
using CustomerMockApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddCustomerTelemetry();
builder.Services.AddHealthChecks();

builder.AddMongoDBClient("scatransactions");

builder.Services.AddScoped<ICustomerTransactionRepository, MongoCustomerTransactionRepository>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapHealthChecks("/api/healthcheck");

app.MapGet(CustomerRoutes.ListCustomers, () => Results.Ok(KnownCustomers.All))
    .WithName("ListCustomers");

app.MapGet(CustomerRoutes.GetByAccountNumber, async (
        string accountNumber,
        ICustomerService service,
        CancellationToken cancellationToken) =>
        ToHttpResult(await service.GetCustomerByAccountNumberAsync(
            accountNumber,
            CustomerRoutes.GetByAccountNumber,
            cancellationToken)))
    .WithName("GetCustomerByAccountNumber");

app.MapPost(CustomerRoutes.Lookup, async (
        CustomerLookupRequest request,
        ICustomerService service,
        CancellationToken cancellationToken) =>
        ToHttpResult(await service.GetCustomerByAccountNumberAsync(
            request.AccountNumber,
            CustomerRoutes.Lookup,
            cancellationToken)))
    .WithName("LookupCustomer");

app.Run();

static IResult ToHttpResult(CustomerServiceResult result) =>
    Results.Json(result.Body, statusCode: result.StatusCode);
