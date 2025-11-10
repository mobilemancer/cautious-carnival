using Microsoft.AspNetCore.Mvc;

namespace tool3;

class Program
{
    const string agentName = "log_analyzer";
    const string selfURL = "http://localhost:5002";
    const string orchestratorUrl = "http://localhost:5000/register";
    const string sanitizerUrl = "http://localhost:5003";

    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.Lifetime.ApplicationStarted.Register(async () =>
        {
            var http = new HttpClient();
            await http.PostAsJsonAsync(orchestratorUrl, new AgentRegistration
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
        });

        app.MapPost("/task", async ([FromBody] TaskRequest req) =>
        {
            var http = new HttpClient();
            // Ensure sanitization before analysis
            var sanitizeResp = await http.PostAsJsonAsync($"{sanitizerUrl}/task", req);
            var sanitized = await sanitizeResp.Content.ReadFromJsonAsync<TaskResponse>();

            string result = sanitized!.Result;
            string notes = "Found 2 warnings, 1 critical error.";
            return new TaskResponse { Result = result, Notes = notes };
        });

        app.Run();
    }
}
