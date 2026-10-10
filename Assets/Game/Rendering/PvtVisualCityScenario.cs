using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // A deliberately small review city: individual PieceData remain the only
    // construction authority. Decorative FBX models are attached at runtime.
    public static class PvtVisualCityScenario
    {
        public const int Seed = 7319;
        public const int BuildingCount = 13;
        public static readonly MaterialId Road = MaterialId.Parse("20000000000040008000000000000003");
        public static readonly MaterialId Glass = MaterialId.Parse("20000000000040008000000000000004");
        private struct Plot
        {
            public int Source, Parts;
            public float X, Z, Yaw;
            public Plot(int source,int parts,float x,float z,float yaw)
            { Source=source;Parts=parts;X=x;Z=z;Yaw=yaw; }
        }
        private static readonly Plot[] Plots = {
            new Plot(0,10,-238f,-76f,-88f),new Plot(1,10,-240f,-55f,-94f),
            new Plot(2,10,-236f,-35f,-86f),new Plot(3,10,-203f,-73f,92f),
            new Plot(4,10,-201f,-53f,87f),new Plot(5,10,-204f,-33f,95f),
            new Plot(300,30,-239f,0f,-92f),new Plot(301,30,-237f,24f,-87f),
            new Plot(302,30,-202f,1f,93f),new Plot(303,30,-204f,25f,87f),
            new Plot(400,80,-238f,52f,-89f),new Plot(401,80,-202f,53f,91f)
        };

        public static List<PieceData> Build()
        {
            var pieces=new List<PieceData>();
            for (int building=0;building<Plots.Length;building++)
            {
                Plot plot=Plots[building];
                int first=plot.Source<300 ? plot.Source*10 :
                    plot.Source<400 ? 3000+(plot.Source-300)*30 :
                    6000+(plot.Source-400)*80;
                PieceData floor=PvtFinalScenario.PieceAt(first,10000,Seed);
                Quaternion delta=Quaternion.Euler(0f,plot.Yaw,0f)*
                    Quaternion.Inverse(floor.Transform.Rotation);
                Vector3 anchor=new Vector3(plot.X,Pvt2Scenario.GroundHeight(plot.X,plot.Z),plot.Z);
                for (int part=0;part<plot.Parts;part++)
                {
                    PieceData source=PvtFinalScenario.PieceAt(first+part,10000,Seed);
                    Vector3 offset=source.Transform.Position-floor.Transform.Position;
                    PieceTransform transform=new PieceTransform(anchor+delta*offset,
                        delta*source.Transform.Rotation);
                    PieceData piece=source.WithTransform(transform);
                    pieces.Add(piece);
                    if (piece.Type==PieceType.Wall)
                        AddOpeningFrames(pieces,piece,building,part);
                }
            }
            AddMonument(pieces);
            return pieces;
        }

        private static void AddOpeningFrames(List<PieceData> pieces,PieceData wall,int building,int part)
        {
            int segment=0;
            foreach (WallOpening opening in wall.Openings)
            {
                const float trim=0.14f;
                float cx=opening.Left+opening.Width*0.5f-wall.WallDimensions.Length*0.5f;
                float front=wall.WallDimensions.Thickness*0.5f+0.045f;
                MaterialId trimMaterial=building%3==0 ? LabMaterialIds.Stone : PvtFinalScenario.Wood;
                AddTrim(-opening.Width*0.5f-trim,opening.Bottom-trim,trim,
                    opening.Height+2f*trim,trimMaterial);
                AddTrim(opening.Width*0.5f,opening.Bottom-trim,trim,
                    opening.Height+2f*trim,trimMaterial);
                AddTrim(-opening.Width*0.5f,opening.Bottom-trim,opening.Width,trim,trimMaterial);
                AddTrim(-opening.Width*0.5f,opening.Top,opening.Width,trim,trimMaterial);
                if (opening.Kind==WallOpeningKind.Window)
                {
                    AddTrim(-trim*0.5f,opening.Bottom,trim,opening.Height,
                        PvtFinalScenario.Wood);
                    AddTrim(-opening.Width*0.5f,opening.Bottom+opening.Height*0.52f,
                        opening.Width,trim,PvtFinalScenario.Wood);
                    PieceId glassId=DetailId(building,part*64+segment++);
                    Vector3 glassLocal=new Vector3(cx,opening.Bottom+trim,0f);
                    pieces.Add(new PieceData(glassId,new PieceTransform(
                        wall.Transform.Position+wall.Transform.Rotation*glassLocal,
                        wall.Transform.Rotation),
                        new BlockDimensions(opening.Width-2f*trim,
                            opening.Height-2f*trim,0.05f)).WithMaterial(Glass));
                }
                void AddTrim(float left,float bottom,float width,float height,MaterialId material)
                {
                    PieceId id=DetailId(building,part*64+segment++);
                    Vector3 local=new Vector3(cx+left+width*0.5f,bottom,front);
                    var transform=new PieceTransform(wall.Transform.Position+
                        wall.Transform.Rotation*local,wall.Transform.Rotation);
                    pieces.Add(new PieceData(id,transform,new BeamDimensions(width,height,0.12f))
                        .WithMaterial(material));
                }
            }
        }

        private static void AddMonument(List<PieceData> pieces)
        {
            const float x=-220f,z=78f;
            float ground=Pvt2Scenario.GroundHeight(x,z);
            int part=0;
            PieceTransform At(float px,float py,float pz) =>
                new PieceTransform(new Vector3(x+px,ground+py,z+pz),Quaternion.identity);
            void Add(PieceTransform transform,PieceDimensions d,MaterialId mat)
            { pieces.Add(new PieceData(DetailId(2000,part++),transform,d,mat)); }
            Add(At(0f,0f,0f),new PieceDimensions(new SlabDimensions(25f,0.45f,20f)),LabMaterialIds.Stone);
            Add(At(0f,0.45f,8.5f),new PieceDimensions(new WallDimensions(24f,6.5f,0.65f)),LabMaterialIds.Stone);
            Add(At(-12f,0.45f,0f),new PieceDimensions(new WallDimensions(17f,6.5f,0.65f)),LabMaterialIds.Stone);
            Add(At(12f,0.45f,0f),new PieceDimensions(new WallDimensions(17f,6.5f,0.65f)),LabMaterialIds.Stone);
            var front=new PieceData(DetailId(2000,part++),At(0f,0.45f,-8.5f),
                new WallDimensions(24f,6.5f,0.65f)).WithMaterial(LabMaterialIds.Stone);
            front=front.WithOpening(new WallOpening(new Guid(Seed,2000,(short)part,
                0x56,0x49,0x53,0x55,0x41,0x4c,0,1),front.Id,
                WallOpeningKind.Passage,10.5f,0f,3f,4.2f));
            pieces.Add(front);
            AddOpeningFrames(pieces,front,2000,part);
            Add(At(0f,6.95f,0f),new PieceDimensions(new FlatRoofDimensions(25f,20f,0.5f)),LabMaterialIds.Stone);
            Add(At(0f,7.45f,0f),new PieceDimensions(new DomeDimensions(13f,6f,0.45f)),PvtFinalScenario.Ceramic);
            Add(At(-8f,7.45f,0f),new PieceDimensions(new VaultDimensions(4f,2.6f,11f,0.35f)),LabMaterialIds.Stone);
            Add(At(8f,7.45f,0f),new PieceDimensions(new VaultDimensions(4f,2.6f,11f,0.35f)),LabMaterialIds.Stone);
            Add(At(0f,0.45f,-12f),new PieceDimensions(new StairDimensions(5f,1.5f,4f,6)),LabMaterialIds.Stone);
            for (int i=0;i<8;i++)
            {
                float px=-10.5f+i*3f;
                Add(At(px,0.45f,-10.2f),new PieceDimensions(new ColumnDimensions(0.65f,6.4f,0.65f)),
                    LabMaterialIds.Stone);
                Add(At(px,6.85f,-10.2f),new PieceDimensions(new ParapetDimensions(2.7f,0.9f,0.6f)),
                    LabMaterialIds.Stone);
            }
            for (int i=0;i<3;i++)
                Add(At(-6f+i*6f,0.45f,-10.6f),
                    new PieceDimensions(new ArchDimensions(3.4f,4.3f,0.7f,0.55f,1.5f,0.3f)),
                    LabMaterialIds.Stone);
        }

        private static PieceId DetailId(int building,int part) => PieceId.Parse(
            new Guid(Seed+1,(short)building,(short)part,
                0x56,0x49,0x53,0x55,0x41,0x4c,0,1).ToString("N"));
    }
}
