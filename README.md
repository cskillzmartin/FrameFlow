# FrameFlow

FrameFlow is a professional-grade non-linear video editor powered by AI that helps you create compelling video content from your media library. Using advanced AI models, it analyzes your video content and creates engaging edits based on your prompts.

## Features

- **AI-Powered Video Editing**: Generate video edits based on natural language prompts
- **Smart Content Analysis**: Automatic transcription and content analysis of imported media
- **Flexible Project Management**: Create and manage multiple video editing projects
- **Customizable Weights**: Fine-tune your edits with adjustable parameters:
  - Relevance: How closely the content matches your prompt (0-100)
  - Sentiment: Emotional tone of the content (0-100)
  - Novelty: How unique or surprising the content is (0-100)
  - Energy: Energy level and intensity of the content (0-100)
- **Multi-Format Support**: Import videos in various formats (.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm)
- **Dark Mode**: Comfortable editing in low-light environments

## Dependencies

### Required Software

1. **FFmpeg**: Required for video processing
   - Download from [FFmpeg's official website](https://ffmpeg.org/download.html)
   - Configure paths in Settings → FFmpeg Settings

2. **CUDA and cuDNN**: Required for GPU acceleration
   - CUDA Toolkit 11.8 or compatible version
   - cuDNN 8.9.0 or compatible version
   - Must match the versions supported by the NuGet packages:
     - Microsoft.ML.OnnxRuntimeGenAI.Cuda (v0.8.2)
     - Whisper.net.Runtime.Cuda (v1.8.1)
   - [CUDA Installation Guide](https://docs.nvidia.com/cuda/cuda-installation-guide-microsoft-windows/)
   - [cuDNN Installation Guide](https://docs.nvidia.com/deeplearning/cudnn/install-guide/)

3. **NuGet Packages**: Automatically restored on build
   ```xml
   <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.Cuda" Version="0.8.2" />
   <PackageReference Include="Microsoft.ML.OnnxRuntimeGenAI.DirectML" Version="0.8.2" />
   <PackageReference Include="Whisper.net" Version="1.8.1" />
   <PackageReference Include="Whisper.net.Runtime.Cuda" Version="1.8.1" />
   ```

4. **AI Models**: Required for content analysis
   - Models can be placed in one of three locations (configurable in Settings → AI Settings):
     ```
     %AppData%\FrameFlow\models\cuda     (CUDA-enabled GPUs)
     %AppData%\FrameFlow\models\directml (DirectML-enabled GPUs)
     %AppData%\FrameFlow\models\cpu      (CPU-only processing)
     ```
   - Required Models:
     1. **Phi-3 Mini**: Used for content analysis and generation
        - Download from [microsoft/Phi-3-mini-128k-instruct](https://huggingface.co/microsoft/Phi-3-mini-128k-instruct)
        - Convert to ONNX format for your compute provider (CUDA/DirectML/CPU)
        - Place in the corresponding model directory
     2. **Whisper Base**: Used for audio transcription
        - Download the GGML base model from [Whisper.cpp](https://huggingface.co/ggerganov/whisper.cpp)
        - Place `ggml-base.bin` in the `AI Models/Whisper/` directory
   - The application will automatically use the best available compute provider
   - Model files are included in `.gitignore` and must be downloaded separately

### System Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- NVIDIA GPU with CUDA support (for GPU acceleration)
- DirectX 12 capable GPU (for DirectML support)
- 8GB RAM minimum, 16GB recommended
- SSD storage recommended for media processing

## Getting Started

1. **Environment Setup**:
   - Install .NET 8.0 Runtime
   - Install CUDA Toolkit 11.8
   - Install cuDNN 8.9.0
   - Verify GPU drivers are up to date
   - Configure environment variables:
     - CUDA_PATH
     - CUDA_PATH_V11_8
     - Path (include CUDA binary directories)

2. **First Launch**:
   - Configure FFmpeg paths in Settings
   - Verify AI model locations
   - Set your preferred project and import locations

3. **Creating a Project**:
   - File → New Project
   - Choose a project location
   - Projects are saved with the `.ffproj` extension

4. **Importing Media**:
   - Click "Import Media +" or drag-and-drop files
   - Supported formats: .mp4, .avi, .mkv, .mov, .wmv, .flv, .webm
   - Media files are analyzed automatically (configurable in settings)

5. **Generating Edits**:
   - Enter a prompt describing your desired edit
   - Adjust weights to fine-tune the result
   - Set desired output length
   - Click "Generate" to create your edit

## Project Structure

Projects are organized as follows:
```
ProjectName/
├── project.ffproj       # Project configuration
├── Media/              # Imported media files
├── Transcripts/        # Generated transcripts
├── Thumbnails/        # Media thumbnails
└── Renders/           # Generated video edits
```

## Settings

All application settings are configurable through Settings (File → Settings):

- **FFmpeg Settings**: Paths to FFmpeg and FFprobe executables
- **Project Settings**: Default locations and recent projects
- **Import Settings**: Auto-analysis, thumbnails, supported formats
- **UI Settings**: Dark mode, language, thumbnail preferences
- **AI Settings**: Model paths and compute preferences

## Upcoming Features

1. **Import Queue**:
   - Background processing for high-volume imports
   - Progress tracking and notifications
   - Batch import optimization

2. **Enhanced User Feedback**:
   - Detailed progress indicators
   - Time remaining estimates
   - Resource usage monitoring
   - Error recovery suggestions

3. **Advanced Analysis**:
   - Scene detection
   - Face recognition
   - Object tracking
   - Audio analysis
   - Emotion detection

## Troubleshooting

1. **Model Loading Issues**:
   - Verify model files exist in the configured directories
   - Check compute provider compatibility
   - Review debug output for specific errors
   - Verify CUDA/cuDNN installation and versions
   - Check GPU compatibility and drivers

2. **Import Failures**:
   - Verify FFmpeg installation
   - Check file permissions
   - Ensure sufficient disk space
   - Review supported formats

3. **Generation Issues**:
   - Check available system resources
   - Verify media analysis completion
   - Review prompt guidelines
   - Monitor GPU memory usage
   - Check CUDA runtime errors in debug output


## 🗺️ Processing Pipeline Overview
(raw media)
   │
   ▼
┌─────────────────────┐
│ 1. Ingest           │  → copy/normalise assets, update manifest
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 2. Transcribe       │  → Whisper transcription
│    (+ Diarise)      │
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 3. Take Layer       │  → detect repeated reads, pick best-take
│                     │    
└─────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 4. Speaker / Shot Tagging  │  → `speakerId`, `shotLabel`, `faceIds`
└────────────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 5. Vector Builder   │  → build 4-axis vectors (relevance, sentiment,
│                     │    novelty, energy) and store in ANN index
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 6. Scalar Ranking   │  → ANN top-K search with user weight vector
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 7. Diversity (MMR)  │  → re-rank for novelty, output shortlist
└─────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 8. Dialogue Sequencer      │  → alternate speakers, reply matching,
│                            │    
└────────────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 9. Energy-Based Expansion  │  → ±Δ seconds, sentence-boundary snap
└────────────────────────────┘
   │
   ▼
┌────────────────────────────┐
│10. Hard Constraints        │  → runtime caps, deterministic ordering
└────────────────────────────┘
   │
   ▼
┌─────────────────────┐
│11. Render           │  → generate FFmpeg concat,
│                     │    apply transitions, subtitles
└─────────────────────┘