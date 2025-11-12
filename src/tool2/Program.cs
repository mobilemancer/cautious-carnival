using Microsoft.AspNetCore.Mvc;

namespace tool2;

class Program
{
    const string agentName = "data_sanitizer";
    const string selfURL = "http://localhost:5002";
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
            string sanitized = req.Text.Replace("user123", "[REDACTED]");
            return new TaskResponse { Result = sanitized, Notes = "Sanitized identifiers." };
        });

        app.Run(selfURL);
    }
}
