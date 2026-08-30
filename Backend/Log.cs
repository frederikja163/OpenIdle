using System;

namespace Backend;

internal static class Log
{
    public static void Debug(object obj)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Message("DEB", obj);
    }
    
    public static void Info(object obj)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Message("INF", obj);
    }

    public static void Warning(object obj)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Message("WRN", obj);
    }

    public static void Error(object obj)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Message("ERR", obj);
    }

    private static void Message(string severity, object obj)
    {
        DateTime time = DateTime.UtcNow;
        Console.WriteLine($"{time:s} | {severity} | {obj}");
    }
}