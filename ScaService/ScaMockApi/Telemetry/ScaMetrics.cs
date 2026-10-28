using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ScaMockApi.Telemetry;

/// <summary>
/// Custom SCA business metrics, exported via OpenTelemetry under the <see cref="MeterName"/> meter.
/// Standard HTTP metrics (e.g. <c>http.server.request.duration</c> per route) come from the
/// ASP.NET Core instrumentation; these add SCA-specific dimensions such as the event type.
/// </summary>
public sealed class ScaMetrics
{
    public const string MeterName = "ScaMockApi";

    private const string EndpointTag = "sca.endpoint";
    private const string EventTypeTag = "sca.event_type";
    private const string OutcomeTag = "sca.outcome";
    private const string StatusCodeTag = "http.response.status_code";
    private const string ErrorTypeTag = "error.type";

    private readonly Counter<long> _requestsProcessed;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _eventsPublished;
    private readonly Counter<long> _eventPublishFailures;

    public ScaMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _requestsProcessed = meter.CreateCounter<long>(
            "sca.requests.processed",
            unit: "{request}",
            description: "Number of SCA requests processed, by endpoint, event type and outcome.");

        _requestDuration = meter.CreateHistogram<double>(
            "sca.request.duration",
            unit: "s",
            description: "Time taken to process an SCA request, including persistence and event publishing.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5]
            });

        _eventsPublished = meter.CreateCounter<long>(
            "sca.events.published",
            unit: "{event}",
            description: "Number of SCA envelope events published to RabbitMQ.");

        _eventPublishFailures = meter.CreateCounter<long>(
            "sca.events.publish_failures",
            unit: "{event}",
            description: "Number of SCA envelope events that failed to publish to RabbitMQ.");
    }

    public void RecordRequestProcessed(string endpoint, string eventType, int statusCode, TimeSpan duration)
    {
        var tags = new TagList
        {
            { EndpointTag, endpoint },
            { EventTypeTag, eventType },
            { StatusCodeTag, statusCode },
            { OutcomeTag, statusCode < 400 ? "success" : "failure" }
        };

        _requestsProcessed.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordEventPublished(string eventType) =>
        _eventsPublished.Add(1, new KeyValuePair<string, object?>(EventTypeTag, eventType));

    public void RecordEventPublishFailed(string eventType, Exception exception) =>
        _eventPublishFailures.Add(1,
            new KeyValuePair<string, object?>(EventTypeTag, eventType),
            new KeyValuePair<string, object?>(ErrorTypeTag, exception.GetType().FullName));
}
