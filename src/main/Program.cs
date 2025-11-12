using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Azure.AI.OpenAI;
using OpenAI;

namespace main;

class Program
{
    private const string selfURL = "http://localhost:5000";

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Main orchestrator started");

        var builder = WebApplication.CreateBuilder(args); // This requires .NET 6+ and Microsoft.AspNetCore.App

        var app = builder.Build();

        var agents = new Dictionary<string, AgentRegistration>();

        string endpoint =
            Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-uri")
            ?? throw new InvalidOperationException("Missing Azure OpenAI endpoint.");

        string apiKey =
            Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-key")
            ?? throw new InvalidOperationException("Missing Azure OpenAI key.");

        AIAgent logParserAgent = new AzureOpenAIClient(
            new Uri(endpoint),
            new System.ClientModel.ApiKeyCredential(apiKey)
        )
            .GetChatClient("gpt-4.1") //chose your model
            .CreateAIAgent(
                name: "Log parser",
                instructions: @"You help the user parse logs.
                Use your tools. 
                Sanitize logs before analyzing them.
                Analyze the logs before making a report."
            );

        // var response = await logParserAgent.RunAsync("Tell me a joke about programmers.");

        app.MapPost("/register", ([FromBody] AgentRegistration agent) =>
        {
            agents[agent.Name] = agent;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[MCP] Registered agent: {agent.Name} @ {agent.Endpoint}");
            return Results.Ok();
        });


        app.MapPost("/plan-and-run", async ([FromBody] PlanRequest request) =>
        {
            string prompt = request.Prompt;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"[MAIN] Received request: {prompt}");

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

            Console.WriteLine(await logParserAgent.RunAsync($"prompt: {prompt}, data: {currentData}"));


            foreach (var agentName in plan)
            {
                var agent = agents[agentName];
                var response = await http.PostAsJsonAsync($"{agent.Endpoint}/task", currentData);
                var result = await response.Content.ReadFromJsonAsync<TaskResponse>();
                results[agentName] = result!;
                currentData = new TaskRequest { Text = result!.Result };
            }

            Console.WriteLine(await logParserAgent.RunAsync(prompt));

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
