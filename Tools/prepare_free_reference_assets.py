"""Fetch the five verified Poly Haven CC0 reference assets at 1K.

Run: python3 Tools/prepare_free_reference_assets.py SOURCE_CACHE
Run in the repository root. Blender source FBX files go to the supplied cache
outside Assets; only the derived game FBX files are committed. Requires curl
and Pillow. Never selects 2K/4K/8K variants.
"""

import csv
import hashlib
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image


ROOT = Path("Assets/Game/Art/FreeReference")
MANIFEST = Path("Docs/PVT_FREE_ASSETS_MANIFEST.csv")
SOURCES = (
    ("DryGround", "dry_ground_01", False, False),
    ("OldSandstone", "old_sandstone_02", False, False),
    ("BermudaGrass", "grass_bermuda_01", True, True),
    ("WeatheredRock", "rock_07", True, False),
    ("CeramicPot", "ceramic_pot", True, False),
)
FIELDS = ("local_path", "asset_id", "author", "official_url", "source_url",
          "license", "resolution", "upstream_md5", "local_sha256", "notes",
          "approx_triangles", "lod", "format")


def fetch(url: str) -> bytes:
    return subprocess.check_output(["curl", "--fail", "--location", "--silent",
                                    "--show-error", "--user-agent", "AEDIFICA CC0 import", url])


def source(node: dict, key: str, fmt: str) -> dict:
    return node[key]["1k"][fmt]


def save_source(rows: list, folder: Path, path: Path, asset_id: str,
                authors: str, metadata: dict, kind: str) -> None:
    payload = fetch(metadata["url"])
    digest = hashlib.md5(payload).hexdigest()
    if digest != metadata["md5"]:
        raise ValueError(f"Poly Haven MD5 mismatch: {asset_id} {kind}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    rows.append({"local_path": str(path), "asset_id": asset_id, "author": authors,
                 "official_url": "https://polyhaven.com/a/" + asset_id,
                 "source_url": metadata["url"], "license": "CC0 - https://polyhaven.com/license",
                 "resolution": "1024x1024", "upstream_md5": digest,
                 "local_sha256": hashlib.sha256(payload).hexdigest(),
                 "notes": kind + " source map", "approx_triangles": "n/a",
                 "lod": "n/a", "format": path.suffix.lstrip(".").upper()})


def derived(rows: list, path: Path, asset_id: str, authors: str, notes: str) -> None:
    rows.append({"local_path": str(path), "asset_id": asset_id, "author": authors,
                 "official_url": "https://polyhaven.com/a/" + asset_id,
                 "source_url": "https://polyhaven.com/a/" + asset_id,
                 "license": "CC0 - https://polyhaven.com/license",
                 "resolution": "1024x1024", "upstream_md5": "",
                 "local_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                 "notes": notes, "approx_triangles": "n/a", "lod": "n/a",
                 "format": path.suffix.lstrip(".").upper()})


def main(cache: Path) -> None:
    rows = []
    for name, asset_id, model, alpha in SOURCES:
        info = json.loads(fetch("https://api.polyhaven.com/info/" + asset_id))
        files = json.loads(fetch("https://api.polyhaven.com/files/" + asset_id))
        authors = "; ".join(info["authors"].keys())
        folder = ROOT / name
        maps = (("Diffuse", "jpg", "Albedo_1k.jpg"),
                ("nor_gl", "png", "Normal_1k.png"),
                ("Rough", "jpg", "Roughness_1k.jpg"))
        if alpha:
            maps += (("Alpha", "png", "Alpha_1k.png"),)
        if "Metal" in files:
            maps += (("Metal", "jpg", "Metallic_1k.jpg"),)
        for key, fmt, suffix in maps:
            save_source(rows, folder, folder / (name + "_" + suffix), asset_id,
                        authors, source(files, key, fmt), key)
        rough = Image.open(folder / (name + "_Roughness_1k.jpg")).convert("L")
        metal_path = folder / (name + "_Metallic_1k.jpg")
        metal = Image.open(metal_path).convert("L") if metal_path.exists() else Image.new("L", rough.size, 0)
        full = Image.new("L", rough.size, 255)
        zero = Image.new("L", rough.size, 0)
        mask_path = folder / (name + "_MetallicSmoothness_1k.png")
        Image.merge("RGBA", (metal, full, zero, rough.point(lambda pixel: 255 - pixel)))\
            .save(mask_path, optimize=True)
        derived(rows, mask_path, asset_id, authors,
                "metal red; full AO green; inverse roughness smoothness alpha")
        if alpha:
            base = Image.open(folder / (name + "_Albedo_1k.jpg")).convert("RGB")
            opacity = Image.open(folder / (name + "_Alpha_1k.png")).convert("L")
            base.putalpha(opacity)
            base_path = folder / (name + "_Base_1k.png")
            base.save(base_path, optimize=True)
            derived(rows, base_path, asset_id, authors, "cutout diffuse alpha packed")
        if model:
            metadata = source(files, "fbx", "fbx")
            model_path = cache / (name + "_source.fbx")
            payload = fetch(metadata["url"])
            if hashlib.md5(payload).hexdigest() != metadata["md5"]:
                raise ValueError("FBX MD5 mismatch: " + asset_id)
            model_path.parent.mkdir(parents=True, exist_ok=True)
            model_path.write_bytes(payload)
            print(name, "source FBX cached", len(payload), "bytes", flush=True)
    # Derived FBX files can already exist when the source maps are refreshed.
    # Keep the manifest complete across repeatable runs.
    mesh_root = ROOT / "Models"
    model_names = {name: (asset, "; ".join(json.loads(fetch(
        "https://api.polyhaven.com/info/" + asset))["authors"].keys()))
        for name, asset, model, _ in SOURCES if model}
    triangle_counts = {"BermudaGrass_LOD0.fbx": 919,
                       "BermudaGrass_LOD1.fbx": 483,
                       "BermudaGrass_LOD2.fbx": 229,
                       "WeatheredRock_LOD0.fbx": 3496,
                       "WeatheredRock_LOD1.fbx": 896,
                       "WeatheredRock_LOD2.fbx": 196,
                       "CeramicPot.fbx": 3592}
    for mesh in sorted(mesh_root.glob("*.fbx")):
        name = next((item for item in model_names if mesh.name.startswith(item)), None)
        if name is None:
            raise ValueError("Unregistered derived FBX: " + str(mesh))
        asset, authors = model_names[name]
        rows.append({"local_path": str(mesh), "asset_id": asset, "author": authors,
                     "official_url": "https://polyhaven.com/a/" + asset,
                     "source_url": "https://polyhaven.com/a/" + asset,
                     "license": "CC0 - https://polyhaven.com/license",
                     "resolution": "model", "upstream_md5": "",
                     "local_sha256": hashlib.sha256(mesh.read_bytes()).hexdigest(),
                     "notes": "Blender 4.3 derived from official 1K FBX; UV-preserving decimation; see Tools/build_free_reference_lods.py",
                     "approx_triangles": str(triangle_counts[mesh.name]),
                     "lod": mesh.stem.split("_LOD")[-1] if "_LOD" in mesh.stem else "single",
                     "format": "FBX"})
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    with MANIFEST.open("w", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    print("Verified", len(rows), "1K files and derived maps", flush=True)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python Tools/prepare_free_reference_assets.py SOURCE_CACHE")
    main(Path(sys.argv[1]))
