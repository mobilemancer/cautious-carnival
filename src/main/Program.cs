using Microsoft.AspNetCore.Mvc;

namespace main;

class Program
{
    private const string selfURL = "http://localhost:5000";

    static void Main(string[] args)
    {
        Console.WriteLine("Main orchestrator started");

        var builder = WebApplication.CreateBuilder(args); // This requires .NET 6+ and Microsoft.AspNetCore.App

        var app = builder.Build();

        var agents = new Dictionary<string, AgentRegistration>();

        app.MapPost("/register", ([FromBody] AgentRegistration agent) =>
        {
            agents[agent.Name] = agent;
            Console.WriteLine($"[MCP] Registered agent: {agent.Name} @ {agent.Endpoint}");
            return Results.Ok();
        });


        app.MapPost("/plan-and-run", async ([FromBody] PlanRequest request) =>
        {
            string prompt = request.Prompt;
            Console.WriteLine($"[MCP] Received request: {prompt}");

            // --- Simple Planner ---
            var plan = new List<string>();
            if (prompt.Contains("sanitize", StringComparison.OrdinalIgnoreCase))
                plan.Add("data_sanitizer");
            if (prompt.Contains("analyze", StringComparison.OrdinalIgnoreCase))
                plan.Add("log_analyzer");
            if (prompt.Contains("report", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("summary", StringComparison.OrdinalIgnoreCase))
                plan.Add("report_generator");

            var results = new Dictionary<string, TaskResponse>();
            var http = new HttpClient();

            var currentData = new TaskRequest { Text = "Raw log data from user123: error at module X" };

            foreach (var agentName in plan)
            {
                var agent = agents[agentName];
                var response = await http.PostAsJsonAsync($"{agent.Endpoint}/task", currentData);
                var result = await response.Content.ReadFromJsonAsync<TaskResponse>();
                results[agentName] = result!;
                currentData = new TaskRequest { Text = result!.Result };
            }

            var summary = string.Join("\n", results.Select(r => $"{r.Key}: {r.Value.Notes}"));
            return Results.Json(new { summary, results });
        });

        app.Run(selfURL);
    }
}

class PlanRequest
{
    public string Prompt { get; set; }
}
