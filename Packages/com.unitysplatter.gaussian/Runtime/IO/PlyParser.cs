using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.IO
{
    internal static class PlyParser
    {
        private const string HeaderEnd = "end_header";

        public static List<GaussianSplatData> Load(string path, out Bounds bounds)
        {
            Guard.NotNull(path, nameof(path));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("PLY file not found.", path);
            }

            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            var header = ReadHeader(reader, out var format, out var vertexCount, out var properties);
            if (!header)
            {
                throw new InvalidDataException("PLY header is invalid or missing end_header.");
            }

            if (vertexCount <= 0)
            {
                bounds = new Bounds();
                return new List<GaussianSplatData>();
            }

            return format == PlyFormat.Ascii
                ? ReadAscii(reader, vertexCount, properties, out bounds)
                : ReadBinary(stream, vertexCount, properties, out bounds);
        }

        private static bool ReadHeader(StreamReader reader, out PlyFormat format, out int vertexCount, out List<PlyProperty> properties)
        {
            format = PlyFormat.Ascii;
            vertexCount = 0;
            properties = new List<PlyProperty>();

            string line = reader.ReadLine();
            if (line == null || !line.StartsWith("ply", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("format", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("binary_little_endian"))
                    {
                        format = PlyFormat.BinaryLittleEndian;
                    }
                    else if (line.Contains("ascii"))
                    {
                        format = PlyFormat.Ascii;
                    }
                }
                else if (line.StartsWith("element vertex", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                    {
                        vertexCount = count;
                    }
                }
                else if (line.StartsWith("property", StringComparison.OrdinalIgnoreCase))
                {
                    var property = PlyProperty.Parse(line);
                    if (property != null)
                    {
                        properties.Add(property);
                    }
                }
                else if (line.StartsWith(HeaderEnd, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GaussianSplatData> ReadAscii(StreamReader reader, int vertexCount, List<PlyProperty> properties, out Bounds bounds)
        {
            var data = new List<GaussianSplatData>(vertexCount);
            bounds = new Bounds();
            var hasBounds = false;

            var tokens = new List<string>(properties.Count);
            for (var i = 0; i < vertexCount; i++)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                tokens.Clear();
                tokens.AddRange(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (tokens.Count < properties.Count)
                {
                    throw new InvalidDataException($"PLY vertex {i} has insufficient properties.");
                }

                var splat = ParseTokens(tokens, properties);
                data.Add(splat);

                if (!hasBounds)
                {
                    bounds = new Bounds(splat.Position, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(splat.Position);
                }
            }

            return data;
        }

        private static List<GaussianSplatData> ReadBinary(Stream stream, int vertexCount, List<PlyProperty> properties, out Bounds bounds)
        {
            var data = new List<GaussianSplatData>(vertexCount);
            bounds = new Bounds();
            var hasBounds = false;
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);

            var buffer = ArrayPool<byte>.Shared.Rent(256);
            try
            {
                for (var i = 0; i < vertexCount; i++)
                {
                    var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    foreach (var property in properties)
                    {
                        var value = property.Read(reader, buffer);
                        values[property.Name] = value;
                    }

                    var splat = MapToSplat(values);
                    data.Add(splat);

                    if (!hasBounds)
                    {
                        bounds = new Bounds(splat.Position, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(splat.Position);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return data;
        }

        private static GaussianSplatData ParseTokens(List<string> tokens, List<PlyProperty> properties)
        {
            var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < properties.Count; i++)
            {
                if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    value = 0f;
                }
                values[properties[i].Name] = value;
            }

            return MapToSplat(values);
        }

        private static GaussianSplatData MapToSplat(Dictionary<string, float> values)
        {
            var position = new Vector3(
                Get(values, "x"),
                Get(values, "y"),
                Get(values, "z"));

            var scale = new Vector3(
                Get(values, "scale_0", Get(values, "sx", 1f)),
                Get(values, "scale_1", Get(values, "sy", 1f)),
                Get(values, "scale_2", Get(values, "sz", 1f)));

            var rotation = new Quaternion(
                Get(values, "rot_1", Get(values, "qx")),
                Get(values, "rot_2", Get(values, "qy")),
                Get(values, "rot_3", Get(values, "qz")),
                Get(values, "rot_0", Get(values, "qw", 1f)));

            var color = new Color(
                NormalizeColor(values, "r", "red"),
                NormalizeColor(values, "g", "green"),
                NormalizeColor(values, "b", "blue"),
                1f);

            var opacity = Get(values, "opacity", Get(values, "alpha", 1f));

            return new GaussianSplatData(position, scale, rotation, color, opacity);
        }

        private static float Get(Dictionary<string, float> values, string key, float fallback = 0f)
        {
            return values.TryGetValue(key, out var value) ? value : fallback;
        }

        private static float NormalizeColor(Dictionary<string, float> values, string primaryKey, string fallbackKey)
        {
            var value = Get(values, primaryKey, Get(values, fallbackKey, 1f));
            return value > 1f ? value / 255f : value;
        }

        private enum PlyFormat
        {
            Ascii,
            BinaryLittleEndian
        }

        private sealed class PlyProperty
        {
            public string Name { get; }
            public PlyType Type { get; }

            private PlyProperty(string name, PlyType type)
            {
                Name = name;
                Type = type;
            }

            public static PlyProperty Parse(string line)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    return null;
                }

                var type = PlyTypeExtensions.Parse(parts[1]);
                if (type == PlyType.Invalid)
                {
                    return null;
                }

                return new PlyProperty(parts[2], type);
            }

            public float Read(BinaryReader reader, byte[] buffer)
            {
                return Type switch
                {
                    PlyType.Float => reader.ReadSingle(),
                    PlyType.Double => (float)reader.ReadDouble(),
                    PlyType.UChar => reader.ReadByte(),
                    PlyType.Int => reader.ReadInt32(),
                    PlyType.UInt => reader.ReadUInt32(),
                    PlyType.Short => reader.ReadInt16(),
                    PlyType.UShort => reader.ReadUInt16(),
                    _ => 0f
                };
            }
        }

        private enum PlyType
        {
            Invalid,
            Float,
            Double,
            UChar,
            Int,
            UInt,
            Short,
            UShort
        }

        private static class PlyTypeExtensions
        {
            public static PlyType Parse(string token)
            {
                return token switch
                {
                    "float" => PlyType.Float,
                    "float32" => PlyType.Float,
                    "double" => PlyType.Double,
                    "uchar" => PlyType.UChar,
                    "uint8" => PlyType.UChar,
                    "int" => PlyType.Int,
                    "int32" => PlyType.Int,
                    "uint" => PlyType.UInt,
                    "uint32" => PlyType.UInt,
                    "short" => PlyType.Short,
                    "int16" => PlyType.Short,
                    "ushort" => PlyType.UShort,
                    "uint16" => PlyType.UShort,
                    _ => PlyType.Invalid
                };
            }
        }
    }
}
