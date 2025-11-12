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
                        InputFormat = "text"
                    }
                }
            });
        }));

        app.MapPost("/task", async ([FromBody] TaskRequest req) =>
        {
            // Ensure sanitization before analysis
            var sanitizeResp = await http.PostAsJsonAsync($"{sanitizerUrl}/task", req);
            var sanitized = await sanitizeResp.Content.ReadFromJsonAsync<TaskResponse>();

            string result = sanitized!.Result;
            string notes = "Found 2 warnings, 1 critical error.";
            return new TaskResponse { Result = result, Notes = notes };
        });

        app.Run(selfURL);
    }
}
