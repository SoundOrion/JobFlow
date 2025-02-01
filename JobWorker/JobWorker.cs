using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

class JobWorker
{
    private NatsClient? _nc;
    private INatsJSContext? _js;
    private readonly string _streamName = "JOB_STREAM";
    private readonly string _jobSubject = "jobs.pending";
    private readonly string _resultSubject = "jobs.result";
    private readonly string _durableConsumerName = $"worker-1";

    private readonly string _url;

    // **✅ デリゲートを追加**
    public event Action<JobResult>? JobCompleted;

    public JobWorker(string url)
    {
        _url = url;
    }

    public async Task StartAsync()
    {
        _nc = new NatsClient(_url);
        await _nc.ConnectAsync();
        _js = _nc.CreateJetStreamContext();

        var consumerConfig = new ConsumerConfig
        {
            Name = _durableConsumerName,
            DurableName = _durableConsumerName,
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            FilterSubject = _jobSubject
        };

        var consumer = await _js.CreateOrUpdateConsumerAsync(_streamName, consumerConfig);
        Console.WriteLine($"Worker '{_durableConsumerName}' ready to process jobs.");

        // **✅ `JobCompleted` イベントを使用**
        await foreach (var msg in consumer.ConsumeAsync<Job>())
        {
            var job = msg.Data;
            Console.WriteLine($"Processing job {job.Id}: {job.Description}...");

            // **ジョブ実行 (2秒)**
            await Task.Delay(2000);

            // **ジョブ完了を通知 (デリゲートを使用)**
            var jobResult = new JobResult
            {
                JobId = job.Id,
                Status = "Completed"
            };

            JobCompleted?.Invoke(jobResult);

            // **ジョブの ACK (確認済みとして削除)**
            await msg.AckAsync();
        }
    }
}



// **ジョブデータモデル**
public record Job
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("description")] public string Description { get; init; }
}

// **ジョブ結果データモデル**
public record JobResult
{
    [JsonPropertyName("job_id")] public int JobId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; }
}
