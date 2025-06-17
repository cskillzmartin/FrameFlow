using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using FrameFlow.App;

namespace FrameFlow.Utilities
{
    /// <summary>
    /// Utility class for managing speaker metadata compression
    /// </summary>
    public static class SpeakerCompressionUtility
    {
        /// <summary>
        /// Compress all existing speaker metadata files in a project
        /// </summary>
        public static async Task<CompressionReport> CompressAllCacheFilesAsync(string projectPath)
        {
            var report = new CompressionReport();
            
            try
            {
                // Find all render directories that might contain speaker.meta.json files
                var renderDirs = Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories)
                    .Where(dir => Path.GetFileName(dir).Contains("render") || 
                                  Path.GetFileName(dir).Contains("output") ||
                                  Directory.GetFiles(dir, "*.speaker.meta.json").Any());

                foreach (var renderDir in renderDirs)
                {
                    var metaFiles = Directory.GetFiles(renderDir, "*.speaker.meta.json");
                    
                    foreach (var metaFile in metaFiles)
                    {
                        var projectName = Path.GetFileNameWithoutExtension(metaFile).Replace(".speaker.meta", "");
                        
                        Debug.WriteLine($"Processing: {metaFile}");
                        
                        // Get original file size
                        var originalSize = new FileInfo(metaFile).Length;
                        
                        // Compress the file
                        var success = await SpeakerManager.Instance.CompressExistingCacheAsync(projectName, renderDir);
                        
                        if (success)
                        {
                            // Get compressed file info
                            var info = await SpeakerManager.Instance.GetCacheFileInfoAsync(projectName, renderDir);
                            
                            if (info != null)
                            {
                                report.ProcessedFiles++;
                                report.OriginalSizeBytes += originalSize;
                                report.CompressedSizeBytes += info.TotalSize;
                                report.EmbeddingFilesCreated++;
                                
                                Debug.WriteLine($"✅ Compressed {Path.GetFileName(metaFile)}: " +
                                              $"{originalSize:N0} → {info.TotalSize:N0} bytes " +
                                              $"({info.CompressionRatio:P1})");
                            }
                        }
                        else
                        {
                            report.FailedFiles++;
                            Debug.WriteLine($"❌ Failed to compress {Path.GetFileName(metaFile)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during compression: {ex.Message}");
                report.ErrorMessage = ex.Message;
            }
            
            return report;
        }

        /// <summary>
        /// Analyze speaker metadata files in a project and show compression potential
        /// </summary>
        public static async Task<AnalysisReport> AnalyzeCompressionPotentialAsync(string projectPath)
        {
            var report = new AnalysisReport();
            
            try
            {
                var metaFiles = Directory.GetFiles(projectPath, "*.speaker.meta.json", SearchOption.AllDirectories);
                
                foreach (var metaFile in metaFiles)
                {
                    var fileInfo = new FileInfo(metaFile);
                    report.TotalFiles++;
                    report.TotalSizeBytes += fileInfo.Length;
                    
                    // Try to load and analyze the file
                    try
                    {
                        var content = await File.ReadAllTextAsync(metaFile);
                        
                        // Count embedding occurrences (rough estimate)
                        var embeddingCount = content.Split("\"embedding\"").Length - 1;
                        var faceCount = content.Split("\"faceId\"").Length - 1;
                        
                        if (embeddingCount > 0)
                        {
                            report.UncompressedFiles++;
                            report.EstimatedEmbeddingData += embeddingCount * 512 * 8; // 512 dims * 8 bytes per float in JSON
                        }
                        else
                        {
                            report.CompressedFiles++;
                        }
                        
                        report.TotalFaces += faceCount;
                        
                        if (fileInfo.Length > 1024 * 1024) // Files larger than 1MB
                        {
                            report.LargeFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error analyzing {metaFile}: {ex.Message}");
                        report.CorruptFiles++;
                    }
                }
                
                // Calculate compression potential
                report.EstimatedCompressionRatio = report.EstimatedEmbeddingData > 0 
                    ? (float)(report.TotalSizeBytes - report.EstimatedEmbeddingData) / report.TotalSizeBytes 
                    : 1.0f;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during analysis: {ex.Message}");
                report.ErrorMessage = ex.Message;
            }
            
            return report;
        }

        /// <summary>
        /// Clean up backup files created during compression
        /// </summary>
        public static async Task<int> CleanupBackupFilesAsync(string projectPath, bool confirmDelete = false)
        {
            int deletedCount = 0;
            
            try
            {
                var backupFiles = Directory.GetFiles(projectPath, "*.speaker.meta.json.backup", SearchOption.AllDirectories);
                
                foreach (var backupFile in backupFiles)
                {
                    if (confirmDelete)
                    {
                        Debug.WriteLine($"Deleting backup: {Path.GetFileName(backupFile)}");
                        File.Delete(backupFile);
                        deletedCount++;
                    }
                    else
                    {
                        Debug.WriteLine($"Found backup file: {backupFile}");
                    }
                }
                
                if (!confirmDelete && backupFiles.Any())
                {
                    Debug.WriteLine($"Found {backupFiles.Length} backup files. Call with confirmDelete=true to delete them.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up backup files: {ex.Message}");
            }
            
            return deletedCount;
        }

        public class CompressionReport
        {
            public int ProcessedFiles { get; set; }
            public int FailedFiles { get; set; }
            public int EmbeddingFilesCreated { get; set; }
            public long OriginalSizeBytes { get; set; }
            public long CompressedSizeBytes { get; set; }
            public string? ErrorMessage { get; set; }
            
            public float CompressionRatio => OriginalSizeBytes > 0 ? (float)CompressedSizeBytes / OriginalSizeBytes : 1.0f;
            public long SpaceSaved => OriginalSizeBytes - CompressedSizeBytes;
            
            public string GetSummary()
            {
                if (ProcessedFiles == 0)
                {
                    return "No files processed" + (string.IsNullOrEmpty(ErrorMessage) ? "" : $": {ErrorMessage}");
                }
                
                return $"Processed {ProcessedFiles} files, {FailedFiles} failed. " +
                       $"Size: {OriginalSizeBytes:N0} → {CompressedSizeBytes:N0} bytes " +
                       $"({CompressionRatio:P1}, saved {SpaceSaved:N0} bytes)";
            }
        }

        public class AnalysisReport
        {
            public int TotalFiles { get; set; }
            public int UncompressedFiles { get; set; }
            public int CompressedFiles { get; set; }
            public int LargeFiles { get; set; }
            public int CorruptFiles { get; set; }
            public long TotalSizeBytes { get; set; }
            public long EstimatedEmbeddingData { get; set; }
            public int TotalFaces { get; set; }
            public float EstimatedCompressionRatio { get; set; }
            public string? ErrorMessage { get; set; }
            
            public string GetSummary()
            {
                if (TotalFiles == 0)
                {
                    return "No speaker metadata files found" + (string.IsNullOrEmpty(ErrorMessage) ? "" : $": {ErrorMessage}");
                }
                
                return $"Found {TotalFiles} files ({TotalSizeBytes:N0} bytes total). " +
                       $"{UncompressedFiles} uncompressed, {CompressedFiles} compressed, {LargeFiles} large (>1MB). " +
                       $"Estimated compression: {EstimatedCompressionRatio:P1} " +
                       $"(could save ~{(TotalSizeBytes * (1 - EstimatedCompressionRatio)):N0} bytes)";
            }
        }
    }
} 