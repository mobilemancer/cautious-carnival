using Microsoft.AspNetCore.Mvc;

namespace tool3;

class Program
{
    const string agentName = "log_analyzer";
    private const string selfURL = "http://localhost:5003";
    const string orchestratorUrl = "http://localhost:5000/register";
    const string sanitizerUrl = "http://localhost:5003";

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

            string notes = "Found 2 warnings, 1 critical error.";

            Console.WriteLine($"Tool {agentName} returning {notes}");

            return new TaskResponse { Notes = notes };
        });

        app.Run(selfURL);
    }
}
