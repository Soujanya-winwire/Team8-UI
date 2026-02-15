using Serilog;
using Serilog.Events;

namespace AgenticAI.Core.Logging
{
    /// <summary>
    /// Centralized logger for the framework
    /// </summary>
    public static class Logger
    {
        private static ILogger? _logger;
        private static readonly object _lock = new object();

        public static void Initialize(string logPath = "TestResults/Logs")
        {
            if (_logger != null) return;

            lock (_lock)
            {
                if (_logger != null) return;

                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                var logFileName = Path.Combine(logPath, $"TestRun_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console(
                        restrictedToMinimumLevel: LogEventLevel.Information,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        logFileName,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                Log.Logger = _logger;
            }
        }

        public static void Info(string message) => Log.Information(message);
        public static void Debug(string message) => Log.Debug(message);
        public static void Warning(string message) => Log.Warning(message);
        public static void Error(string message) => Log.Error(message);
        public static void Error(Exception ex, string message) => Log.Error(ex, message);
        public static void Fatal(string message) => Log.Fatal(message);
        public static void Fatal(Exception ex, string message) => Log.Fatal(ex, message);

        public static void StepInfo(string stepName, string message) =>
            Log.Information($"[STEP: {stepName}] {message}");

        public static void TestInfo(string testName, string message) =>
            Log.Information($"[TEST: {testName}] {message}");

        public static void Close()
        {
            Log.CloseAndFlush();
        }
    }
}
