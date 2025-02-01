using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using NATS.Net;

class JobManager
{
    private NatsClient? _nc;
    private INatsJSContext? _js;
    private readonly string _streamName = "JOB_STREAM";
    private readonly string _jobSubject = "jobs.pending";
    private readonly string _resultSubject = "jobs.result";
    private readonly CancellationTokenSource _cts = new();

    private readonly string _url;

    // **✅ デリゲート風のイベントを追加**
    public event Action<JobResult>? JobCompleted;

    public JobManager(string url)
    {
        _url = url;
    }

    public async Task StartAsync()
    {
        _nc = new NatsClient(_url);
        await _nc.ConnectAsync();
        _js = _nc.CreateJetStreamContext();

        await SetupStream();
        await PublishJobs();
        Console.WriteLine("Waiting for job results...");

        // **✅ `SubscribeAsync` をデリゲート風に実行**
        await SubscribeToJobResultsAsync();
    }

    private async Task SetupStream()
    {
        //Console.WriteLine("Deleting stream...");
        //await _js.DeleteStreamAsync(_streamName);
        //Console.WriteLine($"Stream '{_streamName}' deleted.");

        var streamConfig = new StreamConfig(name: _streamName, subjects: [_jobSubject])
        {
            Retention = StreamConfigRetention.Limits,
            MaxMsgs = 1000,
            MaxBytes = 1024 * 1024,
            MaxAge = TimeSpan.FromMinutes(10)
        };

        await _js.CreateStreamAsync(streamConfig);
        Console.WriteLine($"Stream '{_streamName}' created.");
    }

    private async Task PublishJobs()
    {
        Console.WriteLine("Publishing job commands...");
        await PublishJob(new Job { Id = 1, Description = "Process Data A" });
        await PublishJob(new Job { Id = 2, Description = "Process Data B" });
        await PublishJob(new Job { Id = 3, Description = "Process Data C" });
        Console.WriteLine("Jobs published.");
    }

    private async Task PublishJob(Job job)
    {
        await _js.PublishAsync(subject: _jobSubject, data: job);
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    // **✅ `SubscribeAsync` をデリゲート化**
    private async Task SubscribeToJobResultsAsync()
    {
        await foreach (var msg in _nc.SubscribeAsync<JobResult>(_resultSubject, cancellationToken: _cts.Token))
        {
            try
            {
                JobCompleted?.Invoke(msg.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Handler Error: {ex.Message}");
            }
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
