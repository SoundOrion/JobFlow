# JobManager & JobWorker

## 概要

このプロジェクトは、NATSを使用してジョブの送信、処理、結果報告を行うシンプルなジョブ管理システムです。

- **JobManager**: ジョブを作成し、ジョブワーカーからの結果を監視します。
- **JobWorker**: ジョブを消費して処理し、その結果をJobManagerに返します。

## 特徴

- **NATS JetStream** を使用したメッセージの配信とワークキューの管理。
- JobManagerとJobWorkerは独立して動作し、スケーラブルなジョブ処理が可能。
- メッセージの耐久性と明示的なACKポリシーで信頼性を向上。

## 必要な環境

- .NET 6 以上
- [NATSサーバ](https://nats.io/) (JetStream対応)
- NuGet パッケージ: `NATS.Net`

## インストール方法

1. 必要なNuGetパッケージをインストール:

```bash
$ dotnet add package NATS.Net
```

2. 必要なプロジェクトを準備:
   - **JobManager**
   - **JobWorker**

## JobManager の概要

JobManagerは以下の処理を行います:

1. **ジョブストリームを作成**: `jobs.pending`サブジェクトを持つストリームを作成。
2. **ジョブを発行**: 処理すべきジョブを`jobs.pending`に送信。
3. **ジョブ結果のリスニング**: `jobs.result`サブジェクトを監視し、ジョブの完了通知を受け取ります。

### 使用方法

JobManagerのコード:

```csharp
// NATSサーバURL
var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";

await using var nc = new NatsClient(url);
await nc.ConnectAsync();

var js = nc.CreateJetStreamContext();

// ストリームとサブジェクトの設定
var streamName = "JOB_STREAM";
var jobSubject = "jobs.pending";
var resultSubject = "jobs.result";

// ストリームの作成
var streamConfig = new StreamConfig(name: streamName, subjects: [jobSubject])
{
    Retention = StreamConfigRetention.Workqueue,
    MaxMsgs = 1000,
    MaxBytes = 1024 * 1024,
    MaxAge = TimeSpan.FromHours(24)
};
await js.CreateStreamAsync(streamConfig);

// ジョブの発行
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 1, Description = "Process Data A" });
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 2, Description = "Process Data B" });
await js.PublishAsync(subject: jobSubject, data: new Job { Id = 3, Description = "Process Data C" });

// 結果のリスニング
var cts = new CancellationTokenSource();
await foreach (var msg in nc.SubscribeAsync<JobResult>(resultSubject, cancellationToken: cts.Token))
{
    Console.WriteLine($"Job {msg.Data.JobId} completed with status: {msg.Data.Status}");
}
```

## JobWorker の概要

JobWorkerは以下の処理を行います:

1. **ジョブを消費**: `jobs.pending`からジョブを取得。
2. **ジョブを処理**: ジョブ内容を実行。
3. **結果を報告**: `jobs.result`サブジェクトに処理結果を送信。

### 使用方法

JobWorkerのコード:

```csharp
// NATSサーバURL
var url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";

await using var nc = new NatsClient(url);
await nc.ConnectAsync();

var js = nc.CreateJetStreamContext();

// ストリームとサブジェクトの設定
var streamName = "JOB_STREAM";
var jobSubject = "jobs.pending";
var resultSubject = "jobs.result";
var durableConsumerName = "worker";

// コンシューマの作成
var consumerConfig = new ConsumerConfig
{
    Name = durableConsumerName,
    DurableName = durableConsumerName,
    AckPolicy = ConsumerConfigAckPolicy.Explicit,
    FilterSubject = jobSubject
};

var consumer = await js.CreateOrUpdateConsumerAsync(streamName, consumerConfig);

// ジョブの消費
await foreach (var msg in consumer.ConsumeAsync<Job>())
{
    var job = msg.Data;
    Console.WriteLine($"Processing job {job.Id}: {job.Description}...");

    // ジョブ処理のシミュレーション
    await Task.Delay(2000);

    // 結果の送信
    await nc.PublishAsync(subject: resultSubject, data: new JobResult
    {
        JobId = job.Id,
        Status = "Completed"
    });

    // ACK送信
    await msg.AckAsync();
}
```

## データモデル

### Job

```csharp
public record Job
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("description")] public string Description { get; init; }
}
```

### JobResult

```csharp
public record JobResult
{
    [JsonPropertyName("job_id")] public int JobId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; }
}
```

## 実行方法

1. NATSサーバを起動します。

```bash
$ nats-server --jetstream
```

2. JobManagerを実行してジョブを発行します。

```bash
$ dotnet run --project JobManager
```

3. JobWorkerを実行してジョブを処理します。

```bash
$ dotnet run --project JobWorker
```

## 貢献

バグ報告や機能リクエストは [Issues](https://github.com/your-repository/issues) で受け付けています。

## ライセンス

このプロジェクトはMITライセンスのもとで提供されています。

