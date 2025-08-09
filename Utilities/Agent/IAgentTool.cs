using System.Threading.Tasks;

namespace FrameFlow.Utilities.Agent
{
    public interface IAgentTool
    {
        string Name { get; }
        Task ExecuteAsync(AgentRequest request);
    }
}


