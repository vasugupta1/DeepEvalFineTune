using Microsoft.AspNetCore.Mvc;
using ScaMockApi.Messaging;
using ScaMockApi.Models;
using ScaMockApi.Repositories;
using ScaMockApi.Services;
using ScaMockApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddScaTelemetry();
builder.Services.AddHealthChecks();

builder.Services.Configure<ScaEventOptions>(
    builder.Configuration.GetSection(ScaEventOptions.SectionName));

builder.AddMongoDBClient("scatransactions");
builder.AddRabbitMQClient("deepeval-rabbitmq");

builder.Services.AddScoped<IScaTransactionRepository, MongoScaTransactionRepository>();
builder.Services.AddScoped<IScaEventPublisher, RabbitMqScaEventPublisher>();
builder.Services.AddScoped<IScaTransactionService, ScaTransactionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.MapHealthChecks("/api/healthcheck");

const string idempotencyKeyHeader = "Idempotency-Key";

app.MapPost(ScaRoutes.StartTransaction, async (
        StartScaTransactionRequest request,
        [FromHeader(Name = idempotencyKeyHeader)] string? idempotencyKey,
        IScaTransactionService service,
        CancellationToken cancellationToken) =>
        ToHttpResult(await service.StartAsync(request, idempotencyKey, cancellationToken)))
    .WithName("StartScaTransaction");

app.MapPost(ScaRoutes.IssueFactor, async (
        IssueScaFactorRequest request,
        [FromHeader(Name = idempotencyKeyHeader)] string? idempotencyKey,
        IScaTransactionService service,
        CancellationToken cancellationToken) =>
        ToHttpResult(await service.IssueFactorAsync(request, idempotencyKey, cancellationToken)))
    .WithName("IssueScaFactor");

app.MapPost(ScaRoutes.VerifyTransaction, async (
        VerifyScaTransactionRequest request,
        [FromHeader(Name = idempotencyKeyHeader)] string? idempotencyKey,
        IScaTransactionService service,
        CancellationToken cancellationToken) =>
        ToHttpResult(await service.VerifyAsync(request, idempotencyKey, cancellationToken)))
    .WithName("VerifyScaTransaction");

app.Run();

static IResult ToHttpResult(ScaServiceResult result) =>
    Results.Json(result.Body, statusCode: result.StatusCode);
