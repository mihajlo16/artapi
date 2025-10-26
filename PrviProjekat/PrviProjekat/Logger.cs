using System.Collections.Concurrent;

namespace PrviProjekat;

internal static class Logger
{
    private static readonly BlockingCollection<(ConsoleColor Color, string Line)> _queue = [];
    private static readonly Thread _worker;
    private static volatile bool _isRunning = true;

    static Logger()
    {
        _worker = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "LoggerThread"
        };
        _worker.Start();
    }

    private static void ProcessQueue()
    {
        foreach (var (color, line) in _queue.GetConsumingEnumerable())
        {
            var prev = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(line);
            }
            finally
            {
                Console.ForegroundColor = prev;
            }
        }
    }

    private static void Enqueue(ConsoleColor color, string level, string message)
    {
        if (!_isRunning) return;
        string line = $"[{level,-5}] [{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        _queue.Add((color, line));
    }

    public static void Info(string msg) => Enqueue(ConsoleColor.Green, "INFO", msg);
    public static void Request(string msg) => Enqueue(ConsoleColor.Cyan, "REQ", msg);
    public static void Error(string msg) => Enqueue(ConsoleColor.Red, "ERROR", msg);

    public static void Stop()
    {
        _isRunning = false;
        _queue.CompleteAdding();
        _worker.Join();
    }
}