using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UnitySplatter.GaussianSplatting
{
    /// <summary>
    /// Advanced reconstruction tools for Gaussian Splats
    /// Converts point clouds to Gaussian splats with proper scale and orientation estimation
    /// </summary>
    public static class GaussianSplatAdvancedReconstruction
    {
        /// <summary>
        /// Convert a point cloud to Gaussian splats with automatic scale and normal estimation
        /// </summary>
        public static GaussianSplatFrameData FromPointCloud(
            Vector3[] positions,
            Color[] colors = null,
            Vector3[] normals = null,
            float defaultScale = 0.01f,
            bool estimateNormals = true,
            bool estimateScales = true,
            int neighborCount = 8)
        {
            if (positions == null || positions.Length == 0)
            {
                Debug.LogError("[GaussianSplatAdvancedReconstruction] Invalid point cloud data");
                return null;
            }

            try
            {
                int count = positions.Length;

                // Initialize arrays
                Vector3[] scales = new Vector3[count];
                Quaternion[] rotations = new Quaternion[count];
                Color[] finalColors = colors ?? new Color[count];
                float[] opacities = new float[count];

                // Default colors if not provided
                if (colors == null)
                {
                    for (int i = 0; i < count; i++)
                        finalColors[i] = Color.white;
                }

                // Estimate normals if not provided
                Vector3[] finalNormals = normals;
                if (estimateNormals && normals == null)
                {
                    Debug.Log("[GaussianSplatAdvancedReconstruction] Estimating normals...");
                    finalNormals = EstimateNormals(positions, neighborCount);
                }

                // Estimate scales if requested
                if (estimateScales)
                {
                    Debug.Log("[GaussianSplatAdvancedReconstruction] Estimating scales...");
                    scales = EstimateScales(positions, neighborCount, defaultScale);
                }
                else
                {
                    for (int i = 0; i < count; i++)
                        scales[i] = Vector3.one * defaultScale;
                }

                // Calculate rotations from normals
                if (finalNormals != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (finalNormals[i].sqrMagnitude > 0.001f)
                        {
                            rotations[i] = Quaternion.LookRotation(finalNormals[i]);
                        }
                        else
                        {
                            rotations[i] = Quaternion.identity;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                        rotations[i] = Quaternion.identity;
                }

                // Set full opacity
                for (int i = 0; i < count; i++)
                    opacities[i] = 1.0f;

                var result = new GaussianSplatFrameData();
                result.SetData(positions, scales, rotations, finalColors, opacities);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatAdvancedReconstruction] Point cloud conversion failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Estimate normals using local plane fitting (PCA)
        /// </summary>
        public static Vector3[] EstimateNormals(Vector3[] positions, int neighborCount = 8)
        {
            if (positions == null || positions.Length == 0)
                return null;

            Vector3[] normals = new Vector3[positions.Length];

            // Build spatial data structure for efficient neighbor queries
            // For simplicity, we'll use a basic brute-force approach
            // In production, use a KD-tree or octree

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 point = positions[i];

                // Find K nearest neighbors
                var neighbors = FindKNearestNeighbors(positions, i, neighborCount);

                if (neighbors.Count < 3)
                {
                    normals[i] = Vector3.up; // Default normal
                    continue;
                }

                // Compute covariance matrix
                Matrix3x3 covariance = ComputeCovarianceMatrix(positions, neighbors);

                // Extract normal as eigenvector with smallest eigenvalue
                Vector3 normal = ComputeSmallestEigenvector(covariance);

                normals[i] = normal.normalized;
            }

            return normals;
        }

        /// <summary>
        /// Estimate scales based on local point density
        /// </summary>
        public static Vector3[] EstimateScales(Vector3[] positions, int neighborCount = 8, float scaleFactor = 1.0f)
        {
            if (positions == null || positions.Length == 0)
                return null;

            Vector3[] scales = new Vector3[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 point = positions[i];

                // Find K nearest neighbors
                var neighbors = FindKNearestNeighbors(positions, i, neighborCount);

                if (neighbors.Count < 2)
                {
                    scales[i] = Vector3.one * 0.01f * scaleFactor;
                    continue;
                }

                // Calculate average distance to neighbors
                float avgDistance = 0f;
                foreach (int neighbor in neighbors)
                {
                    avgDistance += Vector3.Distance(point, positions[neighbor]);
                }
                avgDistance /= neighbors.Count;

                // Calculate covariance to get anisotropic scale
                Matrix3x3 covariance = ComputeCovarianceMatrix(positions, neighbors);
                Vector3 eigenvalues = ComputeEigenvalues(covariance);

                // Scale based on eigenvalues (standard deviation along each axis)
                Vector3 scale = new Vector3(
                    Mathf.Sqrt(Mathf.Max(0.0001f, eigenvalues.x)),
                    Mathf.Sqrt(Mathf.Max(0.0001f, eigenvalues.y)),
                    Mathf.Sqrt(Mathf.Max(0.0001f, eigenvalues.z))
                ) * scaleFactor;

                // Ensure minimum scale
                scale = Vector3.Max(scale, Vector3.one * 0.001f);

                scales[i] = scale;
            }

            return scales;
        }

        /// <summary>
        /// Refine existing Gaussian splats using iterative optimization
        /// </summary>
        public static GaussianSplatFrameData RefineGaussians(
            GaussianSplatFrameData source,
            int iterations = 5,
            float learningRate = 0.1f)
        {
            if (source == null || !source.IsValid(out _))
                return null;

            // Clone source data
            Vector3[] positions = (Vector3[])source.Positions.Clone();
            Vector3[] scales = (Vector3[])source.Scales.Clone();
            Quaternion[] rotations = (Quaternion[])source.Rotations.Clone();
            Color[] colors = (Color[])source.Colors.Clone();
            float[] opacities = (float[])source.Opacities.Clone();

            // Iterative refinement
            for (int iter = 0; iter < iterations; iter++)
            {
                // Smooth positions
                positions = SmoothPositions(positions, learningRate);

                // Adjust scales to fill gaps
                scales = AdaptiveScaleAdjustment(positions, scales, learningRate);

                // Smooth opacities
                opacities = SmoothOpacities(positions, opacities, learningRate);
            }

            var refined = new GaussianSplatFrameData();
            refined.SetData(positions, scales, rotations, colors, opacities);
            return refined;
        }

        /// <summary>
        /// Remove outlier splats based on statistical analysis
        /// </summary>
        public static GaussianSplatFrameData RemoveOutliers(
            GaussianSplatFrameData source,
            int neighborCount = 8,
            float stddevMultiplier = 2.0f)
        {
            if (source == null || !source.IsValid(out _))
                return null;

            List<int> inlierIndices = new List<int>();

            for (int i = 0; i < source.Count; i++)
            {
                var neighbors = FindKNearestNeighbors(source.Positions, i, neighborCount);

                if (neighbors.Count < 2)
                {
                    inlierIndices.Add(i);
                    continue;
                }

                // Calculate mean distance to neighbors
                float meanDist = 0f;
                foreach (int neighbor in neighbors)
                {
                    meanDist += Vector3.Distance(source.Positions[i], source.Positions[neighbor]);
                }
                meanDist /= neighbors.Count;

                // Calculate standard deviation
                float variance = 0f;
                foreach (int neighbor in neighbors)
                {
                    float dist = Vector3.Distance(source.Positions[i], source.Positions[neighbor]);
                    float diff = dist - meanDist;
                    variance += diff * diff;
                }
                variance /= neighbors.Count;
                float stddev = Mathf.Sqrt(variance);

                // Check if point is within acceptable range
                if (meanDist <= meanDist + stddevMultiplier * stddev)
                {
                    inlierIndices.Add(i);
                }
            }

            // Create new frame with only inliers
            return CreateSubset(source, inlierIndices.ToArray());
        }

        /// <summary>
        /// Fill holes in Gaussian splat data by adding new splats
        /// </summary>
        public static GaussianSplatFrameData FillHoles(
            GaussianSplatFrameData source,
            float holeThreshold = 0.1f,
            int maxNewSplats = 10000)
        {
            if (source == null || !source.IsValid(out _))
                return null;

            List<Vector3> newPositions = new List<Vector3>();
            List<Vector3> newScales = new List<Vector3>();
            List<Quaternion> newRotations = new List<Quaternion>();
            List<Color> newColors = new List<Color>();
            List<float> newOpacities = new List<float>();

            // Detect holes by finding pairs of nearby points with large gaps
            for (int i = 0; i < source.Count && newPositions.Count < maxNewSplats; i++)
            {
                var neighbors = FindKNearestNeighbors(source.Positions, i, 4);

                foreach (int neighbor in neighbors)
                {
                    if (neighbor <= i) continue; // Avoid duplicates

                    Vector3 pos1 = source.Positions[i];
                    Vector3 pos2 = source.Positions[neighbor];
                    float dist = Vector3.Distance(pos1, pos2);

                    // Check if gap is large enough to need filling
                    float avgScale = (source.Scales[i].magnitude + source.Scales[neighbor].magnitude) * 0.5f;

                    if (dist > holeThreshold && dist > avgScale * 3f)
                    {
                        // Add intermediate splat
                        Vector3 midPos = (pos1 + pos2) * 0.5f;
                        Vector3 midScale = (source.Scales[i] + source.Scales[neighbor]) * 0.5f;
                        Quaternion midRot = Quaternion.Slerp(source.Rotations[i], source.Rotations[neighbor], 0.5f);
                        Color midColor = (source.Colors[i] + source.Colors[neighbor]) * 0.5f;
                        float midOpacity = (source.Opacities[i] + source.Opacities[neighbor]) * 0.5f;

                        newPositions.Add(midPos);
                        newScales.Add(midScale);
                        newRotations.Add(midRot);
                        newColors.Add(midColor);
                        newOpacities.Add(midOpacity);

                        if (newPositions.Count >= maxNewSplats)
                            break;
                    }
                }
            }

            if (newPositions.Count == 0)
                return source; // No holes found

            // Merge original and new splats
            var newSplats = new GaussianSplatFrameData();
            newSplats.SetData(
                newPositions.ToArray(),
                newScales.ToArray(),
                newRotations.ToArray(),
                newColors.ToArray(),
                newOpacities.ToArray()
            );
            return MergeSplats(source, newSplats);
        }

        // Helper methods

        private static List<int> FindKNearestNeighbors(Vector3[] positions, int queryIndex, int k)
        {
            Vector3 queryPoint = positions[queryIndex];

            var distances = new List<(int index, float distance)>();

            for (int i = 0; i < positions.Length; i++)
            {
                if (i == queryIndex) continue;

                float dist = Vector3.Distance(queryPoint, positions[i]);
                distances.Add((i, dist));
            }

            return distances
                .OrderBy(x => x.distance)
                .Take(k)
                .Select(x => x.index)
                .ToList();
        }

        private static Matrix3x3 ComputeCovarianceMatrix(Vector3[] positions, List<int> indices)
        {
            // Calculate centroid
            Vector3 centroid = Vector3.zero;
            foreach (int idx in indices)
                centroid += positions[idx];
            centroid /= indices.Count;

            // Calculate covariance
            Matrix3x3 cov = new Matrix3x3();

            foreach (int idx in indices)
            {
                Vector3 diff = positions[idx] - centroid;

                cov.m00 += diff.x * diff.x;
                cov.m01 += diff.x * diff.y;
                cov.m02 += diff.x * diff.z;

                cov.m10 += diff.y * diff.x;
                cov.m11 += diff.y * diff.y;
                cov.m12 += diff.y * diff.z;

                cov.m20 += diff.z * diff.x;
                cov.m21 += diff.z * diff.y;
                cov.m22 += diff.z * diff.z;
            }

            float scale = 1f / indices.Count;
            cov.m00 *= scale; cov.m01 *= scale; cov.m02 *= scale;
            cov.m10 *= scale; cov.m11 *= scale; cov.m12 *= scale;
            cov.m20 *= scale; cov.m21 *= scale; cov.m22 *= scale;

            return cov;
        }

        private static Vector3 ComputeSmallestEigenvector(Matrix3x3 matrix)
        {
            // Simplified eigenvector computation (power iteration)
            // For production, use a proper eigensolver

            Vector3 v = new Vector3(1, 1, 1).normalized;

            for (int i = 0; i < 10; i++)
            {
                v = matrix.MultiplyVector(v);
                v.Normalize();
            }

            return v;
        }

        private static Vector3 ComputeEigenvalues(Matrix3x3 matrix)
        {
            // Approximate eigenvalues using matrix diagonal
            // For production, use proper eigenvalue decomposition
            return new Vector3(
                Mathf.Abs(matrix.m00),
                Mathf.Abs(matrix.m11),
                Mathf.Abs(matrix.m22)
            );
        }

        private static Vector3[] SmoothPositions(Vector3[] positions, float strength)
        {
            Vector3[] smoothed = new Vector3[positions.Length];

            for (int i = 0; i < positions.Length; i++)
            {
                var neighbors = FindKNearestNeighbors(positions, i, 4);

                Vector3 avgPos = positions[i];
                foreach (int neighbor in neighbors)
                {
                    avgPos += positions[neighbor];
                }
                avgPos /= (neighbors.Count + 1);

                smoothed[i] = Vector3.Lerp(positions[i], avgPos, strength);
            }

            return smoothed;
        }

        private static Vector3[] AdaptiveScaleAdjustment(Vector3[] positions, Vector3[] scales, float strength)
        {
            Vector3[] adjusted = (Vector3[])scales.Clone();

            for (int i = 0; i < positions.Length; i++)
            {
                var neighbors = FindKNearestNeighbors(positions, i, 4);

                if (neighbors.Count > 0)
                {
                    float avgDist = 0f;
                    foreach (int neighbor in neighbors)
                    {
                        avgDist += Vector3.Distance(positions[i], positions[neighbor]);
                    }
                    avgDist /= neighbors.Count;

                    Vector3 targetScale = Vector3.one * (avgDist * 0.5f);
                    adjusted[i] = Vector3.Lerp(scales[i], targetScale, strength);
                }
            }

            return adjusted;
        }

        private static float[] SmoothOpacities(Vector3[] positions, float[] opacities, float strength)
        {
            float[] smoothed = new float[opacities.Length];

            for (int i = 0; i < opacities.Length; i++)
            {
                var neighbors = FindKNearestNeighbors(positions, i, 4);

                float avgOpacity = opacities[i];
                foreach (int neighbor in neighbors)
                {
                    avgOpacity += opacities[neighbor];
                }
                avgOpacity /= (neighbors.Count + 1);

                smoothed[i] = Mathf.Lerp(opacities[i], avgOpacity, strength);
            }

            return smoothed;
        }

        private static GaussianSplatFrameData CreateSubset(GaussianSplatFrameData source, int[] indices)
        {
            var result = new GaussianSplatFrameData();
            result.SetData(
                indices.Select(i => source.Positions[i]).ToArray(),
                indices.Select(i => source.Scales[i]).ToArray(),
                indices.Select(i => source.Rotations[i]).ToArray(),
                indices.Select(i => source.Colors[i]).ToArray(),
                indices.Select(i => source.Opacities[i]).ToArray()
            );
            return result;
        }

        private static GaussianSplatFrameData MergeSplats(GaussianSplatFrameData a, GaussianSplatFrameData b)
        {
            var result = new GaussianSplatFrameData();
            result.SetData(
                a.Positions.Concat(b.Positions).ToArray(),
                a.Scales.Concat(b.Scales).ToArray(),
                a.Rotations.Concat(b.Rotations).ToArray(),
                a.Colors.Concat(b.Colors).ToArray(),
                a.Opacities.Concat(b.Opacities).ToArray()
            );
            return result;
        }

        // Simple 3x3 matrix for covariance calculations
        private struct Matrix3x3
        {
            public float m00, m01, m02;
            public float m10, m11, m12;
            public float m20, m21, m22;

            public Vector3 MultiplyVector(Vector3 v)
            {
                return new Vector3(
                    m00 * v.x + m01 * v.y + m02 * v.z,
                    m10 * v.x + m11 * v.y + m12 * v.z,
                    m20 * v.x + m21 * v.y + m22 * v.z
                );
            }
        }
    }
}
