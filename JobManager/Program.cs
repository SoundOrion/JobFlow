using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://127.0.0.1:4222";
        var jobManager = new JobManager(url);

        // **✅ デリゲート風のイベント登録 (`+=` で追加)**
        jobManager.JobCompleted += ConsoleOutputHandler;
        //jobManager.JobCompleted += LogToFileHandler;
        //jobManager.JobCompleted += SaveToDatabaseHandler;

        // **ジョブマネージャー開始**
        await jobManager.StartAsync();
    }

    // **コンソールに出力するハンドラー**
    static void ConsoleOutputHandler(JobResult result)
    {
        Console.WriteLine($"✅ Console Output: Job {result.JobId} completed with status: {result.Status}");
    }

    // **ファイルにログを記録するハンドラー**
    static void LogToFileHandler(JobResult result)
    {
        string logFilePath = "job_results.log";
        File.AppendAllText(logFilePath, $"[{DateTime.Now}] Job {result.JobId} - {result.Status}{Environment.NewLine}");
        Console.WriteLine($"📁 Logged to file: {logFilePath}");
    }

    // **データベースに保存するハンドラー**
    static void SaveToDatabaseHandler(JobResult result)
    {
        Console.WriteLine($"💾 Saving to Database: Job {result.JobId}, Status: {result.Status}");
    }
}
