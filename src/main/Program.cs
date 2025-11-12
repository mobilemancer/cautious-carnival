using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace main;

class Program
{
    private const string selfURL = "http://localhost:5000";
    private static readonly HttpClient sharedHttpClient = new();

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Main orchestrator started");

        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();

        var agents = new ConcurrentDictionary<string, AgentRegistration>();
        var tools = new ConcurrentDictionary<string, AITool>();
        tools.TryAdd("get_logs", AIFunctionFactory.Create(Tools.GetLogsFunction));

        string endpoint =
            Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-uri")
            ?? throw new InvalidOperationException("Missing Azure OpenAI endpoint.");

        string apiKey =
            Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-key")
            ?? throw new InvalidOperationException("Missing Azure OpenAI key.");

        AIAgent logParserAgent = Agents.CreateLogParserAgent(endpoint, apiKey, tools.Values);

        app.MapPost("/register", ([FromBody] AgentRegistration agent) =>
        {
            agents[agent.Name] = agent;

            if (agent.Tools?.Any() == true)
            {
                foreach (var toolDefinition in agent.Tools)
                {
                    var tool = ToolHelpers.CreateHttpCallbackTool(agent, toolDefinition, sharedHttpClient);
                    tools.AddOrUpdate(tool.Name, tool, (key, oldValue) => tool);
                }

                logParserAgent = Agents.CreateLogParserAgent(endpoint, apiKey, tools.Values);
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Registered tool: {agent.Name} @ {agent.Endpoint}");
            return Results.Ok();
        });


        app.MapPost("/plan-and-run", async ([FromBody] PlanRequest request) =>
        {
            string prompt = request.Prompt;
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"[MAIN] Received request: {prompt}");

            AgentRunResponse answer = await logParserAgent.RunAsync(prompt);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Agent: {answer}");

            // var firstMessage = answer.Messages?.FirstOrDefault();
            // if (firstMessage is null)
            // {
            //     firstMessage = "Empty response";
            // }

            // return Results.Text(firstMessage is null ?? "Empty response" : firstMessage);

            // return Results.Json(new { answer });
            return Results.Text(answer.ToString());
        });

        app.Run(selfURL);
    }
}
