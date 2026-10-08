using System;

namespace Aedifica.Construction
{
    public static class WallOpeningLayout
    {
        public static void Validate(PieceId wallId, PieceDimensions dimensions, WallOpening[] openings)
        {
            if (openings.Length == 0) return;
            if (dimensions.Type != PieceType.Wall)
                throw new ArgumentException("Only walls can contain openings.", nameof(dimensions));
            float margin = WallOpening.MinimumSolid;
            for (int i = 0; i < openings.Length; i++)
            {
                WallOpening a = openings[i];
                if (a.Id == Guid.Empty || a.HostWallId != wallId ||
                    a.Left < margin || a.Right > dimensions.X - margin ||
                    a.Top > dimensions.Y - margin || a.Bottom < 0f ||
                    a.Kind == WallOpeningKind.Window && a.Bottom < margin ||
                    a.Width < WallOpening.MinimumSize || a.Height < WallOpening.MinimumSize)
                    throw new ArgumentException("Opening is outside the wall's valid local area.", nameof(openings));
                for (int j = 0; j < i; j++)
                {
                    WallOpening b = openings[j];
                    if (a.Id == b.Id) throw new ArgumentException("Opening IDs must be unique.", nameof(openings));
                    bool separated = a.Left >= b.Right + margin || b.Left >= a.Right + margin ||
                        a.Bottom >= b.Top + margin || b.Bottom >= a.Top + margin;
                    if (!separated) throw new ArgumentException("Openings overlap or leave insufficient solid wall between them.", nameof(openings));
                }
            }
        }
    }
}
