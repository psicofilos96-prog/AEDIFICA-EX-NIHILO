using System;
using System.Collections.Generic;
using System.Diagnostics;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // The benchmark calls one step per interval, so intermediate edits are rendered.
    public static class Pvt2EditWorkload
    {
        public struct Sample
        {
            public string Operation;
            public float Milliseconds;
            public int RebuiltRegions;
        }

        public sealed class Session
        {
            private readonly ConstructionWorld world;
            private readonly PvtChunkVisualEngine engine;
            private readonly ConstructionCommandHistory history;
            private readonly PieceData roof, created, moved, resized, recolored;
            private readonly int originalCount;
            private int step;

            public bool Complete => step == 15;
            public PieceId CreatedId => created.Id;

            public Session(ConstructionWorld world, PvtChunkVisualEngine engine, int seed, int cycle)
            {
                this.world=world ?? throw new ArgumentNullException(nameof(world));
                this.engine=engine ?? throw new ArgumentNullException(nameof(engine));
                originalCount=world.Count;
                PieceData expected=Pvt2Scenario.PieceAt(5,seed);
                if (!world.TryGet(expected.Id,out roof))
                    throw new InvalidOperationException("Missing PVT-2 selection target.");
                history=new ConstructionCommandHistory(world);
                PieceId id=PieceId.Parse(new Guid(seed,(short)cycle,(short)101,
                    0x50,0x56,0x54,0x32,0,0,0,3).ToString("N"));
                created=new PieceData(id,new PieceTransform(
                    roof.Transform.Position+new Vector3(20f,0f,0f),Quaternion.identity),
                    new BlockDimensions(1f,2f,1f)).WithMaterial(LabMaterialIds.Stone);
                moved=created.WithTransform(new PieceTransform(
                    created.Transform.Position+new Vector3(0.4f,0f,0f),created.Transform.Rotation));
                resized=moved.WithDimensions(moved.Dimensions.Resize(0,1.5f));
                recolored=resized.WithMaterial(LabMaterialIds.Brick);
            }

            public Sample Next()
            {
                if (Complete) throw new InvalidOperationException("PVT-2 edit session already finished.");
                int before=engine.RebuiltRegions;
                Stopwatch timer=Stopwatch.StartNew();
                string name;
                switch (step)
                {
                    case 0:
                        name="select";
                        Physics.SyncTransforms();
                        var ray=new Ray(roof.Transform.Position+new Vector3(0f,30f,0f),Vector3.down);
                        if (!engine.TryPick(ray,60f,out PieceId picked) || picked!=roof.Id)
                            throw new InvalidOperationException("Selection did not resolve the roof PieceId.");
                        break;
                    case 1: name="begin_edit"; engine.BeginEdit(roof.Id); break;
                    case 2: name="end_edit"; engine.EndEdit(roof.Id); break;
                    case 3: name="create"; Require(history.Create(created)); break;
                    case 4: name="move"; Require(history.Update(created.Id,moved)); break;
                    case 5: name="resize"; Require(history.Update(created.Id,resized)); break;
                    case 6: name="recolor"; Require(history.Update(created.Id,recolored)); break;
                    case 7: name="delete"; Require(history.Delete(created.Id)); break;
                    case 8: name="undo_delete"; RequireUndo(); break;
                    case 9: name="redo_delete"; RequireRedo(); break;
                    case 10: name="restore_delete"; RequireUndo(); break;
                    case 11: name="undo_recolor"; RequireUndo(); break;
                    case 12: name="undo_resize"; RequireUndo(); break;
                    case 13: name="undo_move"; RequireUndo(); break;
                    default: name="remove_created"; Require(history.Delete(created.Id)); break;
                }
                engine.RebuildDirty();
                timer.Stop();
                step++;
                if (Complete && (world.Count!=originalCount || world.TryGet(created.Id,out _) ||
                    !world.TryGet(roof.Id,out PieceData restoredRoof) ||
                    !roof.Transform.Equals(restoredRoof.Transform) ||
                    !roof.Dimensions.Equals(restoredRoof.Dimensions) ||
                    roof.MaterialId!=restoredRoof.MaterialId ||
                    engine.PieceCount!=originalCount || engine.DirtyRegionCount!=0))
                    throw new InvalidOperationException("Edit session did not restore the world.");
                return new Sample { Operation=name, Milliseconds=(float)timer.Elapsed.TotalMilliseconds,
                    RebuiltRegions=engine.RebuiltRegions-before };
            }

            private static void Require(ConstructionChangeSet change)
            {
                if (!change.Changed) throw new InvalidOperationException("Edit did not change the world.");
            }
            private void RequireUndo()
            {
                if (!history.TryUndo(out ConstructionChangeSet change) || !change.Changed)
                    throw new InvalidOperationException("Undo failed.");
            }
            private void RequireRedo()
            {
                if (!history.TryRedo(out ConstructionChangeSet change) || !change.Changed)
                    throw new InvalidOperationException("Redo failed.");
            }
        }

        // Convenient deterministic integration probe for the Unity Test Runner.
        public static List<Sample> Run(ConstructionWorld world,PvtChunkVisualEngine engine,int seed,int cycle)
        {
            var session=new Session(world,engine,seed,cycle);
            var samples=new List<Sample>(15);
            while (!session.Complete) samples.Add(session.Next());
            return samples;
        }
    }
}
