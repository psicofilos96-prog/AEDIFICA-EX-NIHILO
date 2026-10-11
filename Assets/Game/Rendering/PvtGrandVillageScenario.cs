using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // A larger authored settlement. Architecture remains individual PieceData.
    public static class PvtGrandVillageScenario
    {
        public const int Seed=74319;
        public const int HouseCount=18;
        public static List<PieceData> Build()
        {
            var pieces=new List<PieceData>();
            Castle(new Author(pieces,10,-22f,38f));
            Barracks(new Author(pieces,20,-60f,-6f));
            Market(new Author(pieces,30,-5f,-7f));
            Bridge(new Author(pieces,40,PvtGrandVillageEnvironment.RiverCenter(-18f),-18f));
            float[] xs={-72f,-57f,-42f,-27f,-12f,3f};
            float[] zs={-58f,-40f,-22f};
            for (int row=0;row<zs.Length;row++)
                for (int col=0;col<xs.Length;col++)
                {
                    float x=xs[col],z=zs[row];
                    int lot=4000+row*xs.Length+col;
                    List<PieceData> house=PvtVisualCityScenario.BuildResidenceAt(lot,x,z,
                        col%2==0 ? 90f : -90f,(row+col)%3);
                    float lift=PvtGrandVillageEnvironment.Height(x,z)-Pvt2Scenario.GroundHeight(x,z);
                    foreach (PieceData item in house)
                        pieces.Add(item.WithTransform(new PieceTransform(
                            item.Transform.Position+Vector3.up*lift,item.Transform.Rotation)));
                }
            return pieces;
        }

        private sealed class Author
        {
            private readonly List<PieceData> output;
            private readonly int region;
            private readonly Vector3 origin;
            private int part;
            public Author(List<PieceData> pieces,int region,float x,float z)
            { output=pieces;this.region=region;origin=new Vector3(x,PvtGrandVillageEnvironment.Height(x,z),z); }
            private Guid Id(int kind,int index)
            {
                byte[] bytes=new byte[16];
                Array.Copy(BitConverter.GetBytes(Seed+kind),0,bytes,0,4);
                Array.Copy(BitConverter.GetBytes(region),0,bytes,4,4);
                Array.Copy(BitConverter.GetBytes(index),0,bytes,8,4);
                bytes[12]=0x47;bytes[13]=0x52;bytes[14]=0x41;bytes[15]=0x4e;
                return new Guid(bytes);
            }
            private PieceId Next() => PieceId.Parse(Id(0,part++).ToString("N"));
            public PieceTransform At(float x,float y,float z,float yaw=0f) =>
                new PieceTransform(origin+new Vector3(x,y,z),Quaternion.Euler(0f,yaw,0f));
            public PieceData Add(float x,float y,float z,PieceDimensions d,MaterialId material,float yaw=0f)
            { var p=new PieceData(Next(),At(x,y,z,yaw),d,material);output.Add(p);return p; }
            public void Block(float x,float y,float z,float w,float h,float d,MaterialId mat) =>
                Add(x,y,z,new PieceDimensions(new BlockDimensions(w,h,d)),mat);
            public void Slab(float x,float y,float z,float w,float h,float d,MaterialId mat) =>
                Add(x,y,z,new PieceDimensions(new SlabDimensions(w,h,d)),mat);
            public void Wall(float x,float y,float z,float length,float height,float thick,
                MaterialId mat,float yaw=0f) =>
                Add(x,y,z,new PieceDimensions(new WallDimensions(length,height,thick)),mat,yaw);
            public void GateWall(float x,float y,float z,float length,float height,float thick,
                float gateWidth,MaterialId mat)
            {
                PieceId id=Next();int index=part-1;
                Guid opening=Id(1,index);
                PieceData wall=new PieceData(id,At(x,y,z),new WallDimensions(length,height,thick))
                    .WithMaterial(mat).WithOpening(new WallOpening(opening,id,
                        WallOpeningKind.Passage,(length-gateWidth)*0.5f,0f,gateWidth,4.8f));
                wall=wall.WithFinish(new ArchitecturalFinish(Id(2,index),id,opening,
                    ArchitecturalFinishKind.DoorFrame,LabMaterialIds.Stone));
                output.Add(wall);
            }
            public void Roof(float x,float y,float z,PieceDimensions d,MaterialId mat) => Add(x,y,z,d,mat);
        }

        private static void Castle(Author b)
        {
            MaterialId stone=LabMaterialIds.Stone,timber=PvtFinalScenario.Wood;
            b.Slab(0f,0f,0f,59f,0.8f,53f,stone);
            const float y=0.8f;
            b.GateWall(0f,y,-24f,54f,9f,1.3f,4.8f,stone);
            b.Wall(0f,y,24f,54f,9f,1.3f,stone);
            b.Wall(-27f,y,0f,48f,9f,1.3f,stone,90f);
            b.Wall(27f,y,0f,48f,9f,1.3f,stone,90f);
            b.Add(0f,y,-25f,new PieceDimensions(new ArchDimensions(6f,5.3f,1.5f,0.7f,1.2f,0.5f)),stone);
            for (int side=-1;side<=1;side+=2)
            {
                b.Block(side*1.05f,y,-25.1f,1.9f,4.6f,0.15f,timber);
                for (int n=-10;n<=10;n++)
                    b.Block(n*2.55f,y+9f,side*24f,1.15f,0.95f,1.35f,stone);
                for (int n=-9;n<=9;n++)
                    b.Block(side*27f,y+9f,n*2.5f,1.35f,0.95f,1.15f,stone);
                for (int end=-1;end<=1;end+=2) Tower(b,side*27f,end*24f);
            }
            // Keep and inner hall, with independently editable floors, walls, roofs and dome.
            b.Slab(0f,y,5f,22f,0.55f,17f,stone);
            float k=y+0.55f;
            for (int level=0;level<3;level++)
            {
                float floor=k+level*4.1f;
                b.Wall(0f,floor,-3f,21f,4.1f,0.72f,stone);
                b.Wall(0f,floor,13f,21f,4.1f,0.72f,stone);
                b.Wall(-10.5f,floor,5f,16f,4.1f,0.72f,stone,90f);
                b.Wall(10.5f,floor,5f,16f,4.1f,0.72f,stone,90f);
                b.Slab(0f,floor+4.1f,5f,22f,0.36f,17f,stone);
                for (int side=-1;side<=1;side+=2)
                    b.Block(side*7.3f,floor+1.3f,-3.6f,1.5f,1.9f,0.16f,timber);
            }
            b.Roof(0f,k+12.66f,5f,new PieceDimensions(new FlatRoofDimensions(22.5f,17.5f,0.45f)),stone);
            b.Add(0f,k+13.1f,5f,new PieceDimensions(new DomeDimensions(7f,2.8f,0.4f)),stone);
            b.Add(0f,y,-15f,new PieceDimensions(new StairDimensions(3.5f,1.3f,7f,8)),stone);
            for (int side=-1;side<=1;side+=2)
            {
                b.Slab(side*18f,y,-2f,7f,0.4f,16f,stone);
                b.Wall(side*18f,y+0.4f,5.5f,7f,4.5f,0.48f,stone);
                b.Roof(side*18f,y+4.9f,-2f,
                    new PieceDimensions(new GableRoofDimensions(8f,17f,0.35f,2f)),PvtFinalScenario.Ceramic);
            }
        }

        private static void Tower(Author b,float x,float z)
        {
            const float width=7f, y=0.8f, h=13f;
            MaterialId stone=LabMaterialIds.Stone;
            b.Slab(x,y,z,7.5f,0.45f,7.5f,stone);
            b.Wall(x,y+0.45f,z-3.5f,width,h,0.9f,stone);
            b.Wall(x,y+0.45f,z+3.5f,width,h,0.9f,stone);
            b.Wall(x-3.5f,y+0.45f,z,width,h,0.9f,stone,90f);
            b.Wall(x+3.5f,y+0.45f,z,width,h,0.9f,stone,90f);
            b.Slab(x,y+h+0.45f,z,8f,0.5f,8f,stone);
            for (int side=-1;side<=1;side+=2)
                for (int end=-1;end<=1;end+=2)
                    b.Block(x+side*2.8f,y+h+0.95f,z+end*2.8f,1.15f,1.1f,1.15f,stone);
        }

        private static void Barracks(Author b)
        {
            MaterialId stone=LabMaterialIds.Stone,plaster=LabMaterialIds.Plaster,
                wood=PvtFinalScenario.Wood;
            b.Slab(0f,0f,0f,24f,0.5f,12f,stone);
            b.GateWall(0f,0.5f,-5.4f,23f,5f,0.5f,2.5f,plaster);
            b.Wall(0f,0.5f,5.4f,23f,5f,0.5f,plaster);
            b.Wall(-11.5f,0.5f,0f,10.8f,5f,0.5f,plaster,90f);
            b.Wall(11.5f,0.5f,0f,10.8f,5f,0.5f,plaster,90f);
            b.Roof(0f,5.5f,0f,new PieceDimensions(new GableRoofDimensions(24.5f,12.5f,0.36f,2.7f)),
                PvtFinalScenario.Ceramic);
            for (int side=-1;side<=1;side+=2)
            {
                b.Block(side*1.2f,0.5f,-5.6f,1.1f,3.1f,0.12f,wood);
                for (int i=0;i<4;i++)
                    b.Block(side*(3.5f+i*2.1f),0.55f,2.8f,1.6f,0.55f,4.4f,wood);
            }
            b.Add(-8f,0.5f,-9f,new PieceDimensions(new StairDimensions(2.5f,0.8f,3.2f,5)),stone);
        }

        private static void Market(Author b)
        {
            MaterialId stone=LabMaterialIds.Stone,wood=PvtFinalScenario.Wood;
            b.Slab(0f,0f,0f,18f,0.45f,10f,stone);
            for (int n=-2;n<=2;n++)
            {
                float x=n*3.5f;
                b.Add(x,0.45f,-4.5f,new PieceDimensions(
                    new ArchDimensions(3.25f,3.7f,0.5f,0.4f,1f,0.32f)),stone);
                b.Block(x,0.45f,-6.3f,2.7f,0.9f,1f,wood);
            }
            b.Wall(0f,0.45f,4.3f,17.5f,4.1f,0.42f,LabMaterialIds.Plaster);
            b.Roof(0f,4.55f,0f,new PieceDimensions(new ShedRoofDimensions(18.4f,10.4f,0.32f,1.8f)),
                PvtFinalScenario.Ceramic);
        }

        private static void Bridge(Author b)
        {
            MaterialId stone=LabMaterialIds.Stone;
            for (int i=-1;i<=1;i++)
                b.Add(i*4.1f,0f,0f,new PieceDimensions(
                    new ArchDimensions(4.1f,2.8f,5.3f,0.52f,1f,0.35f)),stone);
            b.Slab(0f,2.8f,0f,15.3f,0.55f,5.4f,stone);
            for (int side=-1;side<=1;side+=2)
            {
                b.Wall(0f,3.35f,side*2.5f,15.3f,0.9f,0.3f,stone);
                for (int end=-1;end<=1;end+=2)
                    b.Block(end*6.7f,0f,side*2f,1.4f,2.8f,1.4f,stone);
            }
        }
    }
}
