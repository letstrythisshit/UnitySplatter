# UnitySplatter Gaussian Splatting

This package provides an advanced Gaussian splatting toolkit for Unity 6.3 LTS with:

- PLY import (ASCII + binary), runtime streaming, and editor-time baking.
- Real-time rendering via GPU-driven procedural draw.
- Filters, optimizers, and reconstruction utilities.
- Sequenced playback of multiple PLY frames at fixed FPS in editor, play mode, and player builds.
- Designed to run on DirectX and Vulkan (desktop + Android).

## Quick start
1. Create a **Gaussian Splat Asset** from a PLY file using the *Gaussian Splat Importer* window.
2. Add **Gaussian Splat Renderer** to a GameObject and assign the asset.
3. Use **Gaussian Splat Sequence Player** to play a sequence of assets or a directory of PLY files.

## Notes
- For runtime directory streaming on Android, place PLY files under StreamingAssets.
- For maximum performance, bake sequences in the editor into a **Gaussian Splat Sequence Asset**.

## Samples
The Samples~ folder provides small scenes and materials to validate rendering and playback.
