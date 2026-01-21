using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    public static class PlyReader
    {
        public enum PlyFormat
        {
            Ascii,
            BinaryLittleEndian,
            BinaryBigEndian
        }

        public sealed class PlyHeader
        {
            public PlyFormat Format;
            public int VertexCount;
            public List<PlyProperty> Properties = new List<PlyProperty>();
        }

        public readonly struct PlyProperty
        {
            public PlyProperty(string name, string type, bool isList, string listCountType, string listItemType)
            {
                Name = name;
                Type = type;
                IsList = isList;
                ListCountType = listCountType;
                ListItemType = listItemType;
            }

            public string Name { get; }
            public string Type { get; }
            public bool IsList { get; }
            public string ListCountType { get; }
            public string ListItemType { get; }
        }

        public static GaussianPoint[] Load(string path, out Bounds bounds)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is null or empty.", nameof(path));
            }

            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            PlyHeader header = ReadHeader(reader);
            bounds = new Bounds(Vector3.zero, Vector3.zero);

            if (header.VertexCount <= 0)
            {
                return Array.Empty<GaussianPoint>();
            }

            if (header.Format == PlyFormat.Ascii)
            {
                return ReadAscii(reader, header, ref bounds);
            }

            long dataStart = stream.Position;
            stream.Position = dataStart;
            return ReadBinary(stream, header, ref bounds);
        }

        public static PlyHeader ReadHeader(StreamReader reader)
        {
            string line = reader.ReadLine();
            if (line == null || !line.StartsWith("ply", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Not a valid PLY file.");
            }

            var header = new PlyHeader();
            bool headerEnded = false;
            while (!reader.EndOfStream && !headerEnded)
            {
                line = reader.ReadLine();
                if (line == null)
                {
                    break;
                }

                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    continue;
                }

                switch (tokens[0])
                {
                    case "format":
                        header.Format = tokens[1] switch
                        {
                            "ascii" => PlyFormat.Ascii,
                            "binary_little_endian" => PlyFormat.BinaryLittleEndian,
                            "binary_big_endian" => PlyFormat.BinaryBigEndian,
                            _ => throw new InvalidDataException("Unsupported PLY format.")
                        };
                        break;
                    case "element":
                        if (tokens.Length >= 3 && tokens[1] == "vertex")
                        {
                            header.VertexCount = int.Parse(tokens[2], CultureInfo.InvariantCulture);
                        }
                        break;
                    case "property":
                        if (tokens.Length >= 3)
                        {
                            if (tokens[1] == "list" && tokens.Length >= 5)
                            {
                                header.Properties.Add(new PlyProperty(tokens[^1], string.Empty, true, tokens[2], tokens[3]));
                            }
                            else
                            {
                                header.Properties.Add(new PlyProperty(tokens[^1], tokens[1], false, string.Empty, string.Empty));
                            }
                        }
                        break;
                    case "end_header":
                        headerEnded = true;
                        break;
                }
            }

            if (!headerEnded)
            {
                throw new InvalidDataException("PLY header is incomplete.");
            }

            return header;
        }

        private static GaussianPoint[] ReadAscii(StreamReader reader, PlyHeader header, ref Bounds bounds)
        {
            var points = new GaussianPoint[header.VertexCount];
            bool boundsInitialized = false;
            int lineIndex = 0;
            while (!reader.EndOfStream && lineIndex < header.VertexCount)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                points[lineIndex] = ParsePoint(tokens, header.Properties);
                UpdateBounds(points[lineIndex].Position, ref bounds, ref boundsInitialized);
                lineIndex++;
            }

            return points;
        }

        private static GaussianPoint[] ReadBinary(Stream stream, PlyHeader header, ref Bounds bounds)
        {
            if (header.Format == PlyFormat.BinaryBigEndian)
            {
                throw new NotSupportedException("Binary big endian is not supported for performance reasons.");
            }

            var points = new GaussianPoint[header.VertexCount];
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);
            bool boundsInitialized = false;
            int propertyCount = header.Properties.Count;

            for (int i = 0; i < header.VertexCount; i++)
            {
                var values = new object[propertyCount];
                for (int p = 0; p < propertyCount; p++)
                {
                    PlyProperty prop = header.Properties[p];
                    if (prop.IsList)
                    {
                        int listCount = ReadValueAsInt(reader, prop.ListCountType);
                        for (int l = 0; l < listCount; l++)
                        {
                            ReadValue(reader, prop.ListItemType);
                        }
                        values[p] = 0f;
                        continue;
                    }

                    values[p] = ReadValue(reader, prop.Type);
                }

                points[i] = ParsePoint(values, header.Properties);
                UpdateBounds(points[i].Position, ref bounds, ref boundsInitialized);
            }

            return points;
        }

        private static GaussianPoint ParsePoint(string[] tokens, List<PlyProperty> props)
        {
            var values = new object[props.Count];
            for (int i = 0; i < props.Count && i < tokens.Length; i++)
            {
                if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    values[i] = value;
                }
                else
                {
                    values[i] = 0f;
                }
            }

            return ParsePoint(values, props);
        }

        private static GaussianPoint ParsePoint(object[] values, List<PlyProperty> props)
        {
            var point = new GaussianPoint
            {
                Scale = Vector3.one,
                Rotation = Quaternion.identity,
                Color = Color.white,
                Opacity = 1f
            };

            for (int i = 0; i < props.Count; i++)
            {
                string name = props[i].Name;
                float value = values[i] is float f ? f : 0f;

                switch (name)
                {
                    case "x": point.Position.x = value; break;
                    case "y": point.Position.y = value; break;
                    case "z": point.Position.z = value; break;
                    case "scale_0": point.Scale.x = Mathf.Max(0.0001f, value); break;
                    case "scale_1": point.Scale.y = Mathf.Max(0.0001f, value); break;
                    case "scale_2": point.Scale.z = Mathf.Max(0.0001f, value); break;
                    case "rot_0": point.Rotation.x = value; break;
                    case "rot_1": point.Rotation.y = value; break;
                    case "rot_2": point.Rotation.z = value; break;
                    case "rot_3": point.Rotation.w = value; break;
                    case "qx": point.Rotation.x = value; break;
                    case "qy": point.Rotation.y = value; break;
                    case "qz": point.Rotation.z = value; break;
                    case "qw": point.Rotation.w = value; break;
                    case "red": point.Color.r = value / 255f; break;
                    case "green": point.Color.g = value / 255f; break;
                    case "blue": point.Color.b = value / 255f; break;
                    case "alpha": point.Opacity = value; break;
                    case "opacity": point.Opacity = value; break;
                    case "f_dc_0": point.SH0 = value; break;
                    case "f_dc_1": point.SH1 = value; break;
                    case "f_dc_2": point.SH2 = value; break;
                    case "f_rest_0": point.SH3 = value; break;
                    case "f_rest_1": point.SH4 = value; break;
                    case "f_rest_2": point.SH5 = value; break;
                    case "f_rest_3": point.SH6 = value; break;
                    case "f_rest_4": point.SH7 = value; break;
                    case "f_rest_5": point.SH8 = value; break;
                }
            }

            point.Rotation = NormalizeQuaternion(point.Rotation);
            return point;
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag <= 0.0001f)
            {
                return Quaternion.identity;
            }

            float inv = 1f / mag;
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        private static void UpdateBounds(Vector3 position, ref Bounds bounds, ref bool initialized)
        {
            if (!initialized)
            {
                bounds = new Bounds(position, Vector3.zero);
                initialized = true;
                return;
            }

            bounds.Encapsulate(position);
        }

        private static object ReadValue(BinaryReader reader, string type)
        {
            return type switch
            {
                "char" => (float)reader.ReadSByte(),
                "uchar" => (float)reader.ReadByte(),
                "short" => (float)reader.ReadInt16(),
                "ushort" => (float)reader.ReadUInt16(),
                "int" => (float)reader.ReadInt32(),
                "uint" => (float)reader.ReadUInt32(),
                "float" => reader.ReadSingle(),
                "double" => (float)reader.ReadDouble(),
                _ => reader.ReadSingle()
            };
        }

        private static int ReadValueAsInt(BinaryReader reader, string type)
        {
            return type switch
            {
                "char" => reader.ReadSByte(),
                "uchar" => reader.ReadByte(),
                "short" => reader.ReadInt16(),
                "ushort" => reader.ReadUInt16(),
                "int" => reader.ReadInt32(),
                "uint" => (int)reader.ReadUInt32(),
                "float" => Mathf.RoundToInt(reader.ReadSingle()),
                "double" => Mathf.RoundToInt((float)reader.ReadDouble()),
                _ => reader.ReadInt32()
            };
        }
    }
}
