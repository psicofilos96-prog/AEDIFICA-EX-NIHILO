using System;
using System.Collections.Generic;
using System.IO;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Versioned PVT-2 construction snapshot. Terrain and foliage are deterministic scene data.
    public static class Pvt2Snapshot
    {
        private const int Magic = 0x32545650; // PVT2
        private const int Version = 1;
        private const int MaximumPieces = 100000;

        public static byte[] Serialize(ConstructionWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var ordered = new List<PieceData>(world.Pieces);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write(Version); writer.Write(ordered.Count);
                foreach (PieceData piece in ordered)
                {
                    writer.Write(piece.Id.ToString());
                    writer.Write((int)piece.Type);
                    writer.Write(piece.MaterialId.ToString());
                    Vector3 p = piece.Transform.Position;
                    Quaternion q = piece.Transform.Rotation;
                    writer.Write(p.x); writer.Write(p.y); writer.Write(p.z);
                    writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w);
                    WriteDimensions(writer, piece);
                    writer.Write(piece.Openings.Count);
                    foreach (WallOpening opening in piece.Openings)
                    {
                        writer.Write(opening.Id.ToString("N"));
                        writer.Write((int)opening.Kind);
                        writer.Write(opening.Left); writer.Write(opening.Bottom);
                        writer.Write(opening.Width); writer.Write(opening.Height);
                    }
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        public static ConstructionWorld Deserialize(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version)
                    throw new InvalidDataException("Unsupported PVT-2 snapshot header.");
                int count = reader.ReadInt32();
                if (count < 0 || count > MaximumPieces) throw new InvalidDataException("Invalid piece count.");
                var world = new ConstructionWorld();
                for (int i = 0; i < count; i++)
                {
                    PieceId id = PieceId.Parse(reader.ReadString());
                    PieceType type = (PieceType)reader.ReadInt32();
                    MaterialId material = MaterialId.Parse(reader.ReadString());
                    var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    PieceDimensions dimensions = ReadDimensions(reader, type);
                    PieceData piece = new PieceData(id, new PieceTransform(position, rotation), dimensions, material);
                    int openings = reader.ReadInt32();
                    if (openings < 0 || openings > 1000) throw new InvalidDataException("Invalid opening count.");
                    for (int j = 0; j < openings; j++)
                    {
                        var opening = new WallOpening(Guid.Parse(reader.ReadString()), id,
                            (WallOpeningKind)reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(),
                            reader.ReadSingle(), reader.ReadSingle());
                        piece = piece.WithOpening(opening);
                    }
                    if (!world.Create(piece).Changed) throw new InvalidDataException("Duplicate PieceId.");
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing snapshot data.");
                return world;
            }
        }

        public static long Save(ConstructionWorld world, string path)
        {
            byte[] bytes = Serialize(world);
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return bytes.LongLength;
        }

        public static ConstructionWorld Load(string path) => Deserialize(File.ReadAllBytes(path));

        // PieceTransform normalizes a quaternion in its constructor, so a binary round-trip
        // can differ by one float ULP. Compare its effect on two independent camera axes.
        public static bool Matches(ConstructionWorld world, IReadOnlyDictionary<PieceId, PieceData> expected)
        {
            if (world == null || expected == null || world.Count != expected.Count ||
                world.SpatialIndex.IndexedPieceCount != expected.Count) return false;
            foreach (KeyValuePair<PieceId, PieceData> item in expected)
            {
                if (!world.TryGet(item.Key, out PieceData actual)) return false;
                PieceData prior = item.Value;
                if (prior.Type != actual.Type || !prior.Transform.Position.Equals(actual.Transform.Position) ||
                    !prior.Dimensions.Equals(actual.Dimensions) || prior.MaterialId != actual.MaterialId ||
                    prior.Openings.Count != actual.Openings.Count ||
                    Vector3.Distance(prior.Transform.Rotation * Vector3.forward,
                        actual.Transform.Rotation * Vector3.forward) > 0.000001f ||
                    Vector3.Distance(prior.Transform.Rotation * Vector3.right,
                        actual.Transform.Rotation * Vector3.right) > 0.000001f ||
                    world.SpatialIndex.ChunksFor(item.Key).Count == 0) return false;
                for (int i = 0; i < prior.Openings.Count; i++)
                    if (!prior.Openings[i].Equals(actual.Openings[i])) return false;
            }
            return true;
        }

        private static void WriteDimensions(BinaryWriter writer, PieceData piece)
        {
            var values = new float[6];
            int steps = 0;
            switch (piece.Type)
            {
                case PieceType.Block: { var d = piece.BlockDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Depth; break; }
                case PieceType.Wall: { var d = piece.WallDimensions; values[0]=d.Length; values[1]=d.Height; values[2]=d.Thickness; break; }
                case PieceType.Slab: { var d = piece.SlabDimensions; values[0]=d.Width; values[1]=d.Thickness; values[2]=d.Depth; break; }
                case PieceType.Column: { var d = piece.ColumnDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Depth; break; }
                case PieceType.Beam: { var d = piece.BeamDimensions; values[0]=d.Length; values[1]=d.Height; values[2]=d.Width; break; }
                case PieceType.Parapet: { var d = piece.ParapetDimensions; values[0]=d.Length; values[1]=d.Height; values[2]=d.Thickness; break; }
                case PieceType.FlatRoof: { var d = piece.FlatRoofDimensions; values[0]=d.Width; values[1]=d.Depth; values[2]=d.Thickness; break; }
                case PieceType.ShedRoof: { var d = piece.ShedRoofDimensions; values[0]=d.Width; values[1]=d.Depth; values[2]=d.Thickness; values[3]=d.Rise; break; }
                case PieceType.GableRoof: { var d = piece.GableRoofDimensions; values[0]=d.Width; values[1]=d.Depth; values[2]=d.Thickness; values[3]=d.Rise; break; }
                case PieceType.HipRoof: { var d = piece.HipRoofDimensions; values[0]=d.Width; values[1]=d.Depth; values[2]=d.Thickness; values[3]=d.Rise; break; }
                case PieceType.Stair: { var d = piece.StairDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Run; steps=d.StepCount; break; }
                case PieceType.Ramp: { var d = piece.RampDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Run; values[3]=d.Thickness; break; }
                case PieceType.Arch: { var d = piece.ArchDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Depth; values[3]=d.PierWidth; values[4]=d.ArchRise; values[5]=d.CrownThickness; break; }
                case PieceType.Vault: { var d = piece.VaultDimensions; values[0]=d.Width; values[1]=d.Height; values[2]=d.Length; values[3]=d.Thickness; break; }
                case PieceType.Dome: { var d = piece.DomeDimensions; values[0]=d.Diameter; values[1]=d.Rise; values[2]=d.Thickness; break; }
                default: throw new InvalidDataException("Unsupported piece type: " + piece.Type);
            }
            foreach (float value in values) writer.Write(value);
            writer.Write(steps);
        }

        private static PieceDimensions ReadDimensions(BinaryReader reader, PieceType type)
        {
            float a=reader.ReadSingle(), b=reader.ReadSingle(), c=reader.ReadSingle();
            float d=reader.ReadSingle(), e=reader.ReadSingle(), f=reader.ReadSingle();
            int steps=reader.ReadInt32();
            switch (type)
            {
                case PieceType.Block: return new PieceDimensions(new BlockDimensions(a,b,c));
                case PieceType.Wall: return new PieceDimensions(new WallDimensions(a,b,c));
                case PieceType.Slab: return new PieceDimensions(new SlabDimensions(a,b,c));
                case PieceType.Column: return new PieceDimensions(new ColumnDimensions(a,b,c));
                case PieceType.Beam: return new PieceDimensions(new BeamDimensions(a,b,c));
                case PieceType.Parapet: return new PieceDimensions(new ParapetDimensions(a,b,c));
                case PieceType.FlatRoof: return new PieceDimensions(new FlatRoofDimensions(a,b,c));
                case PieceType.ShedRoof: return new PieceDimensions(new ShedRoofDimensions(a,b,c,d));
                case PieceType.GableRoof: return new PieceDimensions(new GableRoofDimensions(a,b,c,d));
                case PieceType.HipRoof: return new PieceDimensions(new HipRoofDimensions(a,b,c,d));
                case PieceType.Stair: return new PieceDimensions(new StairDimensions(a,b,c,steps));
                case PieceType.Ramp: return new PieceDimensions(new RampDimensions(a,b,c,d));
                case PieceType.Arch: return new PieceDimensions(new ArchDimensions(a,b,c,d,e,f));
                case PieceType.Vault: return new PieceDimensions(new VaultDimensions(a,b,c,d));
                case PieceType.Dome: return new PieceDimensions(new DomeDimensions(a,b,c));
                default: throw new InvalidDataException("Unsupported piece type: " + type);
            }
        }
    }
}
