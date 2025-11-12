using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace tool1;

class Program
{
    const string agentName = "report_generator";
    const string selfURL = "http://localhost:5001";
    const string orchestratorUrl = "http://localhost:5000/register";
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        HttpClient http = new();

        app.Lifetime.ApplicationStarted.Register(async () =>
        {
            await http.PostAsJsonAsync(orchestratorUrl, new AgentRegistration
            {
                Name = agentName,
                Endpoint = $"{selfURL}",
                Tools = new()
                {
                    new AgentTool
                    {
                        Name = "generate_report",
                        Description = "Summarizes log analysis results into report.",
                        InputFormat = "text",
                        ParameterName = "payload",
                        ParameterDescription = "Log analysis findings to include in the final report.",
                        CallbackUrl = selfURL
                    }
                }
            });
        });

        app.MapPost("/task", async ([FromBody] TaskRequest req) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Tool {agentName} called");

            string report = BuildSummaryReport(req.Text);

            Console.WriteLine($"Tool {agentName} returning {report}");

            return new TaskResponse { Result = report, Notes = "Generated report and visual asset." };
        });

        app.Run(selfURL);

    }

    private static string BuildSummaryReport(string raw)
    {
        var lines = raw.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var severityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var exceptions = new List<string>();
        var severityExceptions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var dateToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (DateTime.TryParse(dateToken, out var parsedDate))
            {
                currentDate = parsedDate.ToString("yyyy-MM-dd");
            }

            var severityMatch = Regex.Match(trimmed, "\\[(?<severity>[A-Z]+)\\]");
            string? lineSeverity = null;
            if (severityMatch.Success)
            {
                lineSeverity = severityMatch.Groups["severity"].Value.ToUpperInvariant();
                var severity = lineSeverity;
                severityCounts.TryGetValue(severity, out var count);
                severityCounts[severity] = count + 1;
            }

            if (trimmed.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                exceptions.Add(trimmed);
                if (!string.IsNullOrEmpty(lineSeverity))
                {
                    if (!severityExceptions.TryGetValue(lineSeverity, out var list))
                    {
                        list = new List<string>();
                        severityExceptions[lineSeverity] = list;
                    }
                    list.Add(trimmed);
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("| Date | Severity | Count | Outstanding Exceptions |");
        sb.AppendLine("| --- | --- | --- | --- |");

        if (severityCounts.Count == 0)
        {
            var exceptionSummary = exceptions.Count == 0 ? "None" : string.Join("<br>", exceptions);
            sb.AppendLine($"| {currentDate} | None | 0 | {exceptionSummary} |");
            return sb.ToString();
        }

        var orderedSeverities = severityCounts.Keys
            .OrderBy(severity => severity switch
            {
                "CRITICAL" => 0,
                "FATAL" => 1,
                "ERROR" => 2,
                "WARN" => 3,
                "WARNING" => 4,
                "INFO" => 5,
                "DEBUG" => 6,
                "TRACE" => 7,
                _ => 8
            })
            .ThenBy(severity => severity, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var severity in orderedSeverities)
        {
            var count = severityCounts[severity];
            var exceptionSummary = severityExceptions.TryGetValue(severity, out var scopedExceptions) && scopedExceptions.Count > 0
                ? string.Join("<br>", scopedExceptions)
                : "None";

            sb.AppendLine($"| {currentDate} | {severity.ToUpperInvariant()} | {count} | {exceptionSummary} |");
        }

        return sb.ToString();
    }
}
