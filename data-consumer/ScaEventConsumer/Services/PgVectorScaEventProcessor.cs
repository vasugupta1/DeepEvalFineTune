using Microsoft.Extensions.AI;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using ScaEventConsumer.Models;

namespace ScaEventConsumer.Services;

public sealed class PgVectorScaEventProcessor(
    NpgsqlDataSource dataSource,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<PgVectorScaEventProcessor> logger) : IScaEventProcessor
{
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaInitialized;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaInitialized)
        {
            return;
        }

        await _schemaLock.WaitAsync(cancellationToken);
        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using (var cmd = new NpgsqlCommand(
                """
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS sca_event_embeddings (
                    event_id         uuid        PRIMARY KEY,
                    idempotency_key  text        NOT NULL,
                    version          text        NOT NULL,
                    event_type       text        NOT NULL,
                    http_method      text        NOT NULL,
                    http_path        text        NOT NULL,
                    status_code      integer     NOT NULL,
                    content          text        NOT NULL,
                    payload          jsonb       NOT NULL,
                    embedding        vector(768) NOT NULL,
                    occurred_at      timestamptz NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_sca_event_embeddings_hnsw
                    ON sca_event_embeddings USING hnsw (embedding vector_cosine_ops);

                CREATE INDEX IF NOT EXISTS ix_sca_event_embeddings_type_occurred
                    ON sca_event_embeddings (event_type, occurred_at DESC);
                """,
                connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await connection.ReloadTypesAsync(cancellationToken);
            _schemaInitialized = true;
            logger.LogInformation("Verified pgvector extension and sca_event_embeddings schema.");
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    public async Task ProcessAsync(RawScaEventEnvelope rawEvent, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        var env = rawEvent.Envelope;
        var documentText = BuildSearchDocument(env);

        var vectorMemory = await embeddingGenerator.GenerateVectorAsync(
            documentText,
            cancellationToken: cancellationToken);

        await using var cmd = dataSource.CreateCommand(
            """
            INSERT INTO sca_event_embeddings (
                event_id,
                idempotency_key,
                version,
                event_type,
                http_method,
                http_path,
                status_code,
                content,
                payload,
                embedding,
                occurred_at
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
            ON CONFLICT (event_id) DO NOTHING;
            """);

        cmd.Parameters.AddWithValue(env.EventId);
        cmd.Parameters.AddWithValue(env.IdempotencyKey);
        cmd.Parameters.AddWithValue(env.Version);
        cmd.Parameters.AddWithValue(env.Type);
        cmd.Parameters.AddWithValue(env.Request.Method);
        cmd.Parameters.AddWithValue(env.Request.Path);
        cmd.Parameters.AddWithValue(env.Response.StatusCode);
        cmd.Parameters.AddWithValue(documentText);
        cmd.Parameters.Add(new NpgsqlParameter { Value = rawEvent.RawJson, NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.AddWithValue(new Vector(vectorMemory));
        cmd.Parameters.AddWithValue(env.OccurredAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildSearchDocument(
        ScaEventEnvelope<System.Text.Json.JsonElement, System.Text.Json.JsonElement> env) =>
        $"search_document: event_type={env.Type} version={env.Version} idempotency_key={env.IdempotencyKey} " +
        $"method={env.Request.Method} path={env.Request.Path} status_code={env.Response.StatusCode} " +
        $"occurred_at={env.OccurredAt:O} request={env.Request.Body.GetRawText()} response={env.Response.Body.GetRawText()}";
}
