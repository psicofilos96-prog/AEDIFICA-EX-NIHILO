using System;
using Aedifica.Construction;

namespace Aedifica.Interaction
{
    public sealed class SelectionState
    {
        private PieceId? selectedPieceId;

        public bool HasSelection => selectedPieceId.HasValue;
        public PieceId? SelectedPieceId => selectedPieceId;

        public void Select(PieceId id)
        {
            if (!id.IsValid) throw new ArgumentException("Selection requires a valid PieceId.", nameof(id));
            selectedPieceId = id;
        }

        public void Clear() => selectedPieceId = null;
    }
}
