using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

namespace BlazorAppJobManager.Services;

public class JobService
{
    private readonly ILogger<JobService> _logger;

    private readonly INatsClient _natsClient;
    private readonly INatsJSContext _jsContext;
    private const string JobStreamName = "JOB_STREAM";
    private const string JobSubject = "jobs.pending";
    private const string ResultSubject = "jobs.result";

    // イベントを追加
    public event Action? JobResultsUpdated;

    public ConcurrentQueue<Job> JobQueue { get; } = new();
    public ConcurrentDictionary<int, string> JobResults { get; } = new();

    public JobService(ILogger<JobService> logger)
    {
        _logger = logger;

        var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";
        _natsClient = new NatsClient(url);
        _jsContext = _natsClient.CreateJetStreamContext();

        Task.Run(async () =>
        {
            await _natsClient.ConnectAsync();
            await InitializeStreamAsync();
        });

        ListenForJobResultsAsync();
    }

    private async Task InitializeStreamAsync()
    {
        var streamConfig = new StreamConfig(name: JobStreamName, subjects: [JobSubject])
        {
            Retention = StreamConfigRetention.Workqueue
        };
        await _jsContext.CreateStreamAsync(streamConfig);
    }

    public async Task PublishJobAsync(Job job)
    {
        JobQueue.Enqueue(job);
        await _jsContext.PublishAsync(subject: JobSubject, data: job);
        _logger.LogInformation("Jobs published.");
    }

    private async Task ListenForJobResultsAsync()
    {
        await foreach (var msg in _natsClient.SubscribeAsync<JobResult>(ResultSubject))
        {
            JobResults[msg.Data.JobId] = msg.Data.Status;
            _logger.LogInformation($"Job {msg.Data.JobId} completed with status: {msg.Data.Status}");

            // イベントを発火
            JobResultsUpdated?.Invoke();
        }
    }
}

public record Job
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("description")] public string Description { get; init; }
}

public record JobResult
{
    [JsonPropertyName("job_id")] public int JobId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; }
}

