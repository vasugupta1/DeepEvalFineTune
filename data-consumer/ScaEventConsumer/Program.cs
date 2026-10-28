using Npgsql;
using Npgsql.TypeMapping;
using ScaEventConsumer.Messaging;
using ScaEventConsumer.Services;
using ScaEventConsumer.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

builder.AddScaConsumerTelemetry();

builder.Services.Configure<ScaEventOptions>(
    builder.Configuration.GetSection(ScaEventOptions.SectionName));

builder.AddRabbitMQClient(
    "deepeval-rabbitmq",
    configureConnectionFactory: factory => factory.ConsumerDispatchConcurrency = 4);

builder.AddOllamaApiClient("embeddings")
    .AddEmbeddingGenerator();

builder.AddNpgsqlDataSource(
    "scavectors",
    configureDataSourceBuilder: dataSourceBuilder => ((INpgsqlTypeMapper)dataSourceBuilder).UseVector());

builder.Services.AddSingleton<IScaEventProcessor, PgVectorScaEventProcessor>();
builder.Services.AddHostedService<ScaEventConsumerWorker>();

var host = builder.Build();
host.Run();
