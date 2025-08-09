using System;
using System.IO;
using System.Text.Json;

namespace FrameFlow.Utilities.Agent
{
    public sealed class AgentMemory
    {
        private readonly string _runLogPath;
        private readonly object _fileLock = new();

        public AgentMemory(string renderDirectory)
        {
            Directory.CreateDirectory(renderDirectory);
            _runLogPath = Path.Combine(renderDirectory, "run.jsonl");
        }

        public void Append(RunEvent evt)
        {
            lock (_fileLock)
            {
                using var stream = new FileStream(_runLogPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(JsonSerializer.Serialize(evt));
            }
        }

        public void SaveText(string relativePath, string content)
        {
            var full = Path.Combine(Path.GetDirectoryName(_runLogPath)!, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }
}


