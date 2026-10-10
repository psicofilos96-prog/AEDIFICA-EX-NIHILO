using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // One compact eastern Mediterranean inspired settlement; no prefab buildings.
    public static class PvtMedievalVillageScenario
    {
        public const int Seed=48621;
        public const int ResidenceCount=6;

        public static List<PieceData> Build()
        {
            var pieces=new List<PieceData>();
            AddCastle(pieces);
            AddTradingHall(pieces);
            var plots=new[] {
                new Vector4(-22f,-23f,90f,0f),new Vector4(22f,-23f,-90f,1f),
                new Vector4(-22f,-1f,90f,2f),new Vector4(22f,-1f,-90f,0f),
                new Vector4(-24f,21f,90f,1f),new Vector4(24f,21f,-90f,2f) };
            for (int i=0;i<plots.Length;i++)
            {
                Vector4 plot=plots[i];
                pieces.AddRange(PvtVisualCityScenario.BuildResidenceAt(3100+i,
                    plot.x,plot.y,plot.z,(int)plot.w));
            }
            return pieces;
        }

        private sealed class Author
        {
            private readonly List<PieceData> pieces;
            private readonly int region;
            private readonly Vector3 origin;
            private int part;
            public Author(List<PieceData> output,int region,float x,float z)
            { pieces=output;this.region=region;origin=new Vector3(x,Pvt2Scenario.GroundHeight(x,z),z); }
            public PieceTransform At(float x,float y,float z,float yaw=0f) =>
                new PieceTransform(origin+new Vector3(x,y,z),Quaternion.Euler(0f,yaw,0f));
            private PieceId NextId() => PieceId.Parse(new Guid(Seed,(short)region,(short)part++,
                0x56,0x49,0x4c,0x4c,0x41,0x47,0,1).ToString("N"));
            public PieceData Add(PieceTransform transform,PieceDimensions dimensions,MaterialId material)
            {
                var piece=new PieceData(NextId(),transform,dimensions,material);
                pieces.Add(piece);return piece;
            }
            public PieceData Block(float x,float y,float z,float w,float h,float d,MaterialId material,float yaw=0f) =>
                Add(At(x,y,z,yaw),new PieceDimensions(new BlockDimensions(w,h,d)),material);
            public PieceData Slab(float x,float y,float z,float w,float t,float d,MaterialId material) =>
                Add(At(x,y,z),new PieceDimensions(new SlabDimensions(w,t,d)),material);
            public PieceData Wall(float x,float y,float z,float length,float height,float thickness,
                MaterialId material,float yaw=0f,params OpeningSpec[] openings)
            {
                PieceId id=NextId();
                var wall=new PieceData(id,At(x,y,z,yaw),new WallDimensions(length,height,thickness))
                    .WithMaterial(material);
                for (int i=0;i<openings.Length;i++)
                {
                    OpeningSpec spec=openings[i];
                    Guid openingId=new Guid(Seed+1,(short)region,(short)(part-1),
                        0x4f,0x50,0x45,0x4e,0x49,0x4e,0,(byte)i);
                    wall=wall.WithOpening(new WallOpening(openingId,id,spec.Kind,
                        spec.Left,spec.Bottom,spec.Width,spec.Height));
                    ArchitecturalFinishKind frame=spec.Kind==WallOpeningKind.Passage
                        ? ArchitecturalFinishKind.DoorFrame : ArchitecturalFinishKind.WindowFrameSill;
                    wall=wall.WithFinish(new ArchitecturalFinish(new Guid(Seed+2,(short)region,
                        (short)(part-1),0x46,0x52,0x41,0x4d,0x45,0,0,(byte)i),
                        id,openingId,frame,LabMaterialIds.Stone));
                    wall=wall.WithFinish(new ArchitecturalFinish(new Guid(Seed+3,(short)region,
                        (short)(part-1),0x4c,0x49,0x4e,0x54,0x45,0,0,(byte)i),
                        id,openingId,ArchitecturalFinishKind.Lintel,LabMaterialIds.Stone));
                }
                pieces.Add(wall);return wall;
            }
            public PieceData Roof(float x,float y,float z,PieceDimensions shape,MaterialId material)
            {
                PieceId id=NextId();
                var roof=new PieceData(id,At(x,y,z),shape,material);
                roof=roof.WithFinish(new ArchitecturalFinish(new Guid(Seed+4,(short)region,
                    (short)(part-1),0x45,0x41,0x56,0x45,0,0,0,1),id,Guid.Empty,
                    ArchitecturalFinishKind.RoofEave,PvtFinalScenario.Wood));
                pieces.Add(roof);return roof;
            }
        }

        private readonly struct OpeningSpec
        {
            public readonly WallOpeningKind Kind;
            public readonly float Left,Bottom,Width,Height;
            public OpeningSpec(WallOpeningKind kind,float left,float bottom,float width,float height)
            { Kind=kind;Left=left;Bottom=bottom;Width=width;Height=height; }
        }
        private static OpeningSpec Passage(float left,float width,float height) =>
            new OpeningSpec(WallOpeningKind.Passage,left,0f,width,height);
        private static OpeningSpec Window(float left,float bottom,float width,float height) =>
            new OpeningSpec(WallOpeningKind.Window,left,bottom,width,height);

        private static void AddCastle(List<PieceData> pieces)
        {
            var b=new Author(pieces,10,0f,18f);
            MaterialId stone=LabMaterialIds.Stone,wood=PvtFinalScenario.Wood;
            b.Slab(0f,0f,0f,31f,0.85f,29f,stone);
            const float baseY=0.85f;
            b.Wall(0f,baseY,-12f,27f,7f,1f,stone,0f,
                Window(2f,4.2f,0.45f,1f),Passage(11.9f,3.2f,4.4f),
                Window(24.4f,4.2f,0.45f,1f));
            b.Wall(0f,baseY,12f,27f,7f,1f,stone,0f,
                Window(5f,4f,0.45f,1f),Window(21.5f,4f,0.45f,1f));
            b.Wall(-13f,baseY,0f,24f,7f,1f,stone,90f,
                Window(5f,4f,0.45f,1f),Window(19f,4f,0.45f,1f));
            b.Wall(13f,baseY,0f,24f,7f,1f,stone,90f,
                Window(5f,4f,0.45f,1f),Window(19f,4f,0.45f,1f));
            // Passable stone gate arch and timber doors remain individual logical pieces.
            b.Add(b.At(0f,baseY,-12.8f),new PieceDimensions(
                new ArchDimensions(4.4f,4.9f,1f,0.48f,1.25f,0.42f)),stone);
            b.Block(-0.75f,baseY,-12.92f,1.35f,4.1f,0.13f,wood);
            b.Block(0.75f,baseY,-12.92f,1.35f,4.1f,0.13f,wood);
            for (int side=-1;side<=1;side+=2)
            {
                for (int i=-5;i<=5;i++)
                {
                    b.Block(i*2.35f,baseY+7f,side*12f,0.9f,0.75f,1.1f,stone);
                    b.Block(side*13f,baseY+7f,i*2.05f,1.1f,0.75f,0.9f,stone);
                }
                for (int front=-1;front<=1;front+=2)
                    AddTower(b,side*13f,front*12f);
            }
            // Inner keep with open court and shallow eastern Mediterranean dome.
            b.Slab(0f,baseY,1.5f,11.5f,0.48f,9.5f,stone);
            float keepY=baseY+0.48f;
            b.Wall(0f,keepY,-3f,10.6f,5.1f,0.55f,stone,0f,
                Window(1.6f,2.2f,0.8f,1.2f),Passage(4.55f,1.5f,2.8f),
                Window(8.3f,2.2f,0.8f,1.2f));
            b.Wall(0f,keepY,6f,10.6f,5.1f,0.55f,stone);
            b.Wall(-5.3f,keepY,1.5f,8.5f,5.1f,0.55f,stone,90f,
                Window(3.7f,2.3f,0.8f,1.2f));
            b.Wall(5.3f,keepY,1.5f,8.5f,5.1f,0.55f,stone,90f,
                Window(3.7f,2.3f,0.8f,1.2f));
            b.Roof(0f,keepY+5.1f,1.5f,new PieceDimensions(new FlatRoofDimensions(11.3f,9.4f,0.45f)),stone);
            b.Add(b.At(0f,keepY+5.55f,1.5f),new PieceDimensions(
                new DomeDimensions(4.2f,2f,0.32f)),stone);
            b.Add(b.At(0f,baseY,-7.5f),new PieceDimensions(
                new StairDimensions(2.1f,0.9f,4f,6)),stone);
        }

        private static void AddTower(Author b,float x,float z)
        {
            const float y=0.85f,w=4.6f,h=9.5f,t=0.72f;
            b.Wall(x,y,z-w*0.5f,w,h,t,LabMaterialIds.Stone,0f,
                Window(2.05f,4.4f,0.45f,1.1f));
            b.Wall(x,y,z+w*0.5f,w,h,t,LabMaterialIds.Stone);
            b.Wall(x-w*0.5f,y,z,w,h,t,LabMaterialIds.Stone,90f);
            b.Wall(x+w*0.5f,y,z,w,h,t,LabMaterialIds.Stone,90f);
            b.Slab(x,y+h,z,w+0.15f,0.35f,w+0.15f,LabMaterialIds.Stone);
            for (int side=-1;side<=1;side+=2)
                for (int end=-1;end<=1;end+=2)
                    b.Block(x+side*1.7f,y+h+0.35f,z+end*1.7f,
                        0.8f,0.8f,0.8f,LabMaterialIds.Stone);
        }

        private static void AddTradingHall(List<PieceData> pieces)
        {
            var b=new Author(pieces,20,0f,-25f);
            MaterialId stone=LabMaterialIds.Stone,plaster=LabMaterialIds.Plaster,
                timber=PvtFinalScenario.Wood;
            b.Slab(0f,0f,0f,16.5f,0.5f,9f,stone);
            const float y=0.5f;
            b.Wall(0f,y,4f,15.5f,4.5f,0.4f,plaster,0f,
                Passage(1.5f,2.7f,3.2f),Passage(6.4f,2.7f,3.2f),
                Passage(11.3f,2.7f,3.2f));
            b.Wall(0f,y,-4f,15.5f,4.5f,0.4f,plaster,0f,
                Window(2f,1.4f,1.3f,1.3f),Window(12f,1.4f,1.3f,1.3f));
            b.Wall(-7.75f,y,0f,8f,4.5f,0.4f,plaster,90f);
            b.Wall(7.75f,y,0f,8f,4.5f,0.4f,plaster,90f);
            b.Roof(0f,y+4.5f,0f,new PieceDimensions(
                new GableRoofDimensions(17f,9.3f,0.32f,1.7f)),PvtFinalScenario.Ceramic);
            for (int i=-1;i<=1;i++)
            {
                float x=i*4.9f;
                b.Add(b.At(x,y,4.5f),new PieceDimensions(
                    new ArchDimensions(3.8f,3.6f,0.6f,0.45f,1f,0.35f)),stone);
                b.Block(x,y,6f,2.9f,0.9f,0.9f,timber);
                b.Roof(x,y+2.9f,6f,new PieceDimensions(
                    new ShedRoofDimensions(3.1f,1.6f,0.18f,0.5f)),timber);
            }
            b.Add(b.At(-6f,y,1f),new PieceDimensions(
                new VaultDimensions(3f,2.2f,5f,0.28f)),stone);
        }
    }
}
