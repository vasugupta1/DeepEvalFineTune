using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ScaEventConsumer.Telemetry;

public sealed class ScaConsumerMetrics
{
    public const string MeterName = "ScaEventConsumer";

    private const string EventTypeTag = "sca.event_type";
    private const string QueueTag = "sca.queue";
    private const string StatusCodeTag = "http.response.status_code";
    private const string ErrorTypeTag = "error.type";

    private readonly Counter<long> _eventsConsumed;
    private readonly Counter<long> _eventsFailed;
    private readonly Histogram<double> _processingDuration;

    public ScaConsumerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _eventsConsumed = meter.CreateCounter<long>(
            "sca.consumer.events.processed",
            unit: "{event}",
            description: "Number of SCA envelope events successfully consumed, embedded, and persisted to pgvector.");

        _eventsFailed = meter.CreateCounter<long>(
            "sca.consumer.events.failed",
            unit: "{event}",
            description: "Number of SCA envelope events that failed processing.");

        _processingDuration = meter.CreateHistogram<double>(
            "sca.consumer.processing.duration",
            unit: "s",
            description: "Time taken to process, embed, and persist a consumed SCA event.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
            });
    }

    public void RecordProcessed(string queue, string eventType, int statusCode, TimeSpan duration)
    {
        var tags = new TagList
        {
            { QueueTag, queue },
            { EventTypeTag, eventType },
            { StatusCodeTag, statusCode }
        };

        _eventsConsumed.Add(1, tags);
        _processingDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordFailed(string queue, string? eventType, Exception exception, TimeSpan duration)
    {
        var tags = new TagList
        {
            { QueueTag, queue },
            { EventTypeTag, eventType ?? "unknown" },
            { ErrorTypeTag, exception.GetType().FullName }
        };

        _eventsFailed.Add(1, tags);
        _processingDuration.Record(duration.TotalSeconds, tags);
    }
}
