# FaceAiSharp Integration Setup Guide

## Overview
This guide walks through integrating FaceAiSharp for advanced face detection and speaker identification in FrameFlow.

## Prerequisites
- .NET 6.0 or higher
- FrameFlow project
- FFmpeg configured in settings

## Step 1: Install Required NuGet Packages

Add the following packages to your FrameFlow project:

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="FaceAiSharp.Bundle" Version="0.5.23" />
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.17.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.2" />
<PackageReference Include="SixLabors.ImageSharp.Drawing" Version="2.1.1" />
```

Or install via Package Manager Console:
```powershell
Install-Package FaceAiSharp.Bundle -Version 0.5.23
Install-Package Microsoft.ML.OnnxRuntime -Version 1.17.0  
Install-Package SixLabors.ImageSharp -Version 3.1.2
Install-Package SixLabors.ImageSharp.Drawing -Version 2.1.1
```

Or via .NET CLI:
```bash
dotnet add package FaceAiSharp.Bundle --version 0.5.23
dotnet add package Microsoft.ML.OnnxRuntime --version 1.17.0
dotnet add package SixLabors.ImageSharp --version 3.1.2
dotnet add package SixLabors.ImageSharp.Drawing --version 2.1.1
```

## Step 2: Build the Project

After adding the packages, rebuild your solution:
```bash
dotnet build
```

## Step 3: Configure App Settings (Optional)

Add FaceAiSharp-specific settings to your `Settings.cs`:

```csharp
// Add to Settings.cs
public class Settings
{
    // ... existing settings ...
    
    // FaceAiSharp settings
    public bool EnableFaceDetection { get; set; } = true;
    public float FaceDetectionThreshold { get; set; } = 0.7f;
    public float FaceSimilarityThreshold { get; set; } = 0.42f;
    public int MaxFacesPerSegment { get; set; } = 5;
    public bool UseGpuAcceleration { get; set; } = true;
}
```

## Step 4: Test the Integration

Create a simple test to verify FaceAiSharp is working:

```csharp
// Test method - add to your test project or main application
public async Task TestFaceAiSharpIntegration()
{
    try
    {
        // Test face detector initialization
        using var detector = FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks();
        using var embedder = FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator();
        
        Console.WriteLine("FaceAiSharp integration successful!");
        
        // Test with a sample image (optional)
        // var testImage = Image.Load<Rgb24>("path/to/test/image.jpg");
        // var faces = detector.DetectFaces(testImage);
        // Console.WriteLine($"Detected {faces.Count} faces");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FaceAiSharp integration failed: {ex.Message}");
        throw;
    }
}
```

## Step 5: Usage in Render Pipeline

Once installed, the SpeakerManager will automatically use FaceAiSharp for face detection:

```csharp
// Example usage in your render pipeline
public async Task ProcessVideoWithSpeakerAnalysis(string projectName, string renderDir)
{
    // The SpeakerManager will automatically use FaceAiSharp if available
    var success = await SpeakerManager.Instance.ProcessSpeakerAnalysisAsync(projectName, renderDir);
    
    if (success)
    {
        // Load results
        var speakerData = await SpeakerManager.Instance.LoadSpeakerAnalysisAsync(projectName, renderDir);
        
        // Use face detection results
        var faceStats = SpeakerManager.Instance.GetSpeakerStatistics(speakerData);
        foreach (var speaker in faceStats)
        {
            Console.WriteLine($"Speaker {speaker.Key}: {speaker.Value} segments");
        }
    }
}
```

## Performance Considerations

### GPU Acceleration
FaceAiSharp supports GPU acceleration via ONNX Runtime:
- **CUDA**: Requires NVIDIA GPU with CUDA support
- **DirectML**: Windows 10/11 with compatible DirectX 12 GPU
- **CPU**: Fallback option, slower but universally compatible

### Memory Usage
- **Face Detection**: ~50-100MB per model
- **Face Recognition**: ~250MB for ArcFace model
- **Runtime**: ~10MB per hour of analyzed video

### Processing Time
- **Face Detection**: ~50-100ms per keyframe
- **Face Recognition**: ~10-20ms per detected face
- **Total**: ~2-3 seconds per minute of video footage

## Troubleshooting

### Common Issues

**1. Package Installation Errors**
```
Error: Package 'FaceAiSharp.Bundle' is not compatible with 'net5.0'
```
**Solution**: Ensure you're targeting .NET 6.0 or higher in your project file.

**2. ONNX Runtime Errors**
```
Error: Unable to load DLL 'onnxruntime'
```
**Solution**: Install the appropriate ONNX Runtime package for your platform:
- Windows: `Microsoft.ML.OnnxRuntime`
- Linux: `Microsoft.ML.OnnxRuntime.Managed`
- GPU: `Microsoft.ML.OnnxRuntime.Gpu`

**3. Memory Issues**
```
OutOfMemoryException during face detection
```
**Solution**: 
- Reduce `MaxFacesPerSegment` setting
- Process videos in smaller batches
- Ensure sufficient RAM (8GB+ recommended)

**4. Model Loading Errors**
```
Error: Unable to load ONNX model
```
**Solution**:
- Verify FaceAiSharp.Bundle package installed correctly
- Check disk space (models require ~300MB)
- Ensure app has read permissions for model files

### Performance Optimization

**1. Batch Processing**
Process multiple segments together for better GPU utilization:
```csharp
// Process in batches of 10 segments
var batchSize = 10;
for (int i = 0; i < segments.Count; i += batchSize)
{
    var batch = segments.Skip(i).Take(batchSize);
    await ProcessSegmentBatch(batch);
}
```

**2. Keyframe Selection**
Optimize keyframe extraction for better face detection:
```csharp
// Extract multiple keyframes per segment for better face detection
var keyframeTimes = new[]
{
    segment.StartTime + TimeSpan.FromMilliseconds(segment.Duration.TotalMilliseconds * 0.25),
    segment.StartTime + TimeSpan.FromMilliseconds(segment.Duration.TotalMilliseconds * 0.50),
    segment.StartTime + TimeSpan.FromMilliseconds(segment.Duration.TotalMilliseconds * 0.75)
};
```

**3. Caching Strategy**
Cache face embeddings to avoid recomputation:
```csharp
// Cache face embeddings in the metadata
public class FaceCache
{
    public Dictionary<string, float[]> EmbeddingCache { get; set; } = new();
    
    public float[] GetOrComputeEmbedding(string faceId, Func<float[]> computeFunc)
    {
        if (!EmbeddingCache.ContainsKey(faceId))
        {
            EmbeddingCache[faceId] = computeFunc();
        }
        return EmbeddingCache[faceId];
    }
}
```

## Next Steps

1. **Install the packages** using the commands above
2. **Rebuild your project** to ensure all dependencies are resolved
3. **Test the integration** with a sample video
4. **Configure settings** based on your hardware capabilities
5. **Run speaker analysis** on your video projects

The SpeakerManager will automatically detect when FaceAiSharp is available and use it for enhanced face detection and speaker identification. 