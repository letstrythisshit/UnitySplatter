# UnitySplatter Gaussian Splatting

This package provides advanced Gaussian splatting editing, reconstruction, realtime rendering, filtering, optimization, and playback of PLY sequences for Unity 6.3 LTS.

## Features
- Runtime Gaussian splat rendering via `GaussianSplatRenderer`.
- PLY loading (ASCII and binary little-endian) with robust validation.
- Sequence playback via `GaussianSplatSequencePlayer`, supporting baked assets, TextAssets, or StreamingAssets.
- Filters and optimizers for decimation, bounds/opacity filtering, normalization, and reconstruction utilities.
- Editor tool to bake a folder of PLY frames into a `GaussianSplatSequenceAsset`.

## Quick Start
1. Add this package to your project.
2. Create a `GaussianSplatAsset` or `GaussianSplatSequenceAsset` from the Create menu.
3. Add `GaussianSplatRenderer` to a GameObject.
4. Assign the asset in the renderer or use `GaussianSplatSequencePlayer` for frame playback.

## Editor Baking
Use `UnitySplatter > Bake Gaussian Splat Sequence` to bake a folder of `.ply` files into a single sequence asset for build-time playback.
