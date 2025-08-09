using System.Threading.Tasks;

namespace FrameFlow.Utilities.Agent.Tools
{
    using FrameFlow.Utilities;

    internal sealed class ProcessTakesTool : IAgentTool
    {
        public string Name => "ProcessTakeLayer";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await TakeManager.Instance.ProcessTakeLayerAsync(request.StorySettings, request.RenderDirectory);
        }
    }

    internal sealed class SpeakerAnalysisTool : IAgentTool
    {
        public string Name => "ProcessSpeakerAnalysis";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await SpeakerManager.Instance.ProcessSpeakerAnalysisAsync(request.ProjectName, request.RenderDirectory);
        }
    }

    internal sealed class RankTranscriptsTool : IAgentTool
    {
        public string Name => "RankTranscripts";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await StoryManager.Instance.RankProjectTranscriptsAsync(
                App.ProjectHandler.Instance.CurrentProject!,
                request.StorySettings,
                request.RenderDirectory);
        }
    }

    internal sealed class RankOrderTool : IAgentTool
    {
        public string Name => "RankOrder";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await StoryManager.Instance.RankOrder(
                request.ProjectName,
                new StoryManager.RankingWeights(
                    request.StorySettings.Relevance,
                    request.StorySettings.Sentiment,
                    request.StorySettings.Novelty,
                    request.StorySettings.Energy,
                    focus: 0f,
                    clarity: 0f,
                    emotion: 0f,
                    flubScore: 0f,
                    compositeScore: 100f
                ),
                request.RenderDirectory);
        }
    }

    internal sealed class NoveltyReRankTool : IAgentTool
    {
        public string Name => "NoveltyReRank";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await StoryManager.Instance.NoveltyReRank(
                request.ProjectName,
                request.StorySettings.Novelty,
                request.RenderDirectory);
        }
    }

    internal sealed class DialogueSequenceTool : IAgentTool
    {
        public string Name => "SequenceDialogue";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await DialogueManager.Instance.SequenceDialogueAsync(
                request.ProjectName,
                request.StorySettings.Novelty,
                request.RenderDirectory);
        }
    }

    internal sealed class TemporalExpansionTool : IAgentTool
    {
        public string Name => "TemporalExpansion";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await StoryManager.Instance.TemporalExpansion(
                request.ProjectName,
                request.StorySettings.TemporalExpansion,
                request.RenderDirectory);
        }
    }

    internal sealed class TrimToLengthTool : IAgentTool
    {
        public string Name => "TrimToLength";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await StoryManager.Instance.TrimRankOrder(
                request.ProjectName,
                request.TargetMinutes,
                request.RenderDirectory);
        }
    }

    internal sealed class RenderVideoTool : IAgentTool
    {
        public string Name => "RenderVideo";
        public async Task ExecuteAsync(AgentRequest request)
        {
            await RenderManager.Instance.RenderVideoAsync(
                request.ProjectName,
                request.OutputVideoPath,
                request.RenderDirectory);
        }
    }
}


