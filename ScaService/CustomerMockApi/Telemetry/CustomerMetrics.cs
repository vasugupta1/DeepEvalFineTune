using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CustomerMockApi.Telemetry;

public sealed class CustomerMetrics
{
    public const string MeterName = "CustomerMockApi";

    private const string EndpointTag = "customer.endpoint";
    private const string OutcomeTag = "customer.outcome";
    private const string StatusCodeTag = "http.response.status_code";

    private readonly Counter<long> _requestsProcessed;
    private readonly Histogram<double> _requestDuration;

    public CustomerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _requestsProcessed = meter.CreateCounter<long>(
            "customer.requests.processed",
            unit: "{request}",
            description: "Number of customer lookup requests processed.");

        _requestDuration = meter.CreateHistogram<double>(
            "customer.request.duration",
            unit: "s",
            description: "Time taken to process a customer lookup request.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5]
            });
    }

    public void RecordRequestProcessed(string endpoint, int statusCode, TimeSpan duration)
    {
        var tags = new TagList
        {
            { EndpointTag, endpoint },
            { StatusCodeTag, statusCode },
            { OutcomeTag, statusCode < 400 ? "success" : "failure" }
        };

        _requestsProcessed.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }
}
