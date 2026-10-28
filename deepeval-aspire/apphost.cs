#:package Aspire.Hosting.MongoDB@13.6.4
#:package Aspire.Hosting.PostgreSQL@13.6.4
#:package Aspire.Hosting.Python@13.6.4
#:package Aspire.Hosting.RabbitMQ@13.6.4
#:package Aspire.Hosting.Redis@13.6.4
#:package CommunityToolkit.Aspire.Hosting.Ollama@13.3.0
#:sdk Aspire.AppHost.Sdk@13.6.4

var builder = DistributedApplication.CreateBuilder(args);

//#####################Database & Cache########################
var vectorPostgres = builder.AddPostgres("deepeval-vectordb")
    .WithDataVolume("deepeval-vector-data")
    .WithImage("pgvector/pgvector", "pg16")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var vectorDb = vectorPostgres.AddDatabase("scavectors");

var redis = builder.AddRedis("deepeval-cache")
    .WithDataVolume("deepeval-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongo = builder.AddMongoDB("deepeval-mongodb")
    .WithDataVolume("deepeval-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithMongoExpress();

var transactionDb = mongo.AddDatabase("scatransactions");

var rabbitMq = builder.AddRabbitMQ("deepeval-rabbitmq")
    .WithDataVolume("deepeval-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

const string scaEventsQueue = "sca-transaction-events";

//#####################Downstream Domain Services##############
#pragma warning disable ASPIRECSHARPAPPS001
var scaMockApi = builder.AddCSharpApp("scamockapi", "../ScaService/ScaMockApi")
    .WithHttpEndpoint(port: 8001, name: "http")
    .WithHttpHealthCheck("/api/healthcheck")
    .WithEnvironment("ScaEvents__QueueName", scaEventsQueue)
    .WithReference(transactionDb)
    .WithReference(rabbitMq)
    .WaitFor(transactionDb)
    .WaitFor(rabbitMq);

var customerMockApi = builder.AddCSharpApp("customermockapi", "../ScaService/CustomerMockApi")
    .WithHttpEndpoint(port: 8002, name: "http")
    .WithHttpHealthCheck("/api/healthcheck")
    .WithReference(transactionDb)
    .WaitFor(transactionDb);

#pragma warning restore ASPIRECSHARPAPPS001

// Component tests are an xUnit project, so they must be executed via `dotnet test`
// (`dotnet run` on a test project builds and exits without running any tests).
// WaitFor ensures the tests only start once scamockapi is running and its
// /api/healthcheck endpoint reports healthy.
var scaMockApiComponentTests = builder.AddExecutable(
        "scamockapi-componenttests",
        "dotnet",
        "../ScaService/ScaMockApi.ComponentTests",
        "test", "--logger", "console;verbosity=normal")
    .WithEnvironment("ScaMockApi__BaseUrl", scaMockApi.GetEndpoint("http"))
    .WithEnvironment("CustomerMockApi__BaseUrl", customerMockApi.GetEndpoint("http"))
    .WithReference(scaMockApi)
    .WithReference(customerMockApi)
    .WaitFor(scaMockApi)
    .WaitFor(customerMockApi);

//#####################AI & Gateway############################
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithEnvironment("OLLAMA_KEEP_ALIVE", "-1")
    .WithEnvironment("HSA_OVERRIDE_GFX_VERSION", "11.0.0")
    .WithEnvironment("HIP_VISIBLE_DEVICES", "0")
    .WithEnvironment("RUST_LOG", "debug")
    .WithEnvironment("OLLAMA_NUM_PARALLEL", "100")
    .WithEnvironment("OLLAMA_IGPU_ENABLE", "1")
    .WithOpenWebUI()
    .WithGPUSupport(OllamaGpuVendor.AMD)
    .WithImageTag("rocm")
    .WithEnvironment("OLLAMA_CONTEXT_LENGTH", "16384")
    .WithContainerRuntimeArgs("--device", "/dev/kfd", "--device", "/dev/dri");

var llm = ollama.AddModel("llm", "llama3.1:8b");
var embeddings = ollama.AddModel("embeddings", "nomic-embed-text");

#pragma warning disable ASPIRECSHARPAPPS001
var scaEventConsumer = builder.AddCSharpApp("sca-event-consumer", "../data-consumer/ScaEventConsumer")
    .WithEnvironment("ScaEvents__QueueName", scaEventsQueue)
    .WithReference(rabbitMq)
    .WithReference(vectorDb)
    .WithReference(embeddings)
    .WaitFor(rabbitMq)
    .WaitFor(vectorDb)
    .WaitFor(embeddings);
#pragma warning restore ASPIRECSHARPAPPS001

var litellm = builder.AddContainer("litellm", "ghcr.io/berriai/litellm", "main-latest")
    .WithBindMount("../litellm/litellm_config.yaml", "/app/config.yaml", isReadOnly: true)
    .WithArgs("--config", "/app/config.yaml", "--port", "4000")
    .WithHttpEndpoint(port: 4000, targetPort: 4000, name: "http")
    .WithReference(ollama)
    .WithReference(redis)
    .WaitFor(ollama)
    .WaitFor(redis);

// var ai = builder.AddUvicornApp(name: "deepevalai", appDirectory: "../ai", app: "app:app")
//     .WithHttpEndpoint(port: 8000, env: "PORT")
//     .WithHttpHealthCheck("/healthcheck")
//     .WithEnvironment("LITELLM_BASE_URL", litellm.GetEndpoint("http"))
//     .WithEnvironment("SCA_MOCK_API_URL", scaMockApi.GetEndpoint("http"))
//     .WithReference(ollama)
//     .WithReference(llm)
//     .WithReference(embeddings)
//     .WithReference(vectorDb)
//     .WithReference(redis)
//     .WithReference(scaMockApi)
//     .WaitFor(llm)
//     .WaitFor(embeddings)
//     .WaitFor(vectorDb)
//     .WaitFor(litellm)
//     .WaitFor(scaMockApi);

builder.Build().Run();
