using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace tool2;

class Program
{
    const string agentName = "data_sanitizer";
    const string selfURL = "http://localhost:5002";
    const string orchestratorUrl = "http://localhost:5000/register";

    private static readonly Regex UserFieldPattern = new(@"\b(?<key>user[\w-]*)(?<delimiter>\s*:\s*)(?<value>[^\r\n\s,;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UserTokenPattern = new(@"\buser(?!\s*:)[\w-]*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                        Name = "sanitize",
                        Description = "Removes sensitive info from text.",
                        InputFormat = "text",
                        ParameterName = "payload",
                        ParameterDescription = "Raw log content requiring sanitization before sharing.",
                        CallbackUrl = selfURL
                    }
                }
            });
        });

        app.MapPost("/task", ([FromBody] TaskRequest req) =>
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Tool {agentName} called with {req.Text}");

            string sanitized = UserFieldPattern.Replace(req.Text, static match =>
            {
                var key = match.Groups["key"].Value;
                var delimiter = match.Groups["delimiter"].Value;
                return $"{key}{delimiter}[REDACTED]";
            });
            sanitized = UserTokenPattern.Replace(sanitized, "[REDACTED]");

            Console.WriteLine($"Tool {agentName} returning {sanitized}");

            return new TaskResponse { Result = sanitized, Notes = "Sanitized identifiers." };
        });

        app.Run(selfURL);
    }
}
