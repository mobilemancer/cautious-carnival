using Microsoft.AspNetCore.Mvc;

namespace tool2;

class Program
{
    const string agentName = "data_sanitizer";
    const string selfURL = "http://localhost:5003";
    const string orchestratorUrl = "http://localhost:5000/register";

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
                Name = "sanitize",
                Description = "Removes sensitive info from text.",
                InputFormat = "text"
            }
                }
            });
        });

        app.MapPost("/task", ([FromBody] TaskRequest req) =>
        {
            string sanitized = req.Text.Replace("user123", "[REDACTED]");
            return new TaskResponse { Result = sanitized, Notes = "Sanitized identifiers." };
        });

        app.Run(selfURL);
    }
}
