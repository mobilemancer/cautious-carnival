using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

internal class Agents
{
    internal static AIAgent CreateLogParserAgent(string endpoint, string apiKey, IEnumerable<AITool> toolSet)
    {
        var resolvedTools = toolSet?.ToList() ?? new List<AITool>();

        return new AzureOpenAIClient(
                new Uri(endpoint),
                new System.ClientModel.ApiKeyCredential(apiKey))
            .GetChatClient("gpt-4o") //gpt-4.1, gpt-5-mini
            .CreateAIAgent(
                name: "Log parser",
                instructions: @"You are a helpful agent.
                Use your tools.
                You are not allowed to show unredacted logs!",
                tools: resolvedTools);
    }
}