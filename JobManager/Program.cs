// JobManager.csproj
// Install NuGet packages: NATS.Net
using System.Text.Json.Serialization;
using System.Threading;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";

await using var nc = new NatsClient(url);
await nc.ConnectAsync();

var js = nc.CreateJetStreamContext();

// Stream and subject configuration
var streamName = "JOB_STREAM";
var jobSubject = "jobs.pending";
var resultSubject = "jobs.result";

// Step 1: Create the stream for job messages
var streamConfig = new StreamConfig(name: streamName, subjects: [jobSubject])
{
    Retention = StreamConfigRetention.Workqueue, // Ensure messages are consumed by one worker
    MaxMsgs = 1000,  // 最大メッセージ数
    MaxBytes = 1024 * 1024, // 最大サイズ（1MB）
    MaxAge = TimeSpan.FromHours(24) // 最大保持期間（24時間）
};

await js.CreateStreamAsync(streamConfig);
Console.WriteLine($"Stream '{streamName}' created.");

// Step 2: Publish jobs
Console.WriteLine("Publishing job commands...");
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 1, Description = "Process Data A" });
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 2, Description = "Process Data B" });
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 3, Description = "Process Data C" });
Console.WriteLine("Jobs published.");

// Step 3: Listen for job completion notifications
Console.WriteLine("Waiting for job results...");
var cts = new CancellationTokenSource();
await foreach (var msg in nc.SubscribeAsync<JobResult>(resultSubject, cancellationToken: cts.Token))
{
    Console.WriteLine($"Job {msg.Data.JobId} completed with status: {msg.Data.Status}");
}
Console.WriteLine("JobManager finished.");

// Job model
public record Job
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("description")] public string Description { get; init; }
}

// JobResult model
public record JobResult
{
    [JsonPropertyName("job_id")] public int JobId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; }
}
