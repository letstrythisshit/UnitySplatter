# UnitySplatter Gaussian Splatting Package

**Advanced Gaussian Splatting for Unity 6.3 LTS**

A comprehensive Unity package for Gaussian splatting with advanced editing, reconstruction, real-time rendering, filtering, optimization, and high-performance playback of PLY sequences. Supports both desktop (DirectX/Vulkan) and mobile (Android) platforms.

---

## 🎯 Features

### Core Rendering
- **High-Performance GPU Rendering**: Hardware-accelerated rendering using compute shaders and structured buffers
- **Advanced Renderer**: GPU-based frustum culling, LOD system, and depth sorting
- **Mobile Optimization**: Dedicated mobile rendering path for Android devices
- **Cross-Platform**: Supports DirectX 11+, Vulkan, and OpenGL ES 3.1+
- **Quality Modes**: Multiple quality presets from Low to Ultra with advanced shading options

### Sequence Playback
- **Standard Playback**: Frame-by-frame playback with configurable FPS and looping
- **Streaming Playback**: Memory-efficient streaming for large sequences with frame prediction
- **Baked Sequences**: Pre-compiled sequence assets for optimal runtime performance
- **Multiple Sources**: Load from baked assets, TextAssets, or StreamingAssets directories

### Data Processing
- **LOD Generation**: Automatic Level of Detail generation with multiple algorithms:
  - Uniform decimation
  - Random sampling
  - Importance-based selection
  - Spatial clustering
  - Hierarchical clustering
  - Adaptive density
- **Compression**: High-quality compression with 3-5x reduction in file size
- **Filtering**: Opacity, bounds, decimation, and random sampling filters
- **Reconstruction**: Convert point clouds to Gaussian splats with normal and scale estimation
- **Editing Tools**: Translate, rotate, scale, and tint transformations

### Advanced Features
- **GPU Culling**: Compute shader-based frustum and distance culling
- **LOD System**: Distance-based LOD with automatic quality reduction
- **Streaming System**: Async loading with frame caching and prediction
- **Error Prevention**: Comprehensive validation and error handling throughout
- **Editor Tools**: Advanced editor window with import, processing, and analysis tools

---

## 🚀 Quick Start

### Basic Usage

1. **Import PLY File**
   ```
   Window > UnitySplatter > Gaussian Splat Tools > Import Tab
   - Browse to your .ply file
   - Click "Import Single File"
   ```

2. **Setup Rendering**
   - Create an empty GameObject in your scene
   - Add `GaussianSplatRenderer` component
   - Assign your imported asset
   - (Optional) Add `GaussianSplatAdvancedRenderer` for enhanced performance

3. **Sequence Playback**
   - Add `GaussianSplatSequencePlayer` to the GameObject
   - Assign a baked sequence asset OR
   - Configure path to folder containing .ply files
   - Press Play

### Advanced Setup

#### High-Performance Rendering
```csharp
// Add advanced renderer for GPU culling and LOD
var advancedRenderer = gameObject.AddComponent<GaussianSplatAdvancedRenderer>();
advancedRenderer.quality = RenderingQuality.High;
advancedRenderer.enableGPUCulling = true;
advancedRenderer.enableLOD = true;
```

#### Streaming Large Sequences
```csharp
// Stream large sequences from disk
var streamingPlayer = gameObject.AddComponent<GaussianSplatStreamingPlayer>();
streamingPlayer.streamingAssetsPath = "GaussianSplatSequences/MySequence";
streamingPlayer.maxCachedFrames = 10;
streamingPlayer.preloadFrameCount = 3;
streamingPlayer.Play();
```

---

## 📋 Components

### GaussianSplatRenderer
**Base renderer for Gaussian splats**
- Renders splats using GPU-accelerated structured buffers
- Supports both SRP and built-in render pipeline
- Configurable opacity multiplier and frustum culling
- Material override support

### GaussianSplatAdvancedRenderer
**High-performance renderer with advanced features**
- GPU-based frustum and distance culling using compute shaders
- Automatic LOD system with 4 quality levels
- Optional depth sorting for proper alpha blending
- Mobile device detection and optimization
- Real-time performance statistics

**Properties:**
- `quality`: Low, Medium, High, or Ultra
- `enableGPUCulling`: Use compute shaders for culling
- `enableLOD`: Distance-based level of detail
- `enableDepthSorting`: Sort splats for alpha blending (expensive)
- `lod0-3Distance`: Distance thresholds for each LOD level
- `lod0-3Decimation`: Splat reduction factor per LOD

### GaussianSplatSequencePlayer
**Frame-by-frame sequence playback**
- Configurable FPS and looping
- Multiple loading sources (baked, TextAssets, file paths)
- Async loading with coroutines
- Auto-detection of renderer component

**Properties:**
- `fps`: Playback frame rate (default: 30)
- `loop`: Enable/disable looping
- `playOnEnable`: Auto-play on enable

### GaussianSplatStreamingPlayer
**Memory-efficient streaming for large sequences**
- Frame prediction and preloading
- Intelligent caching system
- Background async loading
- Configurable memory usage

**Properties:**
- `maxCachedFrames`: Maximum frames in memory
- `preloadFrameCount`: Frames to preload ahead
- `useBackgroundLoading`: Async loading on background thread

**Methods:**
- `Play()`: Start playback
- `Pause()`: Pause playback
- `Stop()`: Stop and reset
- `Seek(int frameIndex)`: Jump to specific frame
- `GetCacheHitRate()`: Get cache efficiency (0-1)

---

## 🛠️ Editor Tools

### Gaussian Splat Tools Window
**Access via:** `Window > UnitySplatter > Gaussian Splat Tools`

#### Import Tab
- Import single PLY files
- Batch import folders as sequences
- Preview imported splat data

#### Process Tab
- Filter by opacity threshold
- Filter by spatial bounds
- Decimate (keep every N-th splat)
- Random sampling

#### LOD Tab
- Generate multiple LOD levels automatically
- Choose from 6 different generation algorithms
- Preview LOD statistics
- Save LODs as separate assets

#### Compression Tab
- Compress splat data using quantization
- View compression statistics
- Save/load compressed data
- Typical compression: 3-5x size reduction

#### Sequence Tab
- Create sequence assets from frames
- Configure FPS and looping
- Manage frame collections
- Save as baked sequence assets

#### Tools Tab
- Transform tools (translate, rotate, scale)
- Color tinting
- Apply transformations to splat data

### Sequence Baking
**Menu:** `UnitySplatter > Bake Gaussian Splat Sequence`
- Select folder containing .ply files
- Automatically loads all files in order
- Creates single sequence asset
- Optimal for runtime performance

---

## 📚 API Reference

### Data Structures

#### GaussianSplatFrameData
```csharp
public class GaussianSplatFrameData
{
    public Vector3[] positions;
    public Vector3[] scales;
    public Quaternion[] rotations;
    public Color[] colors;
    public float[] opacities;

    public int Count { get; }
    public bool IsValid { get; }
}
```

### Filters and Optimization

#### GaussianSplatFilter
```csharp
// Filter by opacity
var filtered = GaussianSplatFilter.FilterByOpacity(frameData, minOpacity: 0.1f);

// Filter by bounds
var filtered = GaussianSplatFilter.FilterByBounds(frameData, bounds);

// Decimate (keep every N-th)
var decimated = GaussianSplatFilter.Decimate(frameData, stride: 2);

// Random sampling
var sampled = GaussianSplatFilter.RandomSample(frameData, targetCount: 1000);
```

#### GaussianSplatOptimizer
```csharp
// Calculate bounds
Bounds bounds = GaussianSplatOptimizer.CalculateBounds(positions, scales);

// Normalize scale
frameData = GaussianSplatOptimizer.NormalizeScale(frameData, targetScale: 1.0f);
```

### LOD Generation

#### GaussianSplatLODGenerator
```csharp
// Generate multiple LOD levels
var lodLevels = GaussianSplatLODGenerator.GenerateLODLevels(
    sourceData,
    lodLevels: 4,
    method: LODGenerationMethod.ImportanceBased
);

// Generate single LOD level
var lod1 = GaussianSplatLODGenerator.GenerateLODLevel(
    sourceData,
    targetCount: 50000,
    method: LODGenerationMethod.SpatialClustering,
    lodLevel: 1
);
```

**Available Methods:**
- `UniformDecimation`: Simple every-N-th sampling
- `RandomSampling`: Random subset selection
- `ImportanceBased`: Keep large and opaque splats
- `SpatialClustering`: K-means clustering and merging
- `HierarchicalClustering`: Octree-based hierarchical selection
- `AdaptiveDensity`: Density-aware importance sampling

### Compression

#### GaussianSplatCompressor
```csharp
// Compress frame data
var compressed = GaussianSplatCompressor.CompressCPU(frameData);

// Decompress
var decompressed = GaussianSplatCompressor.DecompressCPU(compressed);

// Save/load compressed data
GaussianSplatCompressor.SaveCompressed(compressed, "path/to/file.gspc");
var loaded = GaussianSplatCompressor.LoadCompressed("path/to/file.gspc");

// Get statistics
int originalSize = GaussianSplatCompressor.GetUncompressedSize(frameData);
int compressedSize = compressed.GetCompressedSize();
float ratio = compressed.GetCompressionRatio(originalSize);
```

### Reconstruction

#### GaussianSplatAdvancedReconstruction
```csharp
// Convert point cloud to Gaussian splats
var splats = GaussianSplatAdvancedReconstruction.FromPointCloud(
    positions,
    colors: null,  // Optional colors
    normals: null, // Optional normals
    defaultScale: 0.01f,
    estimateNormals: true,
    estimateScales: true,
    neighborCount: 8
);

// Estimate normals from positions
var normals = GaussianSplatAdvancedReconstruction.EstimateNormals(
    positions,
    neighborCount: 8
);

// Estimate scales from local density
var scales = GaussianSplatAdvancedReconstruction.EstimateScales(
    positions,
    neighborCount: 8,
    scaleFactor: 1.0f
);

// Refine existing splats
var refined = GaussianSplatAdvancedReconstruction.RefineGaussians(
    sourceData,
    iterations: 5,
    learningRate: 0.1f
);

// Remove outliers
var cleaned = GaussianSplatAdvancedReconstruction.RemoveOutliers(
    sourceData,
    neighborCount: 8,
    stddevMultiplier: 2.0f
);

// Fill holes
var filled = GaussianSplatAdvancedReconstruction.FillHoles(
    sourceData,
    holeThreshold: 0.1f,
    maxNewSplats: 10000
);
```

### Editing

#### GaussianSplatEditing
```csharp
// Translate
frameData = GaussianSplatEditing.Translate(frameData, offset: new Vector3(1, 0, 0));

// Scale
frameData = GaussianSplatEditing.Scale(frameData, multiplier: new Vector3(2, 2, 2));

// Rotate
frameData = GaussianSplatEditing.Rotate(frameData, Quaternion.Euler(0, 45, 0));

// Tint
frameData = GaussianSplatEditing.Tint(frameData, Color.red);
```

---

## ⚙️ Compute Shaders

The package includes high-performance compute shaders for GPU-accelerated processing:

### GaussianSplatCulling.compute
- Frustum culling
- Distance culling
- LOD selection
- Combined culling and LOD

**Kernels:**
- `FrustumCull`: Test splats against view frustum
- `DistanceCull`: Cull by distance range
- `LODSelect`: Select splats based on LOD level
- `CombinedCullAndLOD`: All-in-one culling pass

### GaussianSplatSorting.compute
- Depth calculation
- Bitonic sort for alpha blending
- Compact sorted results

**Kernels:**
- `CalculateDepths`: Compute view-space depth
- `BitonicSort`: Parallel sorting algorithm
- `CompactSorted`: Remove invalid entries

### GaussianSplatCompression.compute
- Quantization compression
- Delta compression for sequences
- GPU decompression

**Kernels:**
- `Compress`: Quantize to 16-bit precision
- `Decompress`: Decompress to full precision
- `DeltaCompress`: Compress frame-to-frame deltas
- `DeltaDecompress`: Decompress delta-encoded data

---

## 📱 Platform Support

