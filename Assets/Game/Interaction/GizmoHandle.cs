using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public sealed class GizmoHandle : MonoBehaviour
    {
        public ManipulationMode Mode { get; private set; }
        public ManipulationAxis Axis { get; private set; }
        public int FaceSign { get; private set; } = 1;
        public PieceType TargetPieceType { get; private set; }
        public string SemanticDimension => TargetPieceType == PieceType.Wall
            ? (Axis == ManipulationAxis.X ? "Length" : Axis == ManipulationAxis.Y ? "Height" : "Thickness")
            : TargetPieceType == PieceType.Slab
                ? (Axis == ManipulationAxis.X ? "Width" : Axis == ManipulationAxis.Y ? "Thickness" : "Depth")
            : TargetPieceType == PieceType.Beam
                ? (Axis == ManipulationAxis.X ? "Length" : Axis == ManipulationAxis.Y ? "Height" : "Width")
            : TargetPieceType == PieceType.Parapet
                ? (Axis == ManipulationAxis.X ? "Length" : Axis == ManipulationAxis.Y ? "Height" : "Thickness")
            : TargetPieceType == PieceType.FlatRoof || TargetPieceType == PieceType.ShedRoof ||
              TargetPieceType == PieceType.GableRoof || TargetPieceType == PieceType.HipRoof
                ? (Axis == ManipulationAxis.X ? "Width" : Axis == ManipulationAxis.Y ? "Thickness" : "Depth")
                : (Axis == ManipulationAxis.X ? "Width" : Axis == ManipulationAxis.Y ? "Height" : "Depth");

        public void Configure(ManipulationMode mode, ManipulationAxis axis, int faceSign = 1)
        {
            Mode = mode;
            Axis = axis;
            FaceSign = faceSign;
        }

        public void SetPieceType(PieceType pieceType) => TargetPieceType = pieceType;
    }
}
