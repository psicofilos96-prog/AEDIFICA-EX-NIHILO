using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public sealed class RuntimeGizmo : MonoBehaviour
    {
        private readonly GizmoHandle[,] handles = new GizmoHandle[3, 3];
        private UnityEngine.Camera sceneCamera;
        private readonly GizmoHandle[] rotationSegments = new GizmoHandle[24];

        public void Initialize(UnityEngine.Camera camera, Material material)
        {
            sceneCamera = camera;
            CreateHandle(ManipulationMode.Move, ManipulationAxis.X, PrimitiveType.Cube, Color.red, material);
            CreateHandle(ManipulationMode.Move, ManipulationAxis.Y, PrimitiveType.Cube, Color.green, material);
            CreateHandle(ManipulationMode.Move, ManipulationAxis.Z, PrimitiveType.Cube, Color.blue, material);
            CreateHandle(ManipulationMode.Rotate, ManipulationAxis.Y, PrimitiveType.Sphere, Color.yellow, material);
            for (int i = 0; i < rotationSegments.Length; i++)
            {
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Rotate Y Ring {i}";
                segment.transform.SetParent(transform, false);
                segment.GetComponent<MeshRenderer>().sharedMaterial = material;
                var color = new MaterialPropertyBlock();
                color.SetColor("_BaseColor", Color.yellow);
                segment.GetComponent<MeshRenderer>().SetPropertyBlock(color);
                rotationSegments[i] = segment.AddComponent<GizmoHandle>();
                rotationSegments[i].Configure(ManipulationMode.Rotate, ManipulationAxis.Y);
            }
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
                if (mode == ManipulationMode.Rotate)
                {
                    // The ring shows the Y rotation plane; the camera-facing sphere is its hit target.
                    Vector3 towardCamera = sceneCamera.transform.position - transform.position;
                    towardCamera.y = 0f;
                    towardCamera = towardCamera.sqrMagnitude > 0.001f ? towardCamera.normalized : Vector3.back;
                    handles[(int)mode, (int)ManipulationAxis.Y].transform.localPosition = towardCamera * reach;
                    handles[(int)mode, (int)ManipulationAxis.Y].transform.localScale = Vector3.one * 0.65f;
                    for (int i = 0; i < rotationSegments.Length; i++)
                    {
                        float angle = i * Mathf.PI * 2f / rotationSegments.Length;
                        Transform segment = rotationSegments[i].transform;
                        segment.localPosition = new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
                        segment.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                        segment.localScale = new Vector3(0.24f, 0.18f, 2f * Mathf.PI * reach / rotationSegments.Length + 0.04f);
                    }
                }
            }
            foreach (GizmoHandle segment in rotationSegments) segment.gameObject.SetActive(mode == ManipulationMode.Rotate);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
