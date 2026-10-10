using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Composition is explicit: 10/30/80 editable pieces per simple/medium/complex building.
    // D adds monumental buildings to C in 10,000-piece increments, stopping at 80,000.
    public static class PvtFinalScenario
    {
        public const int Seed = 7319;
        public const float PlotSpacing = 18f;
        public static readonly int[] Sizes = { 10000, 25000, 50000, 60000, 70000, 80000 };
        public static readonly MaterialId Wood = MaterialId.Parse("20000000000040008000000000000001");
        public static readonly MaterialId Ceramic = MaterialId.Parse("20000000000040008000000000000002");

        public static void Composition(int target, out int simple, out int medium, out int complex)
        {
            if (target == 10000) { simple=300; medium=100; complex=50; }
            else if (target == 25000) { simple=500; medium=400; complex=100; }
            else if (target >= 50000 && target <= 80000 && target % 10000 == 0)
            { simple=1000; medium=800; complex=200+(target-50000)/80; }
            else throw new ArgumentOutOfRangeException(nameof(target));
            if (simple*10+medium*30+complex*80 != target)
                throw new InvalidOperationException("PVT-Final composition does not match piece count.");
        }

        public static int BuildingCount(int target)
        {
            Composition(target,out int simple,out int medium,out int complex);
            return simple+medium+complex;
        }

        public static bool ShouldStopD(int target, double minimumMeanFps) =>
            target > 50000 && !double.IsNaN(minimumMeanFps) && minimumMeanFps < 60d;

        public static PieceData PieceAt(int index, int target, int seed = Seed)
        {
            Composition(target,out int simple,out int medium,out int complex);
            if (index < 0 || index >= target) throw new ArgumentOutOfRangeException(nameof(index));
            int building, part, level;
            if (index < simple*10) { building=index/10; part=index%10; level=0; }
            else if (index < simple*10+medium*30)
            { int relative=index-simple*10; building=simple+relative/30; part=relative%30; level=1; }
            else
            { int relative=index-simple*10-medium*30; building=simple+medium+relative/80; part=relative%80; level=2; }
            Vector2Int cell=VisualBenchmarkScenario.Cell(building);
            float x=cell.x*PlotSpacing, z=cell.y*PlotSpacing;
            if (Mathf.Abs(x)>475f || Mathf.Abs(z)>475f)
                throw new InvalidOperationException("PVT-Final building extends beyond the 1 km terrain.");
            uint hash=unchecked((uint)seed*16777619u ^ (uint)building*1103515245u);
            float ground=Pvt2Scenario.GroundHeight(x,z);
            Quaternion rotation=Quaternion.Euler(0f,(building%8)*45f,0f);
            Vector3 origin=new Vector3(x+(hash&3u)*0.12f,ground,z+((hash>>2)&3u)*0.12f);
            PieceTransform At(float px,float py,float pz) =>
                new PieceTransform(origin+rotation*new Vector3(px,py,pz),rotation);
            PieceId id=PieceId.Parse(new Guid(seed,(short)building,(short)part,
                0x50,0x56,0x54,0x46,0,0,0,1).ToString("N"));
            MaterialId masonry=building%3==0 ? LabMaterialIds.Stone
                : building%3==1 ? LabMaterialIds.Brick : LabMaterialIds.Plaster;
            PieceData piece;
            switch (part)
            {
                case 0: piece=new PieceData(id,At(0f,0f,0f),new SlabDimensions(8f,0.35f,7f)); break;
                case 1: piece=new PieceData(id,At(0f,0.35f,-3.5f),new WallDimensions(8f,3.4f,0.4f));
                    piece=piece.WithOpening(new WallOpening(new Guid(seed,(short)building,(short)part,
                        0x50,0x56,0x54,0x46,0,0,0,2),id,WallOpeningKind.Passage,3.2f,0f,1.6f,2.6f)); break;
                case 2: piece=new PieceData(id,At(0f,0.35f,3.5f),new WallDimensions(8f,3.4f,0.4f)); break;
                case 3: case 4:
                    piece=new PieceData(id,At(part==3 ? -4f : 4f,0.35f,0f),new WallDimensions(7f,3.4f,0.4f));
                    piece=piece.WithOpening(new WallOpening(new Guid(seed,(short)building,(short)part,
                        0x50,0x56,0x54,0x46,0,0,0,3),id,WallOpeningKind.Window,2.5f,1.1f,1.6f,1.3f)); break;
                case 5:
                    piece=new PieceData(id,At(0f,3.75f,0f),
                        building%3==0 ? new PieceDimensions(new GableRoofDimensions(9f,8f,0.35f,1.8f))
                        : building%3==1 ? new PieceDimensions(new HipRoofDimensions(9f,8f,0.35f,1.8f))
                        : new PieceDimensions(new ShedRoofDimensions(9f,8f,0.35f,1.8f)));
                    return piece.WithMaterial(Ceramic);
                case 6: piece=new PieceData(id,At(0f,0.35f,-3.7f),
                    new ArchDimensions(3f,3.1f,0.65f,0.5f,1.2f,0.25f)); break;
                case 7: piece=new PieceData(id,At(0f,0.35f,-5.2f),new StairDimensions(2.5f,0.9f,2f,4)); break;
                case 8: piece=new PieceData(id,At(-3.4f,0.35f,-3.3f),new ColumnDimensions(0.45f,3.4f,0.45f)); break;
                case 9: piece=new PieceData(id,At(3.4f,0.35f,-3.3f),new ColumnDimensions(0.45f,3.4f,0.45f)); break;
                default: piece=Detail(id,At,part,level); break;
            }
            return piece.WithMaterial(part==0 ? LabMaterialIds.Stone
                : part==7 ? Wood : part>=10 && part%5==0 ? Ceramic : masonry);
        }

        private static PieceData Detail(PieceId id, Func<float,float,float,PieceTransform> at, int part, int level)
        {
            // Repeated details are geometric pieces, not decorative GameObjects detached from the world.
            int i=part-10;
            if (i<8) return new PieceData(id,at(-3.2f+i*0.9f,0.35f,3.65f),
                new ColumnDimensions(0.28f,3.3f,0.28f));
            if (i<16) return new PieceData(id,at(-3.2f+(i-8)*0.9f,3.65f,3.65f),
                new BeamDimensions(0.8f,0.3f,0.4f));
            if (i<20) return new PieceData(id,at(-3f+(i-16)*2f,3.75f,-3.6f),
                new ParapetDimensions(1.5f,0.6f,0.35f));
            int j=i-20;
            float angle=j*2f*Mathf.PI/50f;
            float radius=level==2 ? 5.2f : 4.5f;
            float px=Mathf.Cos(angle)*radius, pz=Mathf.Sin(angle)*radius;
            switch (j%5)
            {
                case 0: return new PieceData(id,at(px,0.35f,pz),new ColumnDimensions(0.5f,5.5f,0.5f));
                case 1: return new PieceData(id,at(px,5.85f,pz),new ParapetDimensions(1.2f,0.75f,0.4f));
                case 2: return new PieceData(id,at(px,0.35f,pz),new ArchDimensions(1.8f,3.4f,0.5f,0.35f,1.2f,0.2f));
                case 3: return new PieceData(id,at(px,5.85f,pz),new FlatRoofDimensions(1.6f,1.6f,0.3f));
                default: return new PieceData(id,at(px,6.15f,pz),
                    j%2==0 ? new PieceDimensions(new DomeDimensions(1.8f,1f,0.2f))
                        : new PieceDimensions(new VaultDimensions(1.8f,1.1f,1.6f,0.2f)));
            }
        }
    }
}
