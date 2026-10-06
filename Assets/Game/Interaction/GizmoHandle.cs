using UnityEngine;

namespace Aedifica.Interaction
{
    public sealed class GizmoHandle : MonoBehaviour
    {
        public ManipulationMode Mode { get; private set; }
        public ManipulationAxis Axis { get; private set; }

        public void Configure(ManipulationMode mode, ManipulationAxis axis)
        {
            Mode = mode;
            Axis = axis;
        }
    }
}
