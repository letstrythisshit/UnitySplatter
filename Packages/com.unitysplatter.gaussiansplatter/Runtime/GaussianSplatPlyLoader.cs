using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    public static class GaussianSplatPlyLoader
    {
        private const string PlyMagic = "ply";

        private enum PlyFormat
        {
            Ascii,
            BinaryLittleEndian
        }

        private sealed class PlyHeader
        {
            public PlyFormat Format;
            public int VertexCount;
            public readonly List<PlyProperty> Properties = new();
        }

        private sealed class PlyProperty
        {
            public string Name = string.Empty;
            public string Type = string.Empty;
        }

        public static GaussianSplatFrameData LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("PLY file path must be provided.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"PLY file not found at {filePath}.", filePath);
            }

            using FileStream stream = File.OpenRead(filePath);
            return LoadFromStream(stream, Path.GetFileName(filePath));
        }

        public static GaussianSplatFrameData LoadFromBytes(byte[] data, string nameForErrors)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("PLY data must be provided.", nameof(data));
            }

            using MemoryStream stream = new(data, writable: false);
            return LoadFromStream(stream, nameForErrors);
        }

        private static GaussianSplatFrameData LoadFromStream(Stream stream, string nameForErrors)
        {
            using MemoryStream memory = new();
            stream.CopyTo(memory);
            byte[] data = memory.ToArray();

            PlyHeader header = ParseHeader(data, nameForErrors, out int dataStart);
            GaussianSplatFrameData frame = new();

            using MemoryStream dataStream = new(data, dataStart, data.Length - dataStart, writable: false);
            if (header.Format == PlyFormat.Ascii)
            {
                using StreamReader reader = new(dataStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                Vector3[] positions = ReadAsciiVertices(reader, header, nameForErrors, out Vector3[] scales, out Quaternion[] rotations, out Color[] colors, out float[] opacities);
                frame.SetData(positions, scales, rotations, colors, opacities);
                return frame;
            }

            if (header.Format == PlyFormat.BinaryLittleEndian)
            {
                using BinaryReader binaryReader = new(dataStream, Encoding.ASCII, leaveOpen: true);
                ReadBinaryVertices(binaryReader, header, nameForErrors, out Vector3[] positions, out Vector3[] scales, out Quaternion[] rotations, out Color[] colors, out float[] opacities);
                frame.SetData(positions, scales, rotations, colors, opacities);
                return frame;
            }

            throw new NotSupportedException($"PLY format not supported in {nameForErrors}.");
        }

        private static PlyHeader ParseHeader(byte[] data, string nameForErrors, out int dataStart)
        {
            int headerEnd = FindHeaderEnd(data);
            if (headerEnd < 0)
            {
                throw new InvalidDataException($"PLY header in {nameForErrors} is incomplete.");
            }

            using MemoryStream headerStream = new(data, 0, headerEnd, writable: false);
            using StreamReader reader = new(headerStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            string? line = reader.ReadLine();
            if (!string.Equals(line, PlyMagic, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{nameForErrors} is not a valid PLY file.");
            }

            PlyHeader header = new();
            bool inVertexElement = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("format ", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("ascii", StringComparison.OrdinalIgnoreCase))
                    {
                        header.Format = PlyFormat.Ascii;
                    }
                    else if (line.Contains("binary_little_endian", StringComparison.OrdinalIgnoreCase))
                    {
                        header.Format = PlyFormat.BinaryLittleEndian;
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported PLY format in {nameForErrors}.");
                    }
                }
                else if (line.StartsWith("element ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length >= 3 && tokens[1] == "vertex")
                    {
                        inVertexElement = true;
                        header.VertexCount = int.Parse(tokens[2], CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        inVertexElement = false;
                    }
                }
                else if (line.StartsWith("property ", StringComparison.OrdinalIgnoreCase))
                {
                    if (inVertexElement)
                    {
                        string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 3)
                        {
                            header.Properties.Add(new PlyProperty { Type = tokens[1], Name = tokens[2] });
                        }
                    }
                }
                else if (string.Equals(line, "end_header", StringComparison.OrdinalIgnoreCase))
                {
                    dataStart = headerEnd;
                    return header;
                }
            }

            throw new InvalidDataException($"PLY header in {nameForErrors} is incomplete.");
        }

        private static int FindHeaderEnd(byte[] data)
        {
            byte[] endHeader = Encoding.ASCII.GetBytes("end_header");
            for (int i = 0; i <= data.Length - endHeader.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < endHeader.Length; j++)
                {
                    if (data[i + j] != endHeader[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                {
                    continue;
                }

                int newlineIndex = i + endHeader.Length;
                while (newlineIndex < data.Length && (data[newlineIndex] == '\r' || data[newlineIndex] == '\n'))
                {
                    if (data[newlineIndex] == '\n')
                    {
                        return newlineIndex + 1;
                    }

                    newlineIndex++;
                }
            }

            return -1;
        }

        private static Vector3[] ReadAsciiVertices(StreamReader reader, PlyHeader header, string nameForErrors, out Vector3[] scales, out Quaternion[] rotations, out Color[] colors, out float[] opacities)
        {
            Vector3[] positions = new Vector3[header.VertexCount];
            scales = new Vector3[header.VertexCount];
            rotations = new Quaternion[header.VertexCount];
            colors = new Color[header.VertexCount];
            opacities = new float[header.VertexCount];

            for (int i = 0; i < header.VertexCount; i++)
            {
                string? line = reader.ReadLine();
                if (line == null)
                {
                    throw new EndOfStreamException($"Unexpected end of PLY data in {nameForErrors}.");
                }

                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < header.Properties.Count)
                {
                    throw new InvalidDataException($"PLY vertex {i} in {nameForErrors} has {tokens.Length} properties but expected {header.Properties.Count}.");
                }

                ReadVertexProperties(tokens, header.Properties, i, positions, scales, rotations, colors, opacities);
            }

            return positions;
        }

        private static void ReadBinaryVertices(BinaryReader reader, PlyHeader header, string nameForErrors, out Vector3[] positions, out Vector3[] scales, out Quaternion[] rotations, out Color[] colors, out float[] opacities)
        {
            positions = new Vector3[header.VertexCount];
            scales = new Vector3[header.VertexCount];
            rotations = new Quaternion[header.VertexCount];
            colors = new Color[header.VertexCount];
            opacities = new float[header.VertexCount];

            for (int i = 0; i < header.VertexCount; i++)
            {
                Dictionary<string, float> values = new();
                for (int p = 0; p < header.Properties.Count; p++)
                {
                    PlyProperty property = header.Properties[p];
                    values[property.Name] = ReadBinaryValue(reader, property.Type);
                }

                ApplyVertexValues(values, i, positions, scales, rotations, colors, opacities);
            }
        }

        private static void ReadVertexProperties(string[] tokens, List<PlyProperty> properties, int index, Vector3[] positions, Vector3[] scales, Quaternion[] rotations, Color[] colors, float[] opacities)
        {
            Dictionary<string, float> values = new();
            for (int p = 0; p < properties.Count; p++)
            {
                float value = float.Parse(tokens[p], CultureInfo.InvariantCulture);
                values[properties[p].Name] = value;
            }

            ApplyVertexValues(values, index, positions, scales, rotations, colors, opacities);
        }

        private static void ApplyVertexValues(Dictionary<string, float> values, int index, Vector3[] positions, Vector3[] scales, Quaternion[] rotations, Color[] colors, float[] opacities)
        {
            float x = GetValue(values, "x");
            float y = GetValue(values, "y");
            float z = GetValue(values, "z");

            positions[index] = new Vector3(x, y, z);

            float sx = GetValue(values, "scale_0", "scale_x", "sx", fallback: 0.01f);
            float sy = GetValue(values, "scale_1", "scale_y", "sy", fallback: 0.01f);
            float sz = GetValue(values, "scale_2", "scale_z", "sz", fallback: 0.01f);
            scales[index] = new Vector3(sx, sy, sz);

            float qx = GetValue(values, "rot_0", "qx", fallback: 0f);
            float qy = GetValue(values, "rot_1", "qy", fallback: 0f);
            float qz = GetValue(values, "rot_2", "qz", fallback: 0f);
            float qw = GetValue(values, "rot_3", "qw", fallback: 1f);
            rotations[index] = new Quaternion(qx, qy, qz, qw);

            float r = GetValue(values, "red", "r", fallback: 1f) / 255f;
            float g = GetValue(values, "green", "g", fallback: 1f) / 255f;
            float b = GetValue(values, "blue", "b", fallback: 1f) / 255f;
            float a = GetValue(values, "alpha", "opacity", fallback: 1f);

            colors[index] = new Color(r, g, b, 1f);
            opacities[index] = Mathf.Clamp01(a);
        }

        private static float GetValue(Dictionary<string, float> values, string primary, string alternate1 = "", string alternate2 = "", float fallback = 0f)
        {
            if (values.TryGetValue(primary, out float value))
            {
                return value;
            }

            if (!string.IsNullOrEmpty(alternate1) && values.TryGetValue(alternate1, out value))
            {
                return value;
            }

            if (!string.IsNullOrEmpty(alternate2) && values.TryGetValue(alternate2, out value))
            {
                return value;
            }

            return fallback;
        }

        private static float ReadBinaryValue(BinaryReader reader, string type)
        {
            return type switch
            {
                "float" or "float32" => reader.ReadSingle(),
                "double" or "float64" => (float)reader.ReadDouble(),
                "uchar" or "uint8" => reader.ReadByte(),
                "char" or "int8" => reader.ReadSByte(),
                "ushort" or "uint16" => reader.ReadUInt16(),
                "short" or "int16" => reader.ReadInt16(),
                "uint" or "uint32" => reader.ReadUInt32(),
                "int" or "int32" => reader.ReadInt32(),
                _ => throw new NotSupportedException($"Unsupported PLY property type {type}.")
            };
        }
    }
}
