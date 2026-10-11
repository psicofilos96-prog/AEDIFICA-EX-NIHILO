"""Fetch verified CC0 island_tree_02 1K maps/source for Blender LOD generation.

Run from repository root: python3 Tools/prepare_grand_village_tree.py /tmp/aedifica-tree
Then: blender -b -t 2 --python Tools/build_grand_village_tree_lods.py -- /tmp/aedifica-tree
The 32 MB source FBX stays outside Assets. Only capped LODs enter Unity.
"""
import csv
import hashlib
import json
import subprocess
import sys
import uuid
from pathlib import Path

from PIL import Image

ASSET = "island_tree_02"
ROOT = Path("Assets/Game/Art/GrandVillage/CoastalTree")
MAPS = (
    ("Diffuse", "jpg", "CoastalTree_TrunkAlbedo_1k.jpg"),
    ("nor_gl", "jpg", "CoastalTree_TrunkNormal_1k.jpg"),
    ("Rough", "jpg", "CoastalTree_TrunkRoughness_1k.jpg"),
    ("branches_diff", "jpg", "CoastalTree_BranchesAlbedo_1k.jpg"),
    ("branches_nor_gl", "png", "CoastalTree_BranchesNormal_1k.png"),
    ("branches_rough", "jpg", "CoastalTree_BranchesRoughness_1k.jpg"),
    ("leaves_diff", "png", "CoastalTree_LeavesAlbedo_1k.png"),
    ("leaves_nor_gl", "png", "CoastalTree_LeavesNormal_1k.png"),
    ("leaves_rough", "png", "CoastalTree_LeavesRoughness_1k.png"),
    ("leaves_alpha", "png", "CoastalTree_LeavesAlpha_1k.png"),
)


def fetch(url):
    return subprocess.check_output(["curl", "-fLsS", "-A", "Mozilla/5.0", url])


def meta(path):
    sidecar = Path(str(path) + ".meta")
    if not sidecar.exists():
        normal = "Normal" in path.name
        linear = normal or "Mask" in path.name
        sidecar.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex +
                           "\nTextureImporter:\n  internalIDToNameTable: []\n"+
                           "  externalObjects: {}\n  serializedVersion: 13\n"+
                           "  mipmaps:\n    enableMipMap: 1\n  isReadable: 0\n"+
                           "  textureType: " + ("1" if normal else "0") + "\n"+
                           "  sRGBTexture: " + ("0" if linear else "1") + "\n"+
                           "  maxTextureSize: 1024\n  userData:\n  assetBundleName:\n"+
                           "  assetBundleVariant:\n")


def main(cache):
    files = json.loads(fetch("https://api.polyhaven.com/files/" + ASSET))
    info = json.loads(fetch("https://api.polyhaven.com/info/" + ASSET))
    authors = "; ".join(info["authors"])
    ROOT.mkdir(parents=True, exist_ok=True)
    rows = []
    for key, fmt, filename in MAPS:
        entry = files[key]["1k"][fmt]
        path = ROOT / filename
        data = path.read_bytes() if path.exists() and hashlib.md5(path.read_bytes()).hexdigest()==entry["md5"] else fetch(entry["url"])
        if hashlib.md5(data).hexdigest() != entry["md5"]:
            raise ValueError("Poly Haven MD5 mismatch: " + key)
        path.write_bytes(data)
        meta(path)
        rows.append((str(path), entry["url"], entry["md5"], hashlib.sha256(data).hexdigest()))
    leaves = Image.open(ROOT / "CoastalTree_LeavesAlbedo_1k.png").convert("RGBA")
    leaves.putalpha(Image.open(ROOT / "CoastalTree_LeavesAlpha_1k.png").convert("L"))
    out = ROOT / "CoastalTree_LeavesBase_1k.png"
    leaves.save(out, optimize=True)
    meta(out)
    for stem in ("Trunk", "Branches", "Leaves"):
        rough = Image.open(ROOT / ("CoastalTree_" + stem + "Roughness_1k." +
                                   ("png" if stem == "Leaves" else "jpg"))).convert("L")
        full = Image.new("L", rough.size, 255)
        zero = Image.new("L", rough.size, 0)
        out = ROOT / ("CoastalTree_" + stem + "Mask_1k.png")
        Image.merge("RGBA", (zero, full, zero, rough.point(lambda v: 255-v))).save(out, optimize=True)
        meta(out)
    cache.mkdir(parents=True, exist_ok=True)
    fbx = files["fbx"]["1k"]["fbx"]
    cached = cache / "CoastalTree_source.fbx"
    data = cached.read_bytes() if cached.exists() and hashlib.md5(cached.read_bytes()).hexdigest()==fbx["md5"] else fetch(fbx["url"])
    if hashlib.md5(data).hexdigest() != fbx["md5"]:
        raise ValueError("Poly Haven FBX MD5 mismatch")
    cached.write_bytes(data)
    manifest = Path("Docs/GRAND_VILLAGE_ASSETS.csv")
    with manifest.open("w", newline="") as output:
        writer = csv.writer(output, lineterminator="\n")
        writer.writerow(("local_path", "source_url", "license", "resolution", "upstream_md5", "sha256"))
        for path, url, md5, sha in rows:
            writer.writerow((path, url, "CC0 https://polyhaven.com/license", "1K", md5, sha))
        for path in sorted(ROOT.glob("*Base_1k.png")) + sorted(ROOT.glob("*Mask_1k.png")):
            writer.writerow((str(path), "derived from island_tree_02 1K maps",
                             "CC0 https://polyhaven.com/license", "1K", "",
                             hashlib.sha256(path.read_bytes()).hexdigest()))
        writer.writerow(("source FBX in external cache", fbx["url"],
                         "CC0 https://polyhaven.com/license", "model", fbx["md5"],
                         hashlib.sha256(data).hexdigest()))
    print("Verified 1K coastal tree maps and cached source FBX; authors:", authors)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: python3 Tools/prepare_grand_village_tree.py CACHE_DIR")
    main(Path(sys.argv[1]))
