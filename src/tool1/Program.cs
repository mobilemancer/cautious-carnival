using Microsoft.AspNetCore.Mvc;

namespace tool1;

class Program
{
    const string agentName = "report_generator";
    const string selfURL = "http://localhost:5001";
    const string orchestratorUrl = "http://localhost:5000/register";
    const string analyzerUrl = "http://localhost:5006";
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
                        InputFormat = "text"
                    }
                }
            });
        });

        app.MapPost("/task", async ([FromBody] TaskRequest req) =>
        {
            var analysisResp = await http.PostAsJsonAsync($"{analyzerUrl}/task", req);
            var analysis = await analysisResp.Content.ReadFromJsonAsync<TaskResponse>();

            string report = $"Executive Summary:\n- {analysis!.Notes}\n[Visual asset placeholder]";
            return new TaskResponse { Result = report, Notes = "Generated report and visual asset." };
        });

        app.Run(selfURL);

    }
}
