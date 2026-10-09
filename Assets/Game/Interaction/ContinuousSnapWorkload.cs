using System;
using Aedifica.Construction;
using Aedifica.Rendering;
using UnityEngine;

namespace Aedifica.Interaction
{
    // Fixed logical trajectories shared by the E2b.2 tests and standalone benchmark.
    public static class ContinuousSnapWorkload
    {
        public const int Seed = SpatialStressScenario.Seed;
        public const int Frames = 24;
        public const int Cases = 4;

        private static readonly float[] Offsets = {
            0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.35f, 0.45f,
            0.55f, 0.65f, 0.55f, 0.45f, 0.35f, 0.25f, 0.15f, 0.05f,
            0f, -0.1f, -0.2f, -0.35f, -0.2f, -0.1f, 0f, 0.1f
        };

        public static Vector3 Origin(int scenario)
        {
            switch (scenario)
            {
                case 0: return Vector3.zero; // dense center
                case 1: return new Vector3(-64f, 0f, 64f); // negative chunk boundary
                case 2: return new Vector3(1000000f, 0f, -1000000f); // float precision
                case 3: return new Vector3(-256f, 0f, -128f); // long target
                default: throw new ArgumentOutOfRangeException(nameof(scenario));
            }
        }

        public static ConstructionWorld World(int stressPieces)
        {
            var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
            if (stressPieces > 0)
                foreach (PieceData piece in SpatialStressScenario.Generate(stressPieces, Seed))
                    if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate stress piece ID.");
            for (int i = 0; i < Cases; i++)
            {
                Vector3 origin = Origin(i);
                float leftWidth = i == 3 ? 150f : 1f;
                float leftCenter = i == 3 ? -76f : -1.1f;
                PieceData left = new PieceData(Id(700000 + i * 2),
                    new PieceTransform(origin + Vector3.right * leftCenter, Quaternion.identity),
                    new BlockDimensions(leftWidth, 1f, 1f));
                PieceData right = new PieceData(Id(700001 + i * 2),
                    new PieceTransform(origin + Vector3.right * 1.1f, Quaternion.identity),
                    new BlockDimensions(1f, 1f, 1f));
                if (!world.Create(left).Changed || !world.Create(right).Changed)
                    throw new InvalidOperationException("Duplicate target ID.");
            }
            return world;
        }

        public static PieceData Initial(int scenario) => new PieceData(Id(800000 + scenario),
            new PieceTransform(Origin(scenario), Quaternion.identity), new BlockDimensions(1f, 1f, 1f));

        public static PieceData Raw(PieceData initial, int frame, bool faceResize)
        {
            if (frame < 0 || frame >= Frames) throw new ArgumentOutOfRangeException(nameof(frame));
            float offset = Offsets[frame];
            if (!faceResize)
                return initial.WithTransform(new PieceTransform(initial.Transform.Position + Vector3.right * offset,
                    initial.Transform.Rotation));
            var session = Session(initial, true);
            return session.ResizeToDimension(1f + offset);
        }

        public static ManipulationSession Session(PieceData initial, bool faceResize) =>
            new ManipulationSession(initial, faceResize ? ManipulationMode.Resize : ManipulationMode.Move,
                ManipulationAxis.X, Vector2.zero, Vector2.right, 100f, null,
                faceResize ? ResizeMode.Face : ResizeMode.Center, 1);

        public static SnapSettings Settings() => new SnapSettings {
            SurfaceSnapEnabled = true, EdgeSnapEnabled = true, EndpointSnapEnabled = true
        };

        private static PieceId Id(int value) => PieceId.Parse(value.ToString("x32"));
    }
}
