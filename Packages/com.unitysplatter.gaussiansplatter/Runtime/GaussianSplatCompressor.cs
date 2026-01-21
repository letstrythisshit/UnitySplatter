using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UnitySplatter.GaussianSplatting
{
    /// <summary>
    /// Compresses and decompresses Gaussian splat data using quantization
    /// Supports both CPU and GPU compression methods
    /// </summary>
    public static class GaussianSplatCompressor
    {
        /// <summary>
        /// Compressed frame data structure
        /// </summary>
        [Serializable]
        public class CompressedFrameData
        {
            public uint[] compressedPositions;
            public uint[] compressedScales;
            public uint[] compressedRotations;
            public uint[] compressedColors;
            public uint[] compressedOpacities;

            public Vector3 boundsMin;
            public Vector3 boundsMax;
            public Vector3 scaleMin;
            public Vector3 scaleMax;

            public int originalCount;

            public int GetCompressedSize()
            {
                int size = 0;
                size += compressedPositions?.Length * sizeof(uint) ?? 0;
                size += compressedScales?.Length * sizeof(uint) ?? 0;
                size += compressedRotations?.Length * sizeof(uint) ?? 0;
                size += compressedColors?.Length * sizeof(uint) ?? 0;
                size += compressedOpacities?.Length * sizeof(uint) ?? 0;
                size += sizeof(float) * 12; // Bounds and scale min/max
                size += sizeof(int); // Original count
                return size;
            }

            public float GetCompressionRatio(int originalSize)
            {
                return (float)originalSize / GetCompressedSize();
            }
        }

        /// <summary>
        /// Compress a frame using CPU quantization
        /// </summary>
        public static CompressedFrameData CompressCPU(GaussianSplatFrameData source)
        {
            if (source == null || !source.IsValid(out _) || source.Count == 0)
            {
                Debug.LogError("[GaussianSplatCompressor] Invalid source data");
                return null;
            }

            try
            {
                CompressedFrameData compressed = new CompressedFrameData
                {
                    originalCount = source.Count
                };

                // Calculate bounds for quantization
                CalculateBounds(source, out compressed.boundsMin, out compressed.boundsMax,
                               out compressed.scaleMin, out compressed.scaleMax);

                // Allocate compressed buffers
                compressed.compressedPositions = new uint[source.Count * 2]; // 2 uints per position (3 floats)
                compressed.compressedScales = new uint[source.Count * 2]; // 2 uints per scale
                compressed.compressedRotations = new uint[source.Count * 2]; // 2 uints per rotation (4 floats)
                compressed.compressedColors = new uint[source.Count]; // 1 uint per color (RGBA8)
                compressed.compressedOpacities = new uint[(source.Count + 3) / 4]; // Pack 4 opacities per uint

                // Compress each component
                for (int i = 0; i < source.Count; i++)
                {
                    // Compress position (3 floats -> 2 uints, 16 bits each)
                    CompressPosition(source.Positions[i], compressed.boundsMin, compressed.boundsMax,
                                   compressed.compressedPositions, i * 2);

                    // Compress scale
                    CompressScale(source.Scales[i], compressed.scaleMin, compressed.scaleMax,
                                compressed.compressedScales, i * 2);

                    // Compress rotation
                    CompressRotation(source.Rotations[i], compressed.compressedRotations, i * 2);

                    // Compress color
                    compressed.compressedColors[i] = CompressColor(source.Colors[i]);

                    // Compress opacity (4 per uint)
                    CompressOpacity(source.Opacities[i], compressed.compressedOpacities, i);
                }

                return compressed;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatCompressor] Compression failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Decompress a frame using CPU
        /// </summary>
        public static GaussianSplatFrameData DecompressCPU(CompressedFrameData compressed)
        {
            if (compressed == null || compressed.originalCount == 0)
            {
                Debug.LogError("[GaussianSplatCompressor] Invalid compressed data");
                return null;
            }

            try
            {
                // Create arrays for decompressed data
                Vector3[] positions = new Vector3[compressed.originalCount];
                Vector3[] scales = new Vector3[compressed.originalCount];
                Quaternion[] rotations = new Quaternion[compressed.originalCount];
                Color[] colors = new Color[compressed.originalCount];
                float[] opacities = new float[compressed.originalCount];

                // Decompress each component
                for (int i = 0; i < compressed.originalCount; i++)
                {
                    // Decompress position
                    positions[i] = DecompressPosition(compressed.compressedPositions, i * 2,
                                                     compressed.boundsMin, compressed.boundsMax);

                    // Decompress scale
                    scales[i] = DecompressScale(compressed.compressedScales, i * 2,
                                               compressed.scaleMin, compressed.scaleMax);

                    // Decompress rotation
                    rotations[i] = DecompressRotation(compressed.compressedRotations, i * 2);

                    // Decompress color
                    colors[i] = DecompressColor(compressed.compressedColors[i]);

                    // Decompress opacity
                    opacities[i] = DecompressOpacity(compressed.compressedOpacities, i);
                }

                // Create frame data and set the arrays
                GaussianSplatFrameData decompressed = new GaussianSplatFrameData();
                decompressed.SetData(positions, scales, rotations, colors, opacities);

                return decompressed;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatCompressor] Decompression failed: {e.Message}");
                return null;
            }
        }

        // Helper methods for compression

        private static void CalculateBounds(GaussianSplatFrameData source,
                                          out Vector3 boundsMin, out Vector3 boundsMax,
                                          out Vector3 scaleMin, out Vector3 scaleMax)
        {
            boundsMin = source.Positions[0];
            boundsMax = source.Positions[0];
            scaleMin = source.Scales[0];
            scaleMax = source.Scales[0];

            for (int i = 1; i < source.Count; i++)
            {
                boundsMin = Vector3.Min(boundsMin, source.Positions[i]);
                boundsMax = Vector3.Max(boundsMax, source.Positions[i]);
                scaleMin = Vector3.Min(scaleMin, source.Scales[i]);
                scaleMax = Vector3.Max(scaleMax, source.Scales[i]);
            }

            // Add small epsilon to avoid division by zero
            boundsMax += Vector3.one * 0.001f;
            scaleMax += Vector3.one * 0.001f;
        }

        private static uint FloatToUint16(float value, float min, float max)
        {
            float normalized = Mathf.Clamp01((value - min) / (max - min));
            return (uint)(normalized * 65535f);
        }

        private static float Uint16ToFloat(uint value, float min, float max)
        {
            float normalized = value / 65535f;
            return Mathf.Lerp(min, max, normalized);
        }

        private static void CompressPosition(Vector3 pos, Vector3 min, Vector3 max, uint[] buffer, int index)
        {
            uint x = FloatToUint16(pos.x, min.x, max.x);
            uint y = FloatToUint16(pos.y, min.y, max.y);
            uint z = FloatToUint16(pos.z, min.z, max.z);

            buffer[index] = (x << 16) | y;
            buffer[index + 1] = z;
        }

        private static Vector3 DecompressPosition(uint[] buffer, int index, Vector3 min, Vector3 max)
        {
            uint packed1 = buffer[index];
            uint packed2 = buffer[index + 1];

            uint x = (packed1 >> 16) & 0xFFFF;
            uint y = packed1 & 0xFFFF;
            uint z = packed2 & 0xFFFF;

            return new Vector3(
                Uint16ToFloat(x, min.x, max.x),
                Uint16ToFloat(y, min.y, max.y),
                Uint16ToFloat(z, min.z, max.z)
            );
        }

        private static void CompressScale(Vector3 scale, Vector3 min, Vector3 max, uint[] buffer, int index)
        {
            CompressPosition(scale, min, max, buffer, index);
        }

        private static Vector3 DecompressScale(uint[] buffer, int index, Vector3 min, Vector3 max)
        {
            return DecompressPosition(buffer, index, min, max);
        }

        private static void CompressRotation(Quaternion q, uint[] buffer, int index)
        {
            // Normalize quaternion
            q = Quaternion.Normalize(q);

            // Convert to [0, 1] range
            Vector4 positive = new Vector4(
                q.x * 0.5f + 0.5f,
                q.y * 0.5f + 0.5f,
                q.z * 0.5f + 0.5f,
                q.w * 0.5f + 0.5f
            );

            uint x = (uint)(Mathf.Clamp01(positive.x) * 65535f);
            uint y = (uint)(Mathf.Clamp01(positive.y) * 65535f);
            uint z = (uint)(Mathf.Clamp01(positive.z) * 65535f);
            uint w = (uint)(Mathf.Clamp01(positive.w) * 65535f);

            buffer[index] = (x << 16) | y;
            buffer[index + 1] = (z << 16) | w;
        }

        private static Quaternion DecompressRotation(uint[] buffer, int index)
        {
            uint packed1 = buffer[index];
            uint packed2 = buffer[index + 1];

            uint x = (packed1 >> 16) & 0xFFFF;
            uint y = packed1 & 0xFFFF;
            uint z = (packed2 >> 16) & 0xFFFF;
            uint w = packed2 & 0xFFFF;

            Vector4 positive = new Vector4(
                x / 65535f,
                y / 65535f,
                z / 65535f,
                w / 65535f
            );

            // Convert back from [0, 1] to [-1, 1]
            Quaternion q = new Quaternion(
                positive.x * 2f - 1f,
                positive.y * 2f - 1f,
                positive.z * 2f - 1f,
                positive.w * 2f - 1f
            );

            return Quaternion.Normalize(q);
        }

        private static uint CompressColor(Color color)
        {
            uint r = (uint)(Mathf.Clamp01(color.r) * 255f);
            uint g = (uint)(Mathf.Clamp01(color.g) * 255f);
            uint b = (uint)(Mathf.Clamp01(color.b) * 255f);
            uint a = (uint)(Mathf.Clamp01(color.a) * 255f);

            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static Color DecompressColor(uint packed)
        {
            uint r = (packed >> 24) & 0xFF;
            uint g = (packed >> 16) & 0xFF;
            uint b = (packed >> 8) & 0xFF;
            uint a = packed & 0xFF;

            return new Color(
                r / 255f,
                g / 255f,
                b / 255f,
                a / 255f
            );
        }

        private static void CompressOpacity(float opacity, uint[] buffer, int splatIndex)
        {
            int bufferIndex = splatIndex / 4;
            int offset = (splatIndex % 4) * 8;

            uint opacityByte = (uint)(Mathf.Clamp01(opacity) * 255f);
            uint packed = opacityByte << offset;

            // Atomic-like OR operation (not thread-safe, but fine for single-threaded CPU)
            buffer[bufferIndex] |= packed;
        }

        private static float DecompressOpacity(uint[] buffer, int splatIndex)
        {
            int bufferIndex = splatIndex / 4;
            int offset = (splatIndex % 4) * 8;

            uint packed = buffer[bufferIndex];
            uint opacityByte = (packed >> offset) & 0xFF;

            return opacityByte / 255f;
        }

        // File I/O for compressed data

        public static void SaveCompressed(CompressedFrameData compressed, string filePath)
        {
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
                {
                    // Write header
                    writer.Write("GSPC"); // Magic number: Gaussian Splat Compressed
                    writer.Write(1); // Version
                    writer.Write(compressed.originalCount);

                    // Write bounds
                    WriteVector3(writer, compressed.boundsMin);
                    WriteVector3(writer, compressed.boundsMax);
                    WriteVector3(writer, compressed.scaleMin);
                    WriteVector3(writer, compressed.scaleMax);

                    // Write compressed data
                    WriteUintArray(writer, compressed.compressedPositions);
                    WriteUintArray(writer, compressed.compressedScales);
                    WriteUintArray(writer, compressed.compressedRotations);
                    WriteUintArray(writer, compressed.compressedColors);
                    WriteUintArray(writer, compressed.compressedOpacities);
                }

                Debug.Log($"[GaussianSplatCompressor] Saved compressed data to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatCompressor] Failed to save compressed data: {e.Message}");
            }
        }

        public static CompressedFrameData LoadCompressed(string filePath)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
                {
                    // Read header
                    string magic = reader.ReadString();
                    if (magic != "GSPC")
                    {
                        Debug.LogError("[GaussianSplatCompressor] Invalid file format");
                        return null;
                    }

                    int version = reader.ReadInt32();
                    if (version != 1)
                    {
                        Debug.LogError($"[GaussianSplatCompressor] Unsupported version: {version}");
                        return null;
                    }

                    CompressedFrameData compressed = new CompressedFrameData
                    {
                        originalCount = reader.ReadInt32()
                    };

                    // Read bounds
                    compressed.boundsMin = ReadVector3(reader);
                    compressed.boundsMax = ReadVector3(reader);
                    compressed.scaleMin = ReadVector3(reader);
                    compressed.scaleMax = ReadVector3(reader);

                    // Read compressed data
                    compressed.compressedPositions = ReadUintArray(reader);
                    compressed.compressedScales = ReadUintArray(reader);
                    compressed.compressedRotations = ReadUintArray(reader);
                    compressed.compressedColors = ReadUintArray(reader);
                    compressed.compressedOpacities = ReadUintArray(reader);

                    return compressed;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatCompressor] Failed to load compressed data: {e.Message}");
                return null;
            }
        }

        private static void WriteVector3(BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.x);
            writer.Write(v.y);
            writer.Write(v.z);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static void WriteUintArray(BinaryWriter writer, uint[] array)
        {
            writer.Write(array.Length);
            foreach (uint value in array)
            {
                writer.Write(value);
            }
        }

        private static uint[] ReadUintArray(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            uint[] array = new uint[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = reader.ReadUInt32();
            }
            return array;
        }

        // Utility methods

        public static int GetUncompressedSize(GaussianSplatFrameData frame)
        {
            if (frame == null || !frame.IsValid(out _))
                return 0;

            int size = 0;
            size += frame.Count * sizeof(float) * 3; // Positions
            size += frame.Count * sizeof(float) * 3; // Scales
            size += frame.Count * sizeof(float) * 4; // Rotations
            size += frame.Count * sizeof(float) * 4; // Colors
            size += frame.Count * sizeof(float); // Opacities

            return size;
        }

        public static void PrintCompressionStats(GaussianSplatFrameData original, CompressedFrameData compressed)
        {
            int originalSize = GetUncompressedSize(original);
            int compressedSize = compressed.GetCompressedSize();
            float ratio = compressed.GetCompressionRatio(originalSize);

            Debug.Log($"[GaussianSplatCompressor] Compression Stats:\n" +
                     $"  Original Size: {originalSize / 1024f:F2} KB\n" +
                     $"  Compressed Size: {compressedSize / 1024f:F2} KB\n" +
                     $"  Compression Ratio: {ratio:F2}x\n" +
                     $"  Space Saved: {(1f - 1f / ratio) * 100f:F1}%");
        }
    }
}
