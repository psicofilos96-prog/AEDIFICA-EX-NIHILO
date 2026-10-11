# Grand River Castle Village — visual review

This scene extends the small medieval village without replacing it. It is an authored eastern Mediterranean inspired **review scenario**, not a historical reconstruction or a certified performance target. Unity 6000.6.4f1 on Windows must compile, render, and pass Test Runner before visual approval.

## Composition and ownership

- 180 × 180 m terrain with a 12 m castle plateau, lower barracks/market terraces and a sinuous river. Terrain, water, roads, vegetation and loose rocks are environmental objects; the stone arch bridge is built from editable architectural pieces.
- 59 × 53 m castle foundation: four 7 m corner towers, 9 m curtain walls, merlons, framed gate, three level keep, dome, courtyard stairs and side wings.
- 24 × 12 m barracks with five roofed sleeping bays; five arch market stalls; 18 varied residences in three styles.
- Every architectural part is a separate `PieceData` with a stable `PieceId`. Existing `ConstructionWorld`, `ConstructionCommandHistory`, `PvtChunkVisualEngine`, linked opening finishes, snapshots and the selection/edit UI remain in use. Environment trees and river are intentionally independent of architectural editing.
- The old one house and smaller castle scenes retain their previous modes and save files. This scene saves to `aedifica_grand_river_village.pvt2`.

## Verified assets and optimization

The new coastal tree is Poly Haven **Island Tree 02**, authors Rob Tuytel and Rico Cilliers, [asset page](https://polyhaven.com/a/island_tree_02), [CC0 license](https://polyhaven.com/license). `Docs/GRAND_VILLAGE_ASSETS.csv` records each fetched 1K map, official URL, upstream MD5, local SHA-256 and three derived FBX hashes. Source FBX (32 MB, 544,639 render polygons plus a six polygon helper) is cached outside `Assets`. Blender excludes the helper and creates UV preserving LOD0/1/2 FBX meshes at 11,666/3,914/2,192 polygons. Unity creates one shared instanced URP/Lit material each for trunk, branches and alpha clipped leaves. Texture imports use 1024 px, mipmaps and compressed HQ. The scene scatters 230 deterministic environmental objects, selecting coastal trees, existing CC0 pine LODs, shrubs and rocks while excluding buildings, roads and river.

Existing 1K Poly Haven old sandstone, brick, wood, plaster, roof, dry ground and cobble road maps are reused. Their licenses and hashes are in `Docs/PVT_FREE_ASSETS_MANIFEST.csv` and `Docs/PVT_VISUAL_ASSET_MANIFEST.csv`. The shared persistent URP water material is reused; the water surface is a lightweight 120 segment mesh. No 4K/8K textures, unlicensed assets, or monolithic castle prefab are imported. Artistic quality, tree silhouette, shadows, water and map seams require visual review.

## Windows installation and review

From a clean checkout after branch publication, create a separate worktree rather than replacing `C:\Projetos\AEDIFICA-MEDIEVAL`:

```powershell
cd C:\Projetos\AEDIFICA-EX-NIHILO
git fetch origin pvt/grand-river-castle
git worktree add C:\Projetos\AEDIFICA-GRAND-RIVER origin/pvt/grand-river-castle
cd C:\Projetos\AEDIFICA-GRAND-RIVER
git rev-parse HEAD
git status --short --branch
```

Do not use the worktree command if its target folder already exists; inspect that folder first. Open the new project in Unity 6000.6.4f1. Wait for imports, then use **Tools → Aedifica → Grand River Village → Create Village Scene**. The menu creates persistent URP materials and LOD prefab, saves `Assets/Game/Scenes/PvtGrandRiverVillage.unity`, and puts it first in Build Settings. It refuses to overwrite an existing scene. Open the scene and enter Play Mode. The six view buttons are castle approach, barracks, street, river, courtyard and panorama. **Capture 6 views** writes actual Game View PNGs to `Application.persistentDataPath`.

Run EditMode and PlayMode suites, including `PvtGrandVillageTests` and `PvtGrandVillagePlayModeTests`. Inspect Console, material normal maps, LOD transitions, riverbed, castle foundations and visibility from every view. Select individual walls, gate, tower merlons, roofs and residential pieces; exercise Move, Resize where supported, material change, Add, Delete, Undo, Redo, Save and Load. Confirm the river and vegetation do not intercept architectural selection. Review generated `.unity`, materials and prefabs before versioning those Unity generated files.

For a Windows x64 visual build, verify Graphics and Quality both use `PC_RPAsset`, ensure only the desired review scene is enabled, and build via **File → Build Profiles → Windows**. The build validator checks scene dependencies and required URP materials. A build is not evidence of 60 FPS: measure 1080p frame time separately on the Lenovo LOQ. No performance benchmark is started by this scene.

## Reproducing the asset import

The checked in 1K source maps and LOD FBXs are sufficient for Unity. To reproduce them from the official CC0 source on a machine with Python Pillow, curl and Blender 4.3:

```bash
python3 Tools/prepare_grand_village_tree.py /tmp/aedifica-grand-tree
blender -b -t 2 --python Tools/build_grand_village_tree_lods.py -- /tmp/aedifica-grand-tree
```

The source FBX is deliberately cached outside Unity. Do not silently regenerate the checked in LODs during normal scene creation.

## Review gate and risks

Unity Editor and Windows build are unavailable in the remote implementation environment. Static syntax, GUID and hash checks do not prove import or visual quality. The three tree LODs have distinct polygon budgets, but alpha overdraw, terrain draw cost, shadows and the roughly 600–800 editable architectural pieces need profiling in the Windows Player. Terrain height is sampled on a 257 × 257 map, so foundation edges and the riverbank should be inspected for small gaps. The asset selection and visual composition require user approval before this can be called finished art.
