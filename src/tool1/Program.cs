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

            string report = $"Executive Summary:\n- {req.Text}\n[Visual asset placeholder]";

            Console.WriteLine($"Tool {agentName} returning {report}");

            return new TaskResponse { Result = report, Notes = "Generated report and visual asset." };
        });

        app.Run(selfURL);

    }
}
