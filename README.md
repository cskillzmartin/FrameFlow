# FrameFlow

FrameFlow is an AI first video editing application that automatically edits .mp4 files to craft the story you describe in the subject. It transcribes speech, analyzes content, and intelligently selects the most relevant segments based on your chosen subject.

## How It Works

1. **Video Import**
   - Import one or more .mp4 video files
   - Videos are prepared for processing in your project directory

2. **Speech Transcription**
   - Whisper AI model transcribes speech from each video
   - Creates timestamped SRT files with spoken content
   - Shows real-time progress for each video being transcribed

3. **Content Analysis**
   - Phi-3 AI model analyzes each segment of the transcription
   - Scores segments based on relevance to your chosen subject
   - Creates `[projectname].edit.srt` with all segments and their scores

4. **Segment Selection**
   - Selects highest-scoring segments that fit within your time limit
   - Prioritizes segments with scores >= 50
   - Creates `[projectname].working.edit.srt` with selected segments in chronological order

5. **Story Ordering**
   - AI reorders segments to create a coherent narrative
   - Considers context and flow between segments
   - Creates `[projectname].working.edit.reorder.srt` with the story-optimized sequence

6. **Video Creation**
   - FFmpeg extracts selected segments from source videos
   - Combines segments according to the chosen order
   - Creates final edited video in your project directory

Each step shows progress in the application's interface, and the final output includes:
- Original transcription files
- Scored segment list
- Chronological edit
- Story-ordered edit
- Final edited video

After installing all dependancies open the folder in VS Code
run commands:
dotnet restore 
dotnet build
dotnet run

## Dependencies

### Required Software

1. **FFmpeg and Required Libraries**
   - Download the full FFmpeg package from: [FFmpeg Official Website](https://ffmpeg.org/download.html)
   - Required files (place all in the same directory): 
     ffmpeg.exe       # Main FFmpeg executable
     ffplay.exe       # Media player
     ffprobe.exe      # Media analyzer
     avcodec-62.dll   # Codec library
     avdevice-62.dll  # Device handling
     avfilter-11.dll  # Audio/video filtering
     avformat-62.dll  # Container format handling
     avutil-60.dll    # Common utilities
     swresample-6.dll # Audio resampling
     swscale-9.dll    # Video scaling
   - Place in this location
     - 'Utilities/ffmpeg' 

2. **NVIDIA CUDA Toolkit** (Optional - for GPU acceleration)
   - Version: 11.8 or later
   - Download: [NVIDIA CUDA Toolkit](https://developer.nvidia.com/cuda-downloads)
   - Required for GPU-accelerated video processing and AI models

3. **NVIDIA cuDNN** (Optional - for GPU acceleration)
   - Version: 8.9 or later
   - Download: [NVIDIA cuDNN](https://developer.nvidia.com/cudnn)
   - Must match your CUDA version
   - Required for GPU-accelerated AI models

### AI Models

1. **Phi-3 Model**
   - https://huggingface.co/microsoft/Phi-3-mini-128k-instruct-onnx
   - Required for content analysis and scoring
   - Download options:
     - CPU version: `phi3-mini-128k-instruct-cpu-int4-rtn-block-32-acc-level-4.onnx`
     - CUDA version: `phi3-mini-128k-instruct-cuda-int4-rtn-block-32.onnx`
     - DirectML version: `model.onnx`
   - Place in these locations:
     - `models/cpu`
     - `models/cuda`
     - `models/directml`

2. **Whisper Model**
   - https://huggingface.co/ggerganov/whisper.cpp/tree/main
   - Required for speech transcription
   - File: `ggml-base.bin`
   - Place in this location:
     - `models/whisper`

## Issues

 **"CUDA initialization failed"**
   - Update NVIDIA drivers
   - Verify CUDA Toolkit installation
   - Check cuDNN installation
   - make sure CUDA and cuDNN versions are compatible
   - Try DirectML or CPU version instead
