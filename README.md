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

# FrameFlow Updated Workflow - Quality Scoring Integration
```
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
┌──────────────────────────────────────┐
│ 3. Take Layer + Quality Vector       │  → QualityScorer: 9-dimensional assessment
│                                      │    • Relevance 0-100 (LLM)
│                                      │    • Sentiment 0-100 (LLM) 
│                                      │    • Novelty 0-100 (LLM)
│                                      │    • Energy 0-100 (LLM)
│                                      │    • Focus 75 (placeholder)
│                                      │    • Clarity 80 (placeholder)
│                                      │    • Emotion 70 (placeholder)
│                                      │    • FlubScore 0-100 (traditional)
│                                      │    • CompositeScore (weighted)
│                                      │    
│                                      │    Detect repeated takes, cluster similar
│                                      │    segments, select best take per cluster
│                                      │    Output: Enhanced SRT with all metrics
└──────────────────────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 4. Speaker / Shot Tagging  │  → Text-based speaker diarization
│                            │    Face detection & clustering (FaceAiSharp)
│                            │    Shot classification (CU/MS/WS/INSERT)
│                            │    Output: speaker.meta.json
│                            │    `speakerId`, `shotLabel`, `faceIds`, confidence
└────────────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 5. Quality          │  → Collect all quality-scored segments
│    Aggregation      │    Read enhanced SRT format (9 dimensions)
│                     │    Preserve comprehensive quality vectors
│                     │    Output: project.ranked.srt (master file)
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 6. Weighted Ranking │  → User weight vector application:
│                     │    • compositeScore: 100f (primary)
│                     │    • relevance/sentiment/novelty/energy: user input
│                     │    • focus/clarity/emotion/flubScore: 0f (pending UI)
│                     │    Sort by weighted quality score
│                     │    Output: project.ordered.srt (best segments first)
└─────────────────────┘
   │
   ▼
┌─────────────────────┐
│ 7. Diversity (MMR)  │  → Maximal Marginal Relevance
│                     │    λ×relevance - (1-λ)×(1-novelty)
│                     │    Balance quality vs diversity
│                     │    Output: project.novelty.srt (diverse shortlist)
└─────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 8. Dialogue Sequencer      │  → Speaker alternation enforcement
│                            │    LLM reply-score calculation
│                            │    λ×baseScore + (1-λ)×replyScore
│                            │    Output: project.dialogue.srt (conversational flow)
└────────────────────────────┘
   │
   ▼
┌────────────────────────────┐
│ 9. Energy-Based Expansion  │  → Dynamic temporal windows:
│                            │    • Low energy: 0.8×base window
│                            │    • High energy: 1.3×base window
│                            │    Energy score drives expansion
│                            │    Output: project.expanded.srt (context-aware timing)
└────────────────────────────┘
   │
   ▼
┌────────────────────────────┐
│10. Hard Constraints        │  → Runtime caps enforcement
│                            │    Greedy selection by rank order
│                            │    Target duration compliance
│                            │    Output: project.trim.srt (final selection)
└────────────────────────────┘
   │
   ▼
┌─────────────────────┐
│11. Render           │  → Parse enhanced SRT metadata
│                     │    FFmpeg segment extraction
│                     │    Optimal encoding detection
│                     │    Concatenate with transitions
│                     │    Output: Final MP4 video
└─────────────────────┘
```

## Key Changes from Original Workflow

### **Step 3: Take Layer + Quality Vector** (Major Enhancement)
- **Before**: Simple duplicate detection with basic quality assessment
- **Now**: Comprehensive 9-dimensional quality scoring integrated with duplicate detection
- **Impact**: Much richer quality data flows through entire pipeline

### **Step 5: Quality Aggregation** (Renamed from "Vector Builder")
- **Before**: "Vector Builder" with 4-axis vectors stored in ANN index
- **Now**: "Quality Aggregation" that collects pre-computed 9-dimensional vectors from Take Layer
- **Impact**: Eliminates redundant LLM calls, uses richer quality data

### **Step 6: Weighted Ranking** (Enhanced from "Scalar Ranking")
- **Before**: "Scalar Ranking" with ANN top-K search
- **Now**: "Weighted Ranking" using user-defined weights across 9 quality dimensions
- **Impact**: More sophisticated ranking with user control over multiple quality aspects

### **Data Flow Improvements**
1. **Quality vectors generated once** in Take Layer, then reused throughout pipeline
2. **Enhanced SRT format** carries all 9 quality metrics between stages
3. **Speaker metadata flows separately** and merges with quality data for dialogue sequencing
4. **No redundant scoring** - each segment scored comprehensively once

### **Quality Metrics Evolution**
- **Traditional**: FlubScore (filler word detection)
- **LLM-based**: Relevance, Sentiment, Novelty, Energy (prompt-aware)
- **Future-ready**: Focus, Clarity, Emotion (placeholders for CV/audio analysis)
- **Composite**: Weighted combination respecting user preferences

This updated workflow shows how quality assessment has evolved from a simple 4-dimensional vector to a comprehensive 9-dimensional quality system that's generated early and utilized throughout the entire pipeline for better content curation and user control.
