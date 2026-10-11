# Grand River Village — progressive Windows scale lab

This is an opt-in scalability experiment, separate from the visual-review scene. It does **not** certify playability or replace manual inspection. Unity 6000.6.4f1 on the Lenovo LOQ must compile, run the tests, build and produce the measurements.

## Workload

The exact logical piece targets are **1,000, 5,000, 10,000, 25,000, 50,000 and 100,000**. The immutable, authored castle/barracks/market/18-house/bridge village is repeated in a fixed 16-column grid. Copies preserve piece type, dimensions, material, wall openings and architectural finishes, while receiving distinct stable IDs. The last village may be partial to reach the exact requested piece count. One complete 180 × 180 m terrain, river, road and 230-prefab vegetation environment is created for each occupied village tile. This deliberately makes environmental cost grow with architectural scale. No per-piece GameObject is created: `PvtChunkVisualEngine` keeps combined visual pages and individual logical `PieceData`.

The three camera modes are castle (near architecture), street (near mixed scene) and panorama (whole settlement). Each size/mode has 20 s warmup, 45 s capture and three repetitions. The progressive run therefore takes at least about one hour plus world/mesh build time. The runner stops on an error or 8 GiB Unity reserved-memory guard, recording a `failed` row. A process termination or OS OOM may leave only the last completed row; keep `Player.log` for that case. No size is silently reduced.

The main CSV reports mean FPS, 1% low FPS, p50/p95/p99/max frame time, optional Unity CPU/GPU/draw counters, managed/Unity/process memory, build times, region/page and geometry counts. The companion `.edits.csv` times a representative piece's begin/end edit, move, resize, material change, deletion and Undo/Redo at each tier; state must be restored before FPS capture. These events are **outside** the frame-time window. Missing counters are `unavailable`. VRAM used is `unavailable`, since Unity has no trustworthy process VRAM value here. The camera modes have different visible content; compare **the same mode across sizes**, not castle versus panorama. The near cameras stay fixed as the world grows. Panorama pulls back to fit the settlement, so projected piece size changes between tiers; it measures an overview use case, not fixed-distance scaling. Panorama also uses a longer fog range and far clip. The benchmark does not simulate long interactive gestures or measure saving 100,000 pieces (the existing snapshot format has a 100,000-piece cap).

## Windows procedure

Keep existing worktrees and stashes. In PowerShell, from an existing Aedifica Git repository:

```powershell
cd C:\Projetos\AEDIFICA-EX-NIHILO
git fetch origin pvt/grand-village-scale-lab
git worktree add C:\Projetos\AEDIFICA-GRAND-SCALE origin/pvt/grand-village-scale-lab
cd C:\Projetos\AEDIFICA-GRAND-SCALE
git rev-parse HEAD
git status --short --branch
git lfs pull
```

Do not run `git worktree add` into an existing directory. Open **C:\Projetos\AEDIFICA-GRAND-SCALE** in Unity 6000.6.4f1; wait for all CC0/URP assets to import. Run EditMode and PlayMode tests (at least `PvtGrandVillageScaleTests` and `PvtGrandVillageScalePlayModeTests`). In the Editor select **Tools → Aedifica → Grand River Village → Create Scale Lab Scene**. This writes `Assets/Game/Scenes/PvtGrandRiverScaleLab.unity` and puts it first in Build Settings. The command refuses to overwrite an existing scene. Open it and verify its serialized dependencies. For a benchmark build, enable **only** this scene in the Windows x64 Release build. Set the PC URP quality level, build to a new directory outside the repository, disable Development Build, Autoconnect Profiler, VSync and frame limit. The runner also sets VSync to zero and `targetFrameRate=-1` at launch. Use 1920 × 1080.

Example PowerShell invocation, replacing the executable path:

```powershell
cd C:\Projetos\AEDIFICA-GRAND-SCALE
$env:AEDIFICA_COMMIT = git rev-parse HEAD
& 'C:\Projetos\Builds\AedificaGrandScale\AEDIFICA-EX-NIHILO.exe' -grand-scale-run -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile 'C:\Projetos\Builds\AedificaGrandScale\Player.log'
```

The Player writes `aedifica_grand_scale_*.csv` and `aedifica_grand_scale_*.edits.csv` to `Application.persistentDataPath`, normally `C:\Users\<user>\AppData\LocalLow\DefaultCompany\AEDIFICA-EX-NIHILO`. Preserve both CSVs and `Player.log` before another run. Check `status=ok`, all 54 capture rows (six sizes × three modes × three repeats), all edit rows, actual hardware/resolution, absence of caps, and the presence of valid frame samples. A missing/failure row is not a performance result. Repeat a full run with the same build and compare like camera modes. Avoid using Editor or Development Player numbers as the acceptance measurement.

## Interpretation

There is no automatic pass based on a single mean FPS. For each camera mode, inspect median and worst repeat, 1% low, p95/p99 frame time, stutters, memory and the growth of build time. Consider responsiveness, visual quality and manual editability separately. A good near-camera FPS with poor panorama FPS indicates a visibility/streaming problem; it does not establish a universal piece limit. The 100,000 tier is a stress point, not a target promise. No Unity build or Lenovo FPS was measured in the remote environment.
