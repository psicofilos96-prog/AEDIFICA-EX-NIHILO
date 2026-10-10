using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    public readonly struct FinishSegment
    {
        public Guid FinishId { get; }
        public MaterialId MaterialId { get; }
        public BlockGeometry Geometry { get; }
        public FinishSegment(Guid id,MaterialId material,BlockGeometry geometry)
        { FinishId=id;MaterialId=material;Geometry=geometry; }
    }

    // Meshes are derived in host-local space every time the host changes.
    public static class ArchitecturalFinishGeometry
    {
        public static List<FinishSegment> Generate(PieceData host)
        {
            var result=new List<FinishSegment>();
            foreach (ArchitecturalFinish finish in host.Finishes)
            {
                float p=finish.Profile,front=host.Dimensions.Z*0.5f+finish.Projection;
                if (finish.Kind==ArchitecturalFinishKind.RoofEave)
                {
                    Vector3[] edges=host.Dimensions.IsSlopedRoof
                        ? RoofProfile.Create(host.Dimensions).Eaves
                        : new[] { new Vector3(-host.Dimensions.X*0.5f,host.Dimensions.Y,-host.Dimensions.Z*0.5f),
                            new Vector3(host.Dimensions.X*0.5f,host.Dimensions.Y,-host.Dimensions.Z*0.5f),
                            new Vector3(host.Dimensions.X*0.5f,host.Dimensions.Y,host.Dimensions.Z*0.5f),
                            new Vector3(-host.Dimensions.X*0.5f,host.Dimensions.Y,host.Dimensions.Z*0.5f) };
                    for (int i=0;i<4;i++)
                    {
                        Vector3 a=edges[i],b=edges[(i+1)%4];
                        Vector3 direction=b-a;
                        Add(result,finish,new Vector3(direction.magnitude,p,p+finish.Projection),
                            (a+b)*0.5f-Vector3.up*(p*0.35f),
                            Quaternion.FromToRotation(Vector3.right,direction.normalized),true);
                    }
                    continue;
                }
                WallOpening opening=default;
                foreach (WallOpening candidate in host.Openings)
                    if (candidate.Id==finish.OpeningId) { opening=candidate;break; }
                if (opening.Id==Guid.Empty) throw new InvalidOperationException("Finish opening is absent.");
                float left=opening.Left-host.Dimensions.X*0.5f;
                float right=left+opening.Width;
                float z=front;
                switch (finish.Kind)
                {
                    case ArchitecturalFinishKind.DoorFrame:
                    case ArchitecturalFinishKind.WindowFrameSill:
                        Add(result,finish,new Vector3(p,opening.Height+p,p),
                            new Vector3(left-p*0.5f,opening.Bottom+opening.Height*0.5f,z));
                        Add(result,finish,new Vector3(p,opening.Height+p,p),
                            new Vector3(right+p*0.5f,opening.Bottom+opening.Height*0.5f,z));
                        Add(result,finish,new Vector3(opening.Width+2f*p,p,p),
                            new Vector3((left+right)*0.5f,opening.Top,z));
                        if (finish.Kind==ArchitecturalFinishKind.WindowFrameSill)
                        {
                            Add(result,finish,new Vector3(opening.Width+3f*p,p,p+finish.Projection),
                                new Vector3((left+right)*0.5f,opening.Bottom-p,z));
                            // Glazing is dependent visual geometry, not an orphan PieceData.
                            Add(result,finish,new Vector3(opening.Width-2f*p,
                                opening.Height-2f*p,0.035f),
                                new Vector3((left+right)*0.5f,opening.Bottom+opening.Height*0.5f,
                                    host.Dimensions.Z*0.5f+0.018f),Quaternion.identity,false,
                                ArchitecturalFinishMaterials.Glass);
                        }
                        break;
                    case ArchitecturalFinishKind.Lintel:
                        Add(result,finish,new Vector3(opening.Width+2f*p,p*1.5f,p+finish.Projection),
                            new Vector3((left+right)*0.5f,opening.Top,z));
                        break;
                    default: throw new InvalidOperationException("Unsupported finish kind.");
                }
            }
            return result;
        }

        private static void Add(List<FinishSegment> result,ArchitecturalFinish finish,
            Vector3 size,Vector3 center,Quaternion rotation=default,bool centered=false,
            MaterialId materialOverride=default)
        {
            BlockGeometry source=BlockGeometryGenerator.Generate(new BlockDimensions(size.x,size.y,size.z));
            var vertices=new Vector3[source.Vertices.Length];
            var normals=new Vector3[source.Normals.Length];
            Vector3 pivot=centered ? Vector3.up*(size.y*0.5f) : Vector3.zero;
            Vector3 basePosition=centered ? center : center-Vector3.up*(size.y*0.5f);
            if (rotation.x==0f && rotation.y==0f && rotation.z==0f && rotation.w==0f)
                rotation=Quaternion.identity;
            for (int i=0;i<vertices.Length;i++)
            {
                vertices[i]=basePosition+rotation*(source.Vertices[i]-pivot);
                normals[i]=rotation*source.Normals[i];
            }
            var bounds=new Bounds(vertices[0],Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
            result.Add(new FinishSegment(finish.Id,
                materialOverride.IsValid ? materialOverride : finish.MaterialId,
                new BlockGeometry(vertices,source.Triangles,normals,source.UVs,bounds)));
        }
    }
}
