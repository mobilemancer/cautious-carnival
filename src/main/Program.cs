using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace main;

class Program
{
    private const string selfURL = "http://localhost:5000";
    private static readonly HttpClient sharedHttpClient = new();

    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Main orchestrator started");

        var builder = WebApplication.CreateBuilder(args); // This requires .NET 6+ and Microsoft.AspNetCore.App

        var app = builder.Build();

        var agents = new Dictionary<string, AgentRegistration>();
        var tools = new ConcurrentDictionary<string, AITool>();
        tools.AddOrUpdate("get_logs", AIFunctionFactory.Create(GetLogsFunction), (key, oldValue) => AIFunctionFactory.Create(GetLogsFunction));

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
                instructions: @"You are a helpful agent.
                Use your tools.",
                //Sanitize logs before analyzing them.
                //Analyze the logs before making a report.",
                tools: tools.Values.ToList()
            );

        app.MapPost("/register", ([FromBody] AgentRegistration agent) =>
        {
            agents[agent.Name] = agent;

            if (agent.Tools?.Any() == true)
            {
                foreach (var toolDefinition in agent.Tools)
                {
                    var tool = CreateHttpCallbackTool(agent, toolDefinition);
                    tools.AddOrUpdate(tool.Name, tool, (key, oldValue) => tool);

                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[MCP] Registered tool: {agent.Name} @ {agent.Endpoint}");
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
            return Results.Json(new { answer });
        });

        app.Run(selfURL);
    }

    private static string GetLogsFunction()
    {
        return "Raw log data from user123: error at module X";
    }

    private static AIFunction CreateHttpCallbackTool(AgentRegistration agent, AgentTool toolDefinition)
    {
        var toolName = string.IsNullOrWhiteSpace(toolDefinition.Name) ? agent.Name : toolDefinition.Name;
        var description = string.IsNullOrWhiteSpace(toolDefinition.Description)
            ? $"HTTP callback tool for {agent.Name}."
            : toolDefinition.Description;

        var parameterName = string.IsNullOrWhiteSpace(toolDefinition.ParameterName)
            ? "payload"
            : toolDefinition.ParameterName!;

        var parameterDescription = string.IsNullOrWhiteSpace(toolDefinition.ParameterDescription)
            ? "Task payload forwarded to the registered tool."
            : toolDefinition.ParameterDescription!;

        var callbackTarget = string.IsNullOrWhiteSpace(toolDefinition.CallbackUrl)
            ? agent.Endpoint
            : toolDefinition.CallbackUrl!;

        var callbackUri = NormalizeCallbackUri(callbackTarget);

        var options = new AIFunctionFactoryOptions
        {
            Name = toolName,
            Description = description,
            ConfigureParameterBinding = parameterInfo =>
            {
                if (parameterInfo.ParameterType == typeof(TaskRequest))
                {
                    return new AIFunctionFactoryOptions.ParameterBindingOptions
                    {
                        BindParameter = (_, args) => ExtractTaskRequest(args, parameterName, parameterInfo.Name ?? parameterName)
                    };
                }

                if (parameterInfo.ParameterType == typeof(CancellationToken))
                {
                    return new AIFunctionFactoryOptions.ParameterBindingOptions
                    {
                        ExcludeFromSchema = true
                    };
                }

                return default;
            },
            JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions
            {
                IncludeParameter = parameterInfo => parameterInfo.ParameterType != typeof(CancellationToken),
                TransformSchemaNode = (context, schema) =>
                {
                    if (context.TypeInfo?.Type == typeof(TaskRequest) && schema is JsonObject parameterSchema)
                    {
                        parameterSchema["description"] = parameterDescription;
                    }

                    return schema;
                }
            }
        };

        var function = AIFunctionFactory.Create(
            async (TaskRequest payload, CancellationToken cancellationToken) =>
            {
                using var response = await sharedHttpClient.PostAsJsonAsync(callbackUri, payload, cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<TaskResponse>(cancellationToken: cancellationToken);
                return result ?? new TaskResponse();
            },
            options);

        TypeDescriptor.AddAttributes(function, new DescriptionAttribute(description));

        if (function.UnderlyingMethod is { } methodInfo)
        {
            foreach (var parameter in methodInfo.GetParameters().Where(p => p.ParameterType == typeof(TaskRequest)))
            {
                TypeDescriptor.AddAttributes(parameter, new DescriptionAttribute(parameterDescription));
            }
        }

        return function;
    }

    private static string NormalizeCallbackUri(string callbackTarget)
    {
        if (string.IsNullOrWhiteSpace(callbackTarget))
            throw new InvalidOperationException("Callback URL cannot be empty.");

        var trimmed = callbackTarget.TrimEnd('/');
        return trimmed.EndsWith("/task", StringComparison.OrdinalIgnoreCase) ? trimmed : $"{trimmed}/task";
    }

    private static TaskRequest ExtractTaskRequest(AIFunctionArguments arguments, string desiredName, string fallbackName)
    {
        if (TryReadTaskRequest(arguments, desiredName, out var request))
            return request;

        if (!string.Equals(desiredName, fallbackName, StringComparison.OrdinalIgnoreCase) &&
            TryReadTaskRequest(arguments, fallbackName, out request))
            return request;

        foreach (var key in arguments.Keys)
        {
            if (TryReadTaskRequest(arguments, key, out request))
                return request;
        }

        return new TaskRequest();
    }

    private static bool TryReadTaskRequest(AIFunctionArguments arguments, string key, out TaskRequest request)
    {
        request = new TaskRequest();

        if (!arguments.TryGetValue(key, out var raw) || raw is null)
            return false;

        request = ConvertToTaskRequest(raw);
        return true;
    }

    private static TaskRequest ConvertToTaskRequest(object raw)
    {
        return raw switch
        {
            TaskRequest request => request,
            TaskResponse response => new TaskRequest { Text = response.Result },
            JsonElement jsonElement => jsonElement.ValueKind switch
            {
                JsonValueKind.Object => jsonElement.Deserialize<TaskRequest>() ?? new TaskRequest(),
                JsonValueKind.Null => new TaskRequest(),
                _ => new TaskRequest { Text = jsonElement.ToString() ?? string.Empty }
            },
            JsonNode node => node switch
            {
                JsonObject jsonObject => jsonObject.Deserialize<TaskRequest>() ?? new TaskRequest(),
                JsonValue jsonValue => new TaskRequest { Text = jsonValue.ToString() ?? string.Empty },
                _ => new TaskRequest { Text = node.ToJsonString() }
            },
            string text => new TaskRequest { Text = text },
            _ => JsonSerializer.Deserialize<TaskRequest>(JsonSerializer.Serialize(raw)) ?? new TaskRequest()
        };
    }
}

class PlanRequest
{
    public string Prompt { get; set; } = string.Empty;
}
