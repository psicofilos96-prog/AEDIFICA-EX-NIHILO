using System;
using System.Collections.Generic;

namespace Aedifica.Construction
{
    // Stores changed piece snapshots, not world copies. No UI binding in E1b.
    public sealed class ConstructionCommandHistory
    {
        private readonly ConstructionWorld world;
        private readonly List<ConstructionChangeSet> entries = new List<ConstructionChangeSet>();
        private int cursor;
        public int Capacity { get; }
        public int UndoCount => cursor;
        public int RedoCount => entries.Count - cursor;
        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor < entries.Count;

        public ConstructionCommandHistory(ConstructionWorld world, int capacity = 256)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public ConstructionChangeSet Create(PieceData piece) => Record(world.Create(piece));
        public ConstructionChangeSet Update(PieceId id, PieceData piece) => Record(world.Update(id, piece));
        public ConstructionChangeSet Delete(PieceId id) => Record(world.Delete(id));

        private ConstructionChangeSet Record(ConstructionChangeSet change)
        {
            if (!change.Changed) return change;
            if (cursor < entries.Count) entries.RemoveRange(cursor, entries.Count - cursor);
            entries.Add(change);
            if (entries.Count > Capacity) entries.RemoveAt(0);
            cursor = entries.Count;
            return change;
        }

        public bool TryUndo(out ConstructionChangeSet change)
        {
            change = default;
            if (!CanUndo) return false;
            ConstructionChangeSet command = entries[cursor - 1];
            if (!ExpectedState(command.PieceId, command.After)) return false;
            change = command.Operation == ConstructionOperation.Create ? world.Delete(command.PieceId)
                : command.Operation == ConstructionOperation.Delete ? world.Create(command.Before)
                : world.Update(command.PieceId, command.Before);
            if (!change.Changed) return false;
            cursor--;
            return true;
        }

        public bool TryRedo(out ConstructionChangeSet change)
        {
            change = default;
            if (!CanRedo) return false;
            ConstructionChangeSet command = entries[cursor];
            if (!ExpectedState(command.PieceId, command.Before)) return false;
            change = command.Operation == ConstructionOperation.Create ? world.Create(command.After)
                : command.Operation == ConstructionOperation.Delete ? world.Delete(command.PieceId)
                : world.Update(command.PieceId, command.After);
            if (!change.Changed) return false;
            cursor++;
            return true;
        }

        private bool ExpectedState(PieceId id, PieceData expected)
        {
            bool exists = world.TryGet(id, out PieceData current);
            return expected == null ? !exists : exists && ConstructionWorld.SameState(current, expected);
        }
    }
}
