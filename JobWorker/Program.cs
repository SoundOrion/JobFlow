// **プログラムのエントリーポイント**
using NATS.Net;

class Program
{
    static async Task Main()
    {
        string url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";
        var jobWorker = new JobWorker(url);

        // **✅ `JobCompleted` に処理を登録**
        jobWorker.JobCompleted += SendJobResultToManager;
        jobWorker.JobCompleted += LogJobCompletion;

        await jobWorker.StartAsync();
    }

    // **JobManager にジョブ結果を送信**
    static async void SendJobResultToManager(JobResult result)
    {
        string url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";
        await using var nc = new NatsClient(url);
        await nc.ConnectAsync();

        await nc.PublishAsync(subject: "jobs.result", data: result);
        Console.WriteLine($"✅ Job {result.JobId} completed. Sent result to JobManager.");
    }

    // **ジョブ完了ログを記録**
    static void LogJobCompletion(JobResult result)
    {
        Console.WriteLine($"📁 Job {result.JobId} finished with status: {result.Status} (Logged)");
    }
}