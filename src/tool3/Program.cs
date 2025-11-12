using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace tool3;

class Program
{
    const string agentName = "log_analyzer";
    private const string selfURL = "http://localhost:5003";
    const string orchestratorUrl = "http://localhost:5000/register";

    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        HttpClient http = new();

        app.Lifetime.ApplicationStarted.Register((Action)(async () =>
        {
            await HttpClientJsonExtensions.PostAsJsonAsync<AgentRegistration>(http, orchestratorUrl, new AgentRegistration
            {
                Name = agentName,
                Endpoint = $"{selfURL}",
                Tools = new()
                {
                    new AgentTool
                    {
                        Name = "analyze_logs",
                        Description = "Analyzes sanitized logs for errors.",
                        InputFormat = "text",
                        ParameterName = "payload",
                        ParameterDescription = "Sanitized log data to inspect for anomalies and issues.",
                        CallbackUrl = selfURL
                    }
                }
            });
        }));

        app.MapPost("/task", async ([FromBody] TaskRequest req) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Tool {agentName} called");

            var analysis = AnalyzeLogs(req.Text);
            string notes = BuildSummaryNotes(analysis);

            Console.WriteLine($"Tool {agentName} returning {notes}");

            return new TaskResponse
            {
                Result = analysis.ForwardPayload,
                Notes = notes
            };
        });

        app.Run(selfURL);
    }

    private static LogAnalysis AnalyzeLogs(string raw)
    {
        var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var severityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var criticalFindings = new List<string>();
        string currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 0 && DateTime.TryParse(tokens[0], out var parsedDate))
            {
                currentDate = parsedDate.ToString("yyyy-MM-dd");
            }

            var severityMatch = Regex.Match(trimmed, "\\[(?<severity>[A-Z]+)\\]");
            if (severityMatch.Success)
            {
                var severity = severityMatch.Groups["severity"].Value;
                severityCounts.TryGetValue(severity, out var count);
                severityCounts[severity] = count + 1;

                if (string.Equals(severity, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(severity, "CRITICAL", StringComparison.OrdinalIgnoreCase))
                {
                    criticalFindings.Add(trimmed);
                }
            }

            if (trimmed.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                criticalFindings.Add(trimmed);
            }
        }

        return new LogAnalysis(currentDate, raw, severityCounts, criticalFindings);
    }

    private static string BuildSummaryNotes(LogAnalysis analysis)
    {
        var builder = new StringBuilder();
        var countsSummary = analysis.SeverityCounts.Count == 0
            ? "No severities detected"
            : string.Join(", ", analysis.SeverityCounts.Select(kv => $"{kv.Key}: {kv.Value}"));

        builder.AppendLine($"Date: {analysis.Date}");
        builder.AppendLine($"Severity counts: {countsSummary}");

        if (analysis.CriticalFindings.Count == 0)
        {
            builder.AppendLine("Outstanding exceptions: None");
        }
        else
        {
            builder.AppendLine("Outstanding exceptions:");
            foreach (var finding in analysis.CriticalFindings.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- {finding}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private sealed record LogAnalysis(
        string Date,
        string ForwardPayload,
        IReadOnlyDictionary<string, int> SeverityCounts,
        IReadOnlyList<string> CriticalFindings);
}
