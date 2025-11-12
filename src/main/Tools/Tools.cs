internal class Tools
{
    internal static string GetLogsFunction(string scope)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Tool {nameof(GetLogsFunction)} called with {scope}");

        var result = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ".data", "complexLog.txt"));

        Console.WriteLine($"Tool {nameof(GetLogsFunction)} returning {result}");

        return result;
    }
}