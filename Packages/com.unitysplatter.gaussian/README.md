# UnitySplatter Gaussian Splatting

Unity 6.3 LTS package for advanced Gaussian splatting editing, reconstruction, realtime rendering, filtering, optimization, and playback.

## Key Features

- **Realtime rendering** for desktop and Android (DirectX/Vulkan) using procedural draw calls.
- **PLY loader** supporting ASCII and binary little-endian formats with strict validation.
- **Sequence playback** of multiple PLY files at a configurable frame rate during play mode and in builds.
- **Prebuild baking** option to compile sequences into compact binary blobs for runtime efficiency.
- **Filtering & optimization** utilities for deduplication, bounds culling, opacity thresholds, and more.
- **Editor tooling** for import, preview, conversion, and batch bake pipelines.

## Quick Start

1. Import or place `.ply` files in your project (supports ScriptedImporter).
2. Create a `GaussianSplatRenderer` component and assign a `GaussianSplatAsset`.
3. To play sequences, add `GaussianSplatSequencePlayer` and set a directory or baked asset.

## Notes

- Uses a procedural rendering shader with GPU buffers.
- Designed to be extended with more advanced splat encodings or SH-based lighting.
