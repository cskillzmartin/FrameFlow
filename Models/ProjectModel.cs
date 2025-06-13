using System.Text.Json.Serialization;

namespace FrameFlow.Models
{
    public class ProjectModel
    {
        [JsonConstructor]
        public ProjectModel() 
        {
            Name = string.Empty;
            Description = string.Empty;
            CreatedDate = DateTime.Now;
            LastModifiedDate = DateTime.Now;
            MediaFiles = new List<MediaFileInfo>();
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public List<MediaFileInfo> MediaFiles { get; set; }
        public string Prompt { get; set; }
        public bool AutoGenerateThumbnails { get; set; } = true;
        public int ThumbnailInterval { get; set; } = 10;
    }

    public class MediaFileInfo
    {
        [JsonConstructor]
        public MediaFileInfo()
        {
            FileName = string.Empty;
            RelativePath = string.Empty;
            FileHash = string.Empty;
        }

        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public string FileHash { get; set; }
        public long FileSize { get; set; }
        public DateTime ImportDate { get; set; }
        public TimeSpan Duration { get; set; }
        public bool HasThumbnails { get; set; }
        public bool HasTranscription { get; set; }
        public VideoMetaData? VideoMetaData { get; set; }
    }
} 