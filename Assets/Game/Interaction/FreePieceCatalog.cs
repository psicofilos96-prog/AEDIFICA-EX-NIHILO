using System;
using Aedifica.Construction;
using Aedifica.Rendering;
using UnityEngine;

namespace Aedifica.Interaction
{
    // The temporary catalog only offers geometry families implemented by PieceView.
    public static class FreePieceCatalog
    {
        public static readonly PieceType[] Types = {
            PieceType.Block, PieceType.Wall, PieceType.Slab, PieceType.Column,
            PieceType.Beam, PieceType.Parapet, PieceType.FlatRoof, PieceType.ShedRoof,
            PieceType.GableRoof, PieceType.HipRoof, PieceType.Stair, PieceType.Ramp,
            PieceType.Arch, PieceType.Vault, PieceType.Dome
        };

        public static PieceData Create(PieceType type, PieceId id, Vector3 position, float yaw)
        {
            var pose = new PieceTransform(position, Quaternion.Euler(0f, yaw, 0f));
            PieceData piece;
            switch (type)
            {
                case PieceType.Block: piece = new PieceData(id, pose, new BlockDimensions(2f, 1f, 2f)); break;
                case PieceType.Wall: piece = new PieceData(id, pose, new WallDimensions(4f, 3f, 0.25f)); break;
                case PieceType.Slab: piece = new PieceData(id, pose, new SlabDimensions(4f, 0.2f, 4f)); break;
                case PieceType.Column: piece = new PieceData(id, pose, new ColumnDimensions(0.6f, 3f, 0.6f)); break;
                case PieceType.Beam: piece = new PieceData(id, pose, new BeamDimensions(4f, 0.5f, 0.5f)); break;
                case PieceType.Parapet: piece = new PieceData(id, pose, new ParapetDimensions(4f, 1f, 0.25f)); break;
                case PieceType.FlatRoof: piece = new PieceData(id, pose, new FlatRoofDimensions(4f, 4f, 0.25f)); break;
                case PieceType.ShedRoof: piece = new PieceData(id, pose, new ShedRoofDimensions(4f, 4f, 0.25f, 1f)); break;
                case PieceType.GableRoof: piece = new PieceData(id, pose, new GableRoofDimensions(4f, 4f, 0.25f, 1f)); break;
                case PieceType.HipRoof: piece = new PieceData(id, pose, new HipRoofDimensions(4f, 4f, 0.25f, 1f)); break;
                case PieceType.Stair: piece = new PieceData(id, pose, new StairDimensions(2f, 2f, 3f, 10)); break;
                case PieceType.Ramp: piece = new PieceData(id, pose, new RampDimensions(2f, 2f, 4f, 0.2f)); break;
                case PieceType.Arch: piece = new PieceData(id, pose, new ArchDimensions(4f, 3f, 0.7f, 0.6f, 1.2f, 0.25f)); break;
                case PieceType.Vault: piece = new PieceData(id, pose, new VaultDimensions(4f, 2.8f, 5f, 0.25f)); break;
                case PieceType.Dome: piece = new PieceData(id, pose, new DomeDimensions(4f, 2.5f, 0.25f)); break;
                default: throw new ArgumentOutOfRangeException(nameof(type), "No placement geometry for this piece type.");
            }
            return piece.WithMaterial(type == PieceType.Wall || type == PieceType.Column || type == PieceType.Arch
                ? LabMaterialIds.Stone : type == PieceType.Slab || type == PieceType.Dome
                    ? LabMaterialIds.Plaster : LabMaterialIds.Neutral);
        }
    }
}