### Desktop
- **Windows**: DirectX 11/12, Vulkan
- **macOS**: Metal
- **Linux**: Vulkan, OpenGL

### Mobile
- **Android**: OpenGL ES 3.1+, Vulkan
- **iOS**: Metal (requires minor modifications)

### Optimization Tips

**Desktop:**
- Enable GPU culling for large scenes (> 100k splats)
- Use LOD system for distant objects
- Enable depth sorting only if alpha blending issues occur

**Mobile:**
- Reduce max splats (100k recommended)
- Use mobile-optimized shaders automatically
- Disable depth sorting
- Use aggressive LOD settings
- Consider pre-decimating source data

---

## 🎨 Shaders

### GaussianSplatAdvanced.shader
**High-quality desktop shader**
- Advanced Gaussian sphere rendering
- Anisotropic scaling support
- Optional lighting and specular highlights
- Gamma correction support
- Multi-compile variants for quality levels

### GaussianSplatMobile.shader
**Mobile-optimized shader**
- Reduced precision (half/mediump)
- Simplified Gaussian falloff
- No lighting calculations
- Lower max point size (256 vs 512)
- Optimized for mobile GPUs

---

## 📦 File Formats

### PLY Format
**Supported:**
- ASCII format
- Binary little-endian format
- Standard Gaussian splat properties:
  - Position: `x, y, z`
  - Scale: `scale_0/1/2`, `scale_x/y/z`, or `sx/sy/sz`
  - Rotation: `rot_0/1/2/3` or `qx/qy/qz/qw` (quaternion)
  - Color: `red/green/blue` or `r/g/b`
  - Opacity: `alpha` or `opacity`

### Compressed Format (.gspc)
**Custom binary format:**
- Magic number: "GSPC"
- Version: 1
- Quantized 16-bit positions, scales, rotations
- RGBA8888 colors
- 8-bit opacities
- Bounds metadata for decompression

---

## 🔧 Requirements

- **Unity Version**: 6.3 LTS (6000.3 or later)
- **Graphics API**:
  - Desktop: DirectX 11+, Vulkan, Metal
  - Mobile: OpenGL ES 3.1+, Vulkan
- **Compute Shader Support**: Required for advanced features
- **Minimum GPU**:
  - Desktop: Any GPU with shader model 4.5+
  - Mobile: OpenGL ES 3.1 or Vulkan compatible

---

## 🐛 Troubleshooting

### Common Issues

**Splats not rendering:**
- Ensure camera can see the splat bounds
- Check that frameData is valid (not null, Count > 0)
- Verify shader is found and compiled
- Check Graphics API compatibility

**Poor performance:**
- Enable GPU culling on advanced renderer
- Use LOD system for large scenes
- Reduce max splats for mobile
- Consider pre-processing data with decimation

**Mobile crashes:**
- Reduce max splats (use mobile limits)
- Disable advanced features (depth sorting, specular)
- Check device supports OpenGL ES 3.1 or Vulkan

**Streaming stutters:**
- Increase `maxCachedFrames`
- Increase `preloadFrameCount`
- Enable background loading
- Reduce FPS or source file count

**Compilation errors:**
- Ensure compute shaders are in Resources folder
- Verify assembly definitions are correct
- Check Unity version compatibility (6.3 LTS required)

---

## 📝 Best Practices

1. **Always validate data**: Check `IsValid` before using frame data
2. **Use LODs**: Generate and use LOD levels for large scenes
3. **Cache sequences**: Bake sequences at edit time for best runtime performance
4. **Test on target platform**: Performance varies significantly between desktop and mobile
5. **Monitor memory**: Use streaming player for sequences larger than available RAM
6. **Preprocess data**: Apply filters and optimizations at edit time, not runtime
7. **Profile performance**: Use Unity Profiler to identify bottlenecks

---

## 📄 License

See LICENSE file for details.

---

## 🤝 Contributing

Contributions are welcome! Please submit issues and pull requests on the project repository.

---

## 📧 Support

For support, bug reports, or feature requests, please open an issue on the repository.

---

**Version**: 0.2.0
**Unity Version**: 6.3 LTS (6000.3+)
**Last Updated**: 2026-01-20
