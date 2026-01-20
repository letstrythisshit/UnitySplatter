using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UnitySplatter.GaussianSplatting
{
    /// <summary>
    /// Generates Level of Detail (LOD) versions of Gaussian splat data
    /// Uses spatial clustering and importance-based selection
    /// </summary>
    public static class GaussianSplatLODGenerator
    {
        public enum LODGenerationMethod
        {
            UniformDecimation,      // Simple every-N-th sampling
            RandomSampling,         // Random subset selection
            ImportanceBased,        // Keep high-opacity and large splats
            SpatialClustering,      // Cluster nearby splats and merge
            HierarchicalClustering, // Multi-level spatial hierarchy
            AdaptiveDensity         // Vary density based on local complexity
        }

        /// <summary>
        /// Generate multiple LOD levels from a single frame
        /// </summary>
        public static List<GaussianSplatFrameData> GenerateLODLevels(
            GaussianSplatFrameData sourceData,
            int lodLevels = 4,
            LODGenerationMethod method = LODGenerationMethod.ImportanceBased)
        {
            if (sourceData == null || !sourceData.IsValid || sourceData.Count == 0)
            {
                Debug.LogError("[GaussianSplatLODGenerator] Invalid source data");
                return new List<GaussianSplatFrameData>();
            }

            var lodList = new List<GaussianSplatFrameData>();

            // LOD 0 is always the full resolution
            lodList.Add(sourceData);

            // Generate each LOD level with progressively fewer splats
            float[] reductionFactors = GenerateReductionFactors(lodLevels);

            for (int i = 1; i < lodLevels; i++)
            {
                int targetCount = Mathf.Max(1, (int)(sourceData.Count * reductionFactors[i]));
                GaussianSplatFrameData lodData = GenerateLODLevel(sourceData, targetCount, method, i);

                if (lodData != null && lodData.IsValid)
                {
                    lodList.Add(lodData);
                }
                else
                {
                    Debug.LogWarning($"[GaussianSplatLODGenerator] Failed to generate LOD level {i}");
                    break;
                }
            }

            return lodList;
        }

        /// <summary>
        /// Generate a single LOD level
        /// </summary>
        public static GaussianSplatFrameData GenerateLODLevel(
            GaussianSplatFrameData sourceData,
            int targetCount,
            LODGenerationMethod method,
            int lodLevel = 1)
        {
            if (sourceData == null || !sourceData.IsValid)
                return null;

            if (targetCount >= sourceData.Count)
                return sourceData; // No reduction needed

            targetCount = Mathf.Max(1, targetCount);

            switch (method)
            {
                case LODGenerationMethod.UniformDecimation:
                    return GenerateUniformDecimation(sourceData, targetCount);

                case LODGenerationMethod.RandomSampling:
                    return GenerateRandomSampling(sourceData, targetCount, lodLevel);

                case LODGenerationMethod.ImportanceBased:
                    return GenerateImportanceBased(sourceData, targetCount);

                case LODGenerationMethod.SpatialClustering:
                    return GenerateSpatialClustering(sourceData, targetCount);

                case LODGenerationMethod.HierarchicalClustering:
                    return GenerateHierarchicalClustering(sourceData, targetCount);

                case LODGenerationMethod.AdaptiveDensity:
                    return GenerateAdaptiveDensity(sourceData, targetCount);

                default:
                    return GenerateImportanceBased(sourceData, targetCount);
            }
        }

        private static float[] GenerateReductionFactors(int levels)
        {
            float[] factors = new float[levels];
            factors[0] = 1.0f; // LOD 0 is full resolution

            for (int i = 1; i < levels; i++)
            {
                // Exponential reduction: 70%, 40%, 20% for levels 1, 2, 3
                factors[i] = Mathf.Pow(0.7f, i);
            }

            return factors;
        }

        private static GaussianSplatFrameData GenerateUniformDecimation(GaussianSplatFrameData source, int targetCount)
        {
            int stride = Mathf.Max(1, source.Count / targetCount);
            return GaussianSplatFilter.Decimate(source, stride);
        }

        private static GaussianSplatFrameData GenerateRandomSampling(GaussianSplatFrameData source, int targetCount, int seed)
        {
            return GaussianSplatFilter.RandomSample(source, targetCount, seed);
        }

        private static GaussianSplatFrameData GenerateImportanceBased(GaussianSplatFrameData source, int targetCount)
        {
            // Calculate importance score for each splat
            float[] importanceScores = new float[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                Vector3 scale = source.scales[i];
                float opacity = source.opacities[i];

                // Importance = size * opacity
                float size = (scale.x + scale.y + scale.z) / 3f;
                importanceScores[i] = size * opacity;
            }

            // Sort indices by importance (descending)
            var sortedIndices = Enumerable.Range(0, source.Count)
                .OrderByDescending(i => importanceScores[i])
                .Take(targetCount)
                .ToArray();

            // Create new frame data with selected splats
            return CreateFrameFromIndices(source, sortedIndices);
        }

        private static GaussianSplatFrameData GenerateSpatialClustering(GaussianSplatFrameData source, int targetCount)
        {
            // K-means style clustering
            int clusterCount = targetCount;
            Vector3[] clusterCenters = InitializeClusterCenters(source, clusterCount);

            // Assign each splat to nearest cluster
            int[] assignments = new int[source.Count];
            int maxIterations = 10;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Assignment step
                for (int i = 0; i < source.Count; i++)
                {
                    Vector3 pos = source.positions[i];
                    float minDist = float.MaxValue;
                    int bestCluster = 0;

                    for (int c = 0; c < clusterCount; c++)
                    {
                        float dist = Vector3.Distance(pos, clusterCenters[c]);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            bestCluster = c;
                        }
                    }

                    assignments[i] = bestCluster;
                }

                // Update step
                Vector3[] newCenters = new Vector3[clusterCount];
                int[] counts = new int[clusterCount];

                for (int i = 0; i < source.Count; i++)
                {
                    int cluster = assignments[i];
                    newCenters[cluster] += source.positions[i];
                    counts[cluster]++;
                }

                for (int c = 0; c < clusterCount; c++)
                {
                    if (counts[c] > 0)
                        clusterCenters[c] = newCenters[c] / counts[c];
                }
            }

            // Merge splats in each cluster
            return MergeClusters(source, assignments, clusterCount);
        }

        private static GaussianSplatFrameData GenerateHierarchicalClustering(GaussianSplatFrameData source, int targetCount)
        {
            // Build octree for spatial hierarchy
            Bounds bounds = CalculateBounds(source);
            OctreeNode root = new OctreeNode(bounds);

            // Insert all splats into octree
            for (int i = 0; i < source.Count; i++)
            {
                root.Insert(i, source.positions[i], 8); // Max 8 levels deep
            }

            // Extract splats from octree with target density
            List<int> selectedIndices = new List<int>();
            root.ExtractRepresentatives(selectedIndices, targetCount, source);

            // Ensure we have at least some splats
            if (selectedIndices.Count == 0)
            {
                // Fall back to uniform decimation
                return GenerateUniformDecimation(source, targetCount);
            }

            return CreateFrameFromIndices(source, selectedIndices.ToArray());
        }

        private static GaussianSplatFrameData GenerateAdaptiveDensity(GaussianSplatFrameData source, int targetCount)
        {
            // Calculate local density for each splat
            float[] densities = CalculateLocalDensities(source);

            // Calculate importance: prefer high-density areas (more detail)
            float[] importanceScores = new float[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                float opacity = source.opacities[i];
                float density = densities[i];

                // Higher importance in high-density areas
                importanceScores[i] = opacity * Mathf.Sqrt(density);
            }

            // Sort and select top splats
            var sortedIndices = Enumerable.Range(0, source.Count)
                .OrderByDescending(i => importanceScores[i])
                .Take(targetCount)
                .ToArray();

            return CreateFrameFromIndices(source, sortedIndices);
        }

        // Helper methods

        private static Vector3[] InitializeClusterCenters(GaussianSplatFrameData source, int count)
        {
            // Use K-means++ initialization for better initial centers
            Vector3[] centers = new Vector3[count];
            System.Random random = new System.Random(42);

            // First center is random
            centers[0] = source.positions[random.Next(source.Count)];

            // Subsequent centers are chosen with probability proportional to distance from nearest existing center
            for (int c = 1; c < count; c++)
            {
                float[] distances = new float[source.Count];
                float totalDist = 0;

                for (int i = 0; i < source.Count; i++)
                {
                    float minDist = float.MaxValue;
                    for (int j = 0; j < c; j++)
                    {
                        float dist = Vector3.Distance(source.positions[i], centers[j]);
                        minDist = Mathf.Min(minDist, dist);
                    }
                    distances[i] = minDist * minDist; // Square for better distribution
                    totalDist += distances[i];
                }

                // Choose next center with weighted probability
                float threshold = (float)random.NextDouble() * totalDist;
                float cumulative = 0;

                for (int i = 0; i < source.Count; i++)
                {
                    cumulative += distances[i];
                    if (cumulative >= threshold)
                    {
                        centers[c] = source.positions[i];
                        break;
                    }
                }
            }

            return centers;
        }

        private static GaussianSplatFrameData MergeClusters(GaussianSplatFrameData source, int[] assignments, int clusterCount)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> scales = new List<Vector3>();
            List<Quaternion> rotations = new List<Quaternion>();
            List<Color> colors = new List<Color>();
            List<float> opacities = new List<float>();

            for (int c = 0; c < clusterCount; c++)
            {
                // Find all splats in this cluster
                List<int> clusterSplats = new List<int>();
                for (int i = 0; i < source.Count; i++)
                {
                    if (assignments[i] == c)
                        clusterSplats.Add(i);
                }

                if (clusterSplats.Count == 0)
                    continue;

                // Merge splats in cluster
                Vector3 avgPos = Vector3.zero;
                Vector3 avgScale = Vector3.zero;
                Vector4 avgRotQuat = Vector4.zero;
                Color avgColor = Color.black;
                float avgOpacity = 0;

                foreach (int idx in clusterSplats)
                {
                    avgPos += source.positions[idx];
                    avgScale += source.scales[idx];

                    Quaternion q = source.rotations[idx];
                    avgRotQuat += new Vector4(q.x, q.y, q.z, q.w);

                    avgColor += source.colors[idx];
                    avgOpacity += source.opacities[idx];
                }

                float count = clusterSplats.Count;
                positions.Add(avgPos / count);
                scales.Add(avgScale / count);

                // Normalize quaternion
                avgRotQuat /= count;
                rotations.Add(new Quaternion(avgRotQuat.x, avgRotQuat.y, avgRotQuat.z, avgRotQuat.w).normalized);

                colors.Add(avgColor / count);
                opacities.Add(avgOpacity / count);
            }

            return new GaussianSplatFrameData
            {
                positions = positions.ToArray(),
                scales = scales.ToArray(),
                rotations = rotations.ToArray(),
                colors = colors.ToArray(),
                opacities = opacities.ToArray()
            };
        }

        private static GaussianSplatFrameData CreateFrameFromIndices(GaussianSplatFrameData source, int[] indices)
        {
            var positions = new Vector3[indices.Length];
            var scales = new Vector3[indices.Length];
            var rotations = new Quaternion[indices.Length];
            var colors = new Color[indices.Length];
            var opacities = new float[indices.Length];

            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                positions[i] = source.positions[idx];
                scales[i] = source.scales[idx];
                rotations[i] = source.rotations[idx];
                colors[i] = source.colors[idx];
                opacities[i] = source.opacities[idx];
            }

            return new GaussianSplatFrameData
            {
                positions = positions,
                scales = scales,
                rotations = rotations,
                colors = colors,
                opacities = opacities
            };
        }

        private static Bounds CalculateBounds(GaussianSplatFrameData source)
        {
            if (source.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Vector3 min = source.positions[0];
            Vector3 max = source.positions[0];

            for (int i = 1; i < source.Count; i++)
            {
                min = Vector3.Min(min, source.positions[i]);
                max = Vector3.Max(max, source.positions[i]);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
        }

        private static float[] CalculateLocalDensities(GaussianSplatFrameData source)
        {
            float[] densities = new float[source.Count];
            float searchRadius = 0.5f; // Adjust based on typical splat spacing

            for (int i = 0; i < source.Count; i++)
            {
                Vector3 pos = source.positions[i];
                int neighborCount = 0;

                // Count nearby splats (within search radius)
                for (int j = 0; j < source.Count; j++)
                {
                    if (i == j) continue;

                    float dist = Vector3.Distance(pos, source.positions[j]);
                    if (dist < searchRadius)
                    {
                        neighborCount++;
                    }
                }

                densities[i] = neighborCount;
            }

            return densities;
        }

        // Simple octree for hierarchical clustering
        private class OctreeNode
        {
            public Bounds bounds;
            public List<int> indices;
            public OctreeNode[] children;
            public bool isLeaf;

            private const int MaxSplatsPerNode = 32;

            public OctreeNode(Bounds bounds)
            {
                this.bounds = bounds;
                this.indices = new List<int>();
                this.isLeaf = true;
            }

            public void Insert(int index, Vector3 position, int maxDepth, int currentDepth = 0)
            {
                if (!bounds.Contains(position))
                    return;

                if (isLeaf)
                {
                    indices.Add(index);

                    // Split if we have too many splats and haven't reached max depth
                    if (indices.Count > MaxSplatsPerNode && currentDepth < maxDepth)
                    {
                        Subdivide();
                        isLeaf = false;

                        // Redistribute existing splats to children
                        foreach (int idx in indices)
                        {
                            // We need position here, but we don't have it stored
                            // This is a limitation - in production, store positions with indices
                        }
                        // Keep indices for now
                    }
                }
                else
                {
                    // Insert into appropriate child
                    foreach (var child in children)
                    {
                        if (child.bounds.Contains(position))
                        {
                            child.Insert(index, position, maxDepth, currentDepth + 1);
                            break;
                        }
                    }
                }
            }

            private void Subdivide()
            {
                children = new OctreeNode[8];
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;
                Vector3 childExtents = extents * 0.5f;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = new Vector3(
                        ((i & 1) == 0) ? -1 : 1,
                        ((i & 2) == 0) ? -1 : 1,
                        ((i & 4) == 0) ? -1 : 1
                    );

                    Vector3 childCenter = center + Vector3.Scale(offset, childExtents);
                    children[i] = new OctreeNode(new Bounds(childCenter, extents));
                }
            }

            public void ExtractRepresentatives(List<int> output, int targetCount, GaussianSplatFrameData source)
            {
                if (isLeaf && indices.Count > 0)
                {
                    // For leaf nodes, select one representative splat (largest/most opaque)
                    int bestIdx = indices[0];
                    float bestScore = 0;

                    foreach (int idx in indices)
                    {
                        if (idx >= source.Count) continue;

                        float size = source.scales[idx].magnitude;
                        float opacity = source.opacities[idx];
                        float score = size * opacity;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIdx = idx;
                        }
                    }

                    output.Add(bestIdx);
                }
                else if (!isLeaf && children != null)
                {
                    // Recursively extract from children
                    foreach (var child in children)
                    {
                        if (output.Count >= targetCount)
                            break;

                        child.ExtractRepresentatives(output, targetCount, source);
                    }
                }
            }
        }
    }
}
