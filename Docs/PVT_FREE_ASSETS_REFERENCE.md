# Free Asset Visual Reference — integration gate

This branch starts at `e0594586387c67b25e5a5f9d12fbea6e63a1edfa` (`pvt/visual-art-review`). It is a **small art and integration gate**, not a new city benchmark or a visual approval. It contains one Mediterranean inspired masonry residence on dry ground, a weathered stone lane, grass, rocks, shrubs and two ceramic pots. All structural architectural elements remain `PieceData` with stable `PieceId`; the environmental meshes have no authority over construction data. No Unity Editor or Windows Player was available in the remote workspace, so screenshots, EditMode/PlayMode outcomes and visual quality remain unverified.

## Incorporated assets and rights

Every **new** external asset comes from [Poly Haven](https://polyhaven.com/license), licensed CC0. Poly Haven states that its CC0 assets may be used commercially and redistributed in games. The exact local file, source URL, author, resolution, source MD5 where provided and local SHA-256 are in [PVT_FREE_ASSETS_MANIFEST.csv](PVT_FREE_ASSETS_MANIFEST.csv). These are the five added source assets:

| Asset | Official page | Author | Use |
| --- | --- | --- | --- |
| Dry Ground 01 | https://polyhaven.com/a/dry_ground_01 | Rob Tuytel | Dry terrain PBR layer |
| Old Sandstone 02 | https://polyhaven.com/a/old_sandstone_02 | Dimitrios Savva | Shared URP masonry material |
| Bermuda Grass 01 | https://polyhaven.com/a/grass_bermuda_01 | Rico Cilliers | Cutout grass, three LOD meshes |
| Rock 07 | https://polyhaven.com/a/rock_07 | Jenelle van Heerden | Weathered rock, three LOD meshes |
| Ceramic Pot | https://polyhaven.com/a/ceramic_pot | Aron Łyczek | Two decorative pots |

The scene also reuses the previously imported Poly Haven road, ground, shrub, timber, roof and other PBR maps. Their provenance remains in the prior visual milestone manifest. No asset is labeled as historically accurate solely because it is CC0.

Source maps are 1024 × 1024. OpenGL normal maps and packed URP metallic/smoothness masks are explicitly configured by the material builder. The packed masks use red=metallic, green=occlusion (white), alpha=1−roughness. The grass base texture combines albedo and cutout alpha. Shared `Universal Render Pipeline/Lit` materials are created as persistent assets in `Assets/Game/Art/VisualMaterials`; no runtime `Shader.Find` is required by the scene. The existing persistent `Pvt2Terrain.mat` uses `Universal Render Pipeline/Terrain/Lit`.

The original FBXs are downloaded and MD5 checked into a local **external cache** by `Tools/prepare_free_reference_assets.py`. `Tools/build_free_reference_lods.py` converts them in Blender 4.3 to the checked-in UV preserving FBXs. Source FBXs are not checked in. The exported models are independently hashed in the manifest. Blender re-import and triangulation reports 919/483/229 triangles for grass LOD0/1/2, 3496/896/196 for rock, and 3592 for the pot. These are pre-Unity FBX triangle counts; inspect Unity Stats/Frame Debugger for actual rendering costs. LOD screen thresholds are 0.55/0.17/0.025 for grass and rock. Grass shadow casting is disabled; rock and pot retain shadows. The vegetation count is capped at 55 instances plus two pots on the 80 m terrain. No 4K or 8K maps were imported.

To reproduce source preparation from scratch, run `python3 Tools/prepare_free_reference_assets.py <cache path>` in the project root, then `blender -b -t 2 --python Tools/build_free_reference_lods.py -- <cache path>`. The first script uses only the official Poly Haven API and verifies upstream MD5. Its CLI and cache path are documented in its header. Rebuilding derived FBXs may change binary hashes when using a different Blender version; compare geometry and update the manifest deliberately.

## Windows Unity procedure

1. Use an isolated checkout of this branch. Preserve any other worktrees and stashes. In PowerShell:

   ```powershell
   cd C:\Projetos\AEDIFICA-VISUAL
   git status --short --branch
   git fetch origin pvt/free-assets-visual-reference
   git worktree add C:\Projetos\AEDIFICA-FREE-REFERENCE origin/pvt/free-assets-visual-reference
   cd C:\Projetos\AEDIFICA-FREE-REFERENCE
   git rev-parse HEAD
   ```

   If the target folder already exists or the listed source checkout has uncommitted changes, stop and inspect the paths rather than replacing anything. Open the **new** root containing `Assets`, `Packages` and `ProjectSettings` in Unity **6000.6.4f1**.
2. Allow asset import. Check Console for shader/model import errors. Use **Tools → Aedifica → Free Asset Reference → Prepare CC0 Assets**. The command creates shared URP materials and three prefabs. It validates existing materials and refuses inconsistent ones rather than silently overwriting them.
3. Use **Tools → Aedifica → Free Asset Reference → Create Reference Scene**. This creates `Assets/Game/Scenes/PvtFreeAssetsReference.unity` and adds it to enabled build scenes. It refuses to overwrite a scene already present. The scene file and generated materials/prefabs should be reviewed and committed separately after local validation.
4. Open the scene, enter Play Mode, then switch to Game view. The buttons `facade`, `materials`, `street`, `landscape` provide four fixed, reproducible views. Use **Capture 4 views** to write PNGs into `Application.persistentDataPath` (for Windows, typically `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`). Inspect the four images at 1920 × 1080 for correct normal orientation, UV continuity, cutout edges, LOD popping, material scale, shadows, roof and wall seams, vegetation size and terrain blend. Screenshots are not available from the remote workspace.
5. Click an architectural mesh (not environmental grass/rock) and exercise Move +X, Resize +X on an axis supported by that piece, Change material, Delete, Undo, Redo, Add block, Save and Load. Verify logical `PieceId`, localized rebuild and snapshot integrity. The UI is a review harness, not a replacement construction tool.
6. Run Unity Test Runner EditMode and PlayMode suites including `PvtFreeAssetsReferenceTests` and `PvtFreeAssetsReferencePlayModeTests`. Record counts and logs; do not infer pass from static checks.
7. If a standalone visual review is needed, keep this scene first in Build Profiles, select Windows x64, build a Development Player and inspect the same four views. Check the Player log for missing shaders/materials. This is an art gate only; **do not run the three extensive performance sessions yet**.

## Architecture and known gaps

- `PvtVisualCityScenario.BuildReferenceResidence()` uses one authored house recipe; wall openings, paneled door leaf, window glass/trim, roof and masonry are separately editable logical pieces. The `ConstructionWorld`/`ConstructionCommandHistory`/`PvtChunkVisualEngine`/`Pvt2Snapshot` path is unchanged. New environmental meshes never become construction authority. `Change material` swaps only existing logical masonry IDs.
- The door leaf and window trim are separate `PieceData` and therefore do **not** automatically follow a host wall moved on its own. This is a preexisting relationship gap, and must be considered during the human quality review. Decorative pots are independent environment props and likewise do not follow the doorway.
- The scene does **not** introduce a claimed period correct modular door/window asset. The verified free sources above supply environmental detail and natural surfaces; no legally verified, appropriately scaled period architectural module was found for this branch. The existing parametric architectural door/windows remain review candidates, not a claim of final visual realism. A purpose built modular door/window set with source/license/geometry review may still be required.
- The LOD meshes were algorithmically reduced and have UVs; silhouette, alpha clipping, normal map handedness and true URP visual result require Unity inspection. The reference scene is generated in Unity to ensure valid serialized GUID/fileID references. No handcrafted scene YAML is included.
- The test and art gate measures no FPS, GPU timings, VRAM or benchmark equivalence. Benchmarks E1/E2/PVT and their historical CSVs were not modified.

## Acceptance gate

Approval requires (a) clean Unity compilation and green applicable EditMode/PlayMode suites, (b) all four Game view captures reviewed by the user, (c) no magenta materials or missing assets in Editor or Windows Player, (d) independently editable residence pieces and working history/snapshot operations, and (e) an explicit artistic decision on whether the architecture and environmental assets are sufficiently convincing. Existing benchmark results cannot substitute for this inspection.
