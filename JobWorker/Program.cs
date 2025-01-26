// JobWorker.csproj
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
var durableConsumerName = "worker";
//var durableConsumerName = $"worker-{Guid.NewGuid()}";

// Step 1: Create a durable consumer to receive jobs
var consumerConfig = new ConsumerConfig
{
    Name = durableConsumerName,
    DurableName = durableConsumerName,
    AckPolicy = ConsumerConfigAckPolicy.Explicit, // Ensure jobs are acknowledged
    FilterSubject = jobSubject // サブジェクトを明示
};

var consumer = await js.CreateOrUpdateConsumerAsync(streamName, consumerConfig);
Console.WriteLine($"Created new consumer: {consumer.Info.Name}");
Console.WriteLine($"Worker '{durableConsumerName}' ready to process jobs.");

// Step 2: Fetch and process jobs
await foreach (var msg in consumer.ConsumeAsync<Job>())
{
    var job = msg.Data;
    Console.WriteLine($"Processing job {job.Id}: {job.Description}...");

    // Simulate job execution
    await Task.Delay(2000); // Simulating job execution time

    // Send job result back to JobManager
    await nc.PublishAsync(subject: resultSubject, data: new JobResult
    {
        JobId = job.Id,
        Status = "Completed"
    });

    Console.WriteLine($"Job {job.Id} completed. Sending result to JobManager.");

    // Acknowledge the job to remove it from the queue
    await msg.AckAsync();
}

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
