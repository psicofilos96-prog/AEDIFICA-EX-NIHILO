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
            public int Style;
            public float X, Z, Yaw;
            public Plot(int style,float x,float z,float yaw)
            { Style=style;X=x;Z=z;Yaw=yaw; }
        }
        private static readonly Plot[] Plots = {
            new Plot(0,-237f,-82f,-90f),new Plot(0,-239f,-61f,-93f),
            new Plot(0,-236f,-40f,-87f),new Plot(0,-203f,-80f,90f),
            new Plot(0,-201f,-59f,88f),new Plot(0,-204f,-38f,94f),
            new Plot(1,-239f,-15f,-92f),new Plot(1,-237f,8f,-87f),
            new Plot(1,-202f,-16f,92f),new Plot(1,-204f,7f,87f),
            new Plot(2,-238f,34f,-89f),new Plot(2,-202f,35f,91f)
        };

        public static List<PieceData> Build()
        {
            var pieces=new List<PieceData>();
            for (int building=0;building<Plots.Length;building++)
            {
                AddHouse(pieces,building,Plots[building]);
            }
            AddMonument(pieces);
            return pieces;
        }

        // A single editable residence for the free-asset art gate. Its geometry
        // and stable IDs use the same authored recipe as the city review.
        public static List<PieceData> BuildReferenceResidence()
        {
            var pieces=new List<PieceData>();
            AddHouse(pieces,1,new Plot(0,0f,0f,0f),true);
            return pieces;
        }

        // Authored wall joints, openings and eaves replace the benchmark's repeated
        // detail recipe. The styles live only in this review scenario, not the engine.
        private static void AddHouse(List<PieceData> pieces,int building,Plot plot,bool linkedFinishes=false)
        {
            float width=plot.Style==0 ? 7.6f+(building%3)*0.4f :
                plot.Style==1 ? 8.4f : 9.2f;
            float depth=plot.Style==0 ? 6.8f : plot.Style==1 ? 7.4f : 8f;
            float height=plot.Style==2 ? 3.35f : 3.2f;
            int levels=plot.Style==0 ? 1 : 2;
            const float wallThickness=0.36f;
            Quaternion yaw=Quaternion.Euler(0f,plot.Yaw,0f);
            Vector3 anchor=new Vector3(plot.X,Pvt2Scenario.GroundHeight(plot.X,plot.Z),plot.Z);
            MaterialId masonry=building%3==0 ? LabMaterialIds.Stone :
                building%3==1 ? LabMaterialIds.Plaster : LabMaterialIds.Brick;
            int part=0,wallIndex=0;
            PieceTransform At(float x,float y,float z,float rotation=0f) =>
                new PieceTransform(anchor+yaw*new Vector3(x,y,z),yaw*Quaternion.Euler(0f,rotation,0f));
            void Add(PieceTransform transform,PieceDimensions dimensions,MaterialId material)
            { pieces.Add(new PieceData(DetailId(building,20000+part++),transform,dimensions,material)); }
            Add(At(0f,0f,0f),new PieceDimensions(new SlabDimensions(width+0.6f,0.38f,depth+0.6f)),
                LabMaterialIds.Stone);
            for (int floor=0;floor<levels;floor++)
            {
                float baseY=0.38f+floor*(height+0.25f);
                if (floor>0)
                    Add(At(0f,baseY-0.25f,0f),new PieceDimensions(new SlabDimensions(
                        width+0.3f,0.25f,depth+0.3f)),PvtFinalScenario.Wood);
                AddWall(0f,-depth*0.5f,width,0f,true);
                AddWall(0f,depth*0.5f,width,0f,false);
                AddWall(-width*0.5f,0f,depth-wallThickness,90f,false);
                AddWall(width*0.5f,0f,depth-wallThickness,90f,false);
                // Continuous floor stringcourse and corner pilasters bridge wall joints.
                Add(At(0f,baseY+height-0.2f,-depth*0.5f-0.12f),
                    new PieceDimensions(new BeamDimensions(width+0.48f,0.24f,0.22f)),
                    LabMaterialIds.Stone);
                for (int side=-1;side<=1;side+=2)
                    Add(At(side*width*0.5f,baseY,-depth*0.5f),
                        new PieceDimensions(new ColumnDimensions(0.3f,height,0.3f)),
                        LabMaterialIds.Stone);

                void AddWall(float x,float z,float length,float rotation,bool front)
                {
                    int thisWall=wallIndex++;
                    var wall=new PieceData(DetailId(building,20000+part++),
                        At(x,baseY,z,rotation),new WallDimensions(length,height,wallThickness))
                        .WithMaterial(masonry);
                    int openingIndex=0;
                    void Opening(WallOpeningKind kind,float left,float bottom,float openingWidth,float openingHeight)
                    {
                        Guid id=new Guid(Seed,(short)building,(short)thisWall,
                            0x41,0x52,0x54,0x48,0x4f,0x55,0x53,(byte)openingIndex++);
                        wall=wall.WithOpening(new WallOpening(id,wall.Id,kind,left,bottom,
                            openingWidth,openingHeight));
                    }
                    if (front && floor==0)
                    {
                        Opening(WallOpeningKind.Window,0.75f,1.25f,1.2f,1.25f);
                        Opening(WallOpeningKind.Passage,(length-1.4f)*0.5f,0f,1.4f,2.5f);
                        Opening(WallOpeningKind.Window,length-1.95f,1.25f,1.2f,1.25f);
                    }
                    else if (rotation==0f)
                    {
                        Opening(WallOpeningKind.Window,0.95f,1.15f,1.25f,1.35f);
                        Opening(WallOpeningKind.Window,length-2.2f,1.15f,1.25f,1.35f);
                    }
                    else Opening(WallOpeningKind.Window,(length-1.25f)*0.5f,
                        1.2f,1.25f,1.35f);
                    if (linkedFinishes)
                    {
                        int finishIndex=0;
                        foreach (WallOpening opening in wall.Openings)
                        {
                            ArchitecturalFinishKind frame=opening.Kind==WallOpeningKind.Passage
                                ? ArchitecturalFinishKind.DoorFrame
                                : ArchitecturalFinishKind.WindowFrameSill;
                            wall=wall.WithFinish(new ArchitecturalFinish(FinishId(building,thisWall,finishIndex++),
                                wall.Id,opening.Id,frame,LabMaterialIds.Stone));
                            wall=wall.WithFinish(new ArchitecturalFinish(FinishId(building,thisWall,finishIndex++),
                                wall.Id,opening.Id,ArchitecturalFinishKind.Lintel,LabMaterialIds.Stone));
                        }
                    }
                    pieces.Add(wall);
                    if (!linkedFinishes) AddOpeningFrames(pieces,wall,building,thisWall);
                    if (front && floor==0)
                    {
                        // A paneled timber leaf fills the passage, so the street
                        // facade does not expose an empty black opening.
                        float doorZ=-depth*0.5f-0.025f;
                        Add(At(0f,baseY+0.06f,doorZ),new PieceDimensions(
                            new BlockDimensions(1.3f,2.37f,0.12f)),PvtFinalScenario.Wood);
                        for (int stile=-1;stile<=1;stile+=2)
                            Add(At(stile*0.47f,baseY+0.14f,doorZ-0.075f),
                                new PieceDimensions(new BeamDimensions(0.14f,2.15f,0.07f)),
                                PvtFinalScenario.Wood);
                        Add(At(0f,baseY+1.1f,doorZ-0.08f),new PieceDimensions(
                            new BeamDimensions(1.05f,0.13f,0.07f)),PvtFinalScenario.Wood);
                    }
                }
            }
            float roofBase=0.38f+levels*height+(levels-1)*0.25f-0.06f;
            float eave=plot.Style==2 ? 0.68f : 0.54f;
            PieceDimensions roof=building%3==0 ? new PieceDimensions(
                new GableRoofDimensions(width+2f*eave,depth+2f*eave,0.32f,1.65f)) :
                building%3==1 ? new PieceDimensions(new HipRoofDimensions(
                    width+2f*eave,depth+2f*eave,0.32f,1.5f)) :
                    new PieceDimensions(new ShedRoofDimensions(
                        width+2f*eave,depth+2f*eave,0.32f,1.25f));
            if (linkedFinishes)
            {
                PieceId roofId=DetailId(building,20000+part++);
                pieces.Add(new PieceData(roofId,At(0f,roofBase,0f),roof,PvtFinalScenario.Ceramic)
                    .WithFinish(new ArchitecturalFinish(FinishId(building,100,0),roofId,Guid.Empty,
                        ArchitecturalFinishKind.RoofEave,PvtFinalScenario.Wood)));
            }
            else Add(At(0f,roofBase,0f),roof,PvtFinalScenario.Ceramic);
            if (building%3==0)
            {
                // Masonry courses close the gable ends beneath the pitched roof.
                // Each course remains an editable logical block.
                const int courses=6;
                float courseHeight=1.65f/courses;
                for (int course=0;course<courses;course++)
                    for (int end=-1;end<=1;end+=2)
                        Add(At(end*width*0.5f,roofBase+course*courseHeight-0.06f,
                            0f,90f),new PieceDimensions(new BlockDimensions(
                            depth*(1f-(course+0.25f)/courses),courseHeight+0.07f,
                            wallThickness)),masonry);
            }
            if (!linkedFinishes) for (int side=-1;side<=1;side+=2)
            {
                Add(At(0f,roofBase-0.28f,side*(depth*0.5f+eave-0.09f)),
                    new PieceDimensions(new BeamDimensions(width+2f*eave,0.3f,0.18f)),
                    PvtFinalScenario.Wood);
                Add(At(side*(width*0.5f+eave-0.09f),roofBase-0.28f,0f,90f),
                    new PieceDimensions(new BeamDimensions(depth+2f*eave,0.3f,0.18f)),
                    PvtFinalScenario.Wood);
            }
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
            PieceTransform At(float px,float py,float pz,float yaw=0f) =>
                new PieceTransform(new Vector3(x+px,ground+py,z+pz),Quaternion.Euler(0f,yaw,0f));
            void Add(PieceTransform transform,PieceDimensions d,MaterialId mat)
            { pieces.Add(new PieceData(DetailId(2000,part++),transform,d,mat)); }
            Add(At(0f,0f,0f),new PieceDimensions(new SlabDimensions(25f,0.45f,20f)),LabMaterialIds.Stone);
            Add(At(0f,0.45f,8.5f),new PieceDimensions(new WallDimensions(24f,6.5f,0.65f)),LabMaterialIds.Stone);
            Add(At(-12f,0.45f,0f,90f),new PieceDimensions(new WallDimensions(17f,6.5f,0.65f)),LabMaterialIds.Stone);
            Add(At(12f,0.45f,0f,90f),new PieceDimensions(new WallDimensions(17f,6.5f,0.65f)),LabMaterialIds.Stone);
            var front=new PieceData(DetailId(2000,part++),At(0f,0.45f,-8.5f),
                new WallDimensions(24f,6.5f,0.65f)).WithMaterial(LabMaterialIds.Stone);
            front=front.WithOpening(new WallOpening(new Guid(Seed,2000,(short)part,
                0x56,0x49,0x53,0x55,0x41,0x4c,0,1),front.Id,
                WallOpeningKind.Passage,10.5f,0f,3f,4.2f));
            pieces.Add(front);
            AddOpeningFrames(pieces,front,2000,part);
            Add(At(0f,6.95f,0f),new PieceDimensions(new FlatRoofDimensions(25f,20f,0.5f)),LabMaterialIds.Stone);
            // Four editable cornice pieces visually close the flat roof edge.
            for (int side=-1;side<=1;side+=2)
            {
                Add(At(0f,6.68f,side*9.8f),new PieceDimensions(
                    new BeamDimensions(25.4f,0.38f,0.48f)),LabMaterialIds.Stone);
                Add(At(side*12.5f,6.68f,0f,90f),new PieceDimensions(
                    new BeamDimensions(19.5f,0.38f,0.48f)),LabMaterialIds.Stone);
            }
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
            for (int i=0;i<2;i++)
            {
                float px=i==0 ? -11f : 11f;
                Add(At(px,0.45f,-10.4f),new PieceDimensions(
                    new SlabDimensions(1.8f,0.28f,3.4f)),LabMaterialIds.Stone);
            }
        }

        private static Guid FinishId(int building,int host,int part) =>
            new Guid(Seed+2,(short)building,(short)host,
                0x46,0x49,0x4e,0x49,0x53,0x48,0,(byte)part);

        private static PieceId DetailId(int building,int part) => PieceId.Parse(
            new Guid(Seed+1,(short)building,(short)part,
                0x56,0x49,0x53,0x55,0x41,0x4c,0,1).ToString("N"));
    }
}
