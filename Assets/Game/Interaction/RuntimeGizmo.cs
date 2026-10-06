using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public sealed class RuntimeGizmo : MonoBehaviour
    {
        private readonly GizmoHandle[,] handles = new GizmoHandle[3, 3];
        private UnityEngine.Camera sceneCamera;

        public void Initialize(UnityEngine.Camera camera, Material material)
        {
            sceneCamera = camera;
            CreateHandle(ManipulationMode.Move, ManipulationAxis.X, PrimitiveType.Cube, Color.red, material);
            CreateHandle(ManipulationMode.Move, ManipulationAxis.Y, PrimitiveType.Cube, Color.green, material);
            CreateHandle(ManipulationMode.Move, ManipulationAxis.Z, PrimitiveType.Cube, Color.blue, material);
            CreateHandle(ManipulationMode.Rotate, ManipulationAxis.Y, PrimitiveType.Sphere, Color.yellow, material);
            CreateHandle(ManipulationMode.Resize, ManipulationAxis.X, PrimitiveType.Cube, Color.red, material);
            CreateHandle(ManipulationMode.Resize, ManipulationAxis.Y, PrimitiveType.Cube, Color.green, material);
            CreateHandle(ManipulationMode.Resize, ManipulationAxis.Z, PrimitiveType.Cube, Color.blue, material);
            gameObject.SetActive(false);
        }

        private void CreateHandle(ManipulationMode mode, ManipulationAxis axis, PrimitiveType primitive, Color color, Material material)
        {
            GameObject handleObject = GameObject.CreatePrimitive(primitive);
            handleObject.name = $"{mode} {axis} Handle";
            handleObject.transform.SetParent(transform, false);
            handleObject.transform.localScale = Vector3.one * 0.32f;
            handleObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            handleObject.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            GizmoHandle handle = handleObject.AddComponent<GizmoHandle>();
            handle.Configure(mode, axis);
            handles[(int)mode, (int)axis] = handle;
        }

        public void Show(PieceData piece, ManipulationMode mode)
        {
            gameObject.SetActive(true);
            float distance = Vector3.Distance(sceneCamera.transform.position, piece.Transform.Position);
            float scale = Mathf.Clamp(distance * 0.04f, 0.35f, 8f);
            bool resize = mode == ManipulationMode.Resize;
            transform.SetPositionAndRotation(piece.Transform.Position + (resize ? Vector3.zero : piece.Transform.Rotation * new Vector3(0f, piece.BlockDimensions.Height * 0.5f, 0f)),
                resize ? piece.Transform.Rotation : Quaternion.identity);
            transform.localScale = Vector3.one * scale;

            for (int m = 0; m < 3; m++)
                for (int a = 0; a < 3; a++)
                    if (handles[m, a] != null) handles[m, a].gameObject.SetActive(m == (int)mode);

            if (resize)
            {
                handles[(int)mode, 0].transform.localPosition = new Vector3(piece.BlockDimensions.Width * 0.5f / scale + 0.5f, piece.BlockDimensions.Height * 0.5f / scale, 0f);
                handles[(int)mode, 1].transform.localPosition = new Vector3(0f, piece.BlockDimensions.Height / scale + 0.5f, 0f);
                handles[(int)mode, 2].transform.localPosition = new Vector3(0f, piece.BlockDimensions.Height * 0.5f / scale, piece.BlockDimensions.Depth * 0.5f / scale + 0.5f);
            }
            else
            {
                float reach = Mathf.Max(piece.BlockDimensions.Width, piece.BlockDimensions.Height, piece.BlockDimensions.Depth)
                    * 0.5f / scale + 0.8f;
                for (int axis = 0; axis < 3; axis++)
                    if (handles[(int)mode, axis] != null)
                        handles[(int)mode, axis].transform.localPosition = ManipulationSession.AxisVector((ManipulationAxis)axis) * reach;
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
