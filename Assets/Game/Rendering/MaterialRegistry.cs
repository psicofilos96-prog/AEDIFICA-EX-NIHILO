using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Scene-owned catalog. Every view resolves IDs to shared assets through this instance.
    public sealed class MaterialRegistry
    {
        private readonly Dictionary<MaterialId, Material> materials = new Dictionary<MaterialId, Material>();
        private readonly Material fallback;
        private readonly MaterialId[] cycleOrder;
        private readonly HashSet<MaterialId> reportedMissing = new HashSet<MaterialId>();
        private readonly bool logMissing;

        public MaterialRegistry(Material neutral, Material stone = null, Material brick = null, Material plaster = null, bool logMissing = false)
        {
            fallback = neutral != null ? neutral : throw new ArgumentNullException(nameof(neutral));
            this.logMissing = logMissing;
            Register(LabMaterialIds.Neutral, neutral);
            RegisterIfPresent(LabMaterialIds.Stone, stone);
            RegisterIfPresent(LabMaterialIds.Brick, brick);
            RegisterIfPresent(LabMaterialIds.Plaster, plaster);
            cycleOrder = new[] { LabMaterialIds.Neutral, LabMaterialIds.Stone, LabMaterialIds.Brick, LabMaterialIds.Plaster };
        }

        private void RegisterIfPresent(MaterialId id, Material material)
        {
            if (material != null) Register(id, material);
        }

        public void Register(MaterialId id, Material material)
        {
            if (!id.IsValid) throw new ArgumentException("Material ID must be valid.", nameof(id));
            if (material == null) throw new ArgumentNullException(nameof(material));
            materials.Add(id, material);
        }

        public bool TryGet(MaterialId id, out Material material) => materials.TryGetValue(id, out material);

        public Material Resolve(MaterialId id)
        {
            if (TryGet(id, out Material material)) return material;
            if (logMissing && reportedMissing.Add(id)) Debug.LogWarning($"Unknown material ID '{id}'; using neutral fallback.");
            return fallback;
        }

        public MaterialId Next(MaterialId current)
        {
            int index = Array.IndexOf(cycleOrder, current);
            for (int offset = 1; offset <= cycleOrder.Length; offset++)
            {
                MaterialId candidate = cycleOrder[(index + offset + cycleOrder.Length) % cycleOrder.Length];
                if (materials.ContainsKey(candidate)) return candidate;
            }
            return LabMaterialIds.Neutral;
        }
    }
}
