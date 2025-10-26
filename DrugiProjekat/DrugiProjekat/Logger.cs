using System.Threading.Channels;

namespace DrugiProjekat;

internal static class Logger
{
    private static readonly Channel<(ConsoleColor Color, string Message)> _channel =
        Channel.CreateUnbounded<(ConsoleColor, string)>();

    private static Task? _processingTask;

    public static void Start(CancellationToken token)
    {
        _processingTask = ProcessQueueAsync(token);
    }

    private static async Task ProcessQueueAsync(CancellationToken token)
    {
        await foreach (var (color, message) in _channel.Reader.ReadAllAsync(token))
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = prev;
        }
    }

    private static void Enqueue(ConsoleColor color, string level, string message)
    {
        if (!_channel.Writer.TryWrite((color, $"[{DateTime.Now:HH:mm:ss}] [{level,-5}] {message}")))
            return;
    }

    public static void Info(string msg) => Enqueue(ConsoleColor.Green, "INFO", msg);
    public static void Request(string msg) => Enqueue(ConsoleColor.Cyan, "REQ", msg);
    public static void Error(string msg) => Enqueue(ConsoleColor.Red, "ERROR", msg);

    public static async Task StopAsync()
    {
        _channel.Writer.TryComplete();
        if (_processingTask is not null)
            await _processingTask;
    }
}