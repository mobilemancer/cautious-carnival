internal class Tools
{
    internal static string GetLogsFunction(string scope)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Tool {nameof(GetLogsFunction)} called with {scope}");

        var result = "\"Raw log data from user123: error at module X\"";

        Console.WriteLine($"Tool {nameof(GetLogsFunction)} returning {result}");

        return result;
    }
}