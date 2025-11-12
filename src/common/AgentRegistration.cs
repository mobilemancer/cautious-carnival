public class AgentRegistration
{
    public string Name { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public List<AgentTool> Tools { get; set; } = new();
}
