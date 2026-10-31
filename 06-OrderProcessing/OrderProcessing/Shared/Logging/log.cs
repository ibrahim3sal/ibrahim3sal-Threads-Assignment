
namespace OrderProcessing.Shared.Logging;

public static class Log
{
    private static readonly object _lock = new();

    public static void Info(string message)
    {
        Write(message, ConsoleColor.Gray);
    }

    public static void Success(string message)
    {
        Write(message, ConsoleColor.Green);
    }

    public static void Error(string message)
    {
        Write(message, ConsoleColor.Red);
    }

    private static void Write(string message, ConsoleColor color)
    {
        lock (_lock)
        {
            try
            {
                Console.ForegroundColor = color;

                string time = DateTime.Now.ToString("HH:mm:ss.fff");
                int threadId = Environment.CurrentManagedThreadId;

                Console.WriteLine($"[{time}] [Thread {threadId}] {message}");
            }
            finally
            {
                Console.ResetColor();
            }
        }
    }
}
