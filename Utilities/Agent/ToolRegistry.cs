using System;
using System.Collections.Generic;

namespace FrameFlow.Utilities.Agent
{
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string, IAgentTool> _toolsByName = new(StringComparer.OrdinalIgnoreCase);

        public void Register(IAgentTool tool)
        {
            _toolsByName[tool.Name] = tool;
        }

        public IAgentTool Get(string name)
        {
            if (_toolsByName.TryGetValue(name, out var tool)) return tool;
            throw new KeyNotFoundException($"Tool not found: {name}");
        }

        public IReadOnlyDictionary<string, IAgentTool> All => _toolsByName;
    }
}


