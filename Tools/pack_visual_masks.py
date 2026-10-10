"""Rebuild checked-in 1K URP metallic/smoothness and cutout maps.

Requires Pillow. Input files and their Poly Haven origins are recorded in
Docs/PVT_VISUAL_ASSET_MANIFEST.csv. This script is not used by the Unity Player.
"""

from pathlib import Path

from PIL import Image


ROOT = Path("Assets/Game/Art/PolyHaven")


def pack_material(folder: Path, name: str, tag: str = "") -> None:
    stem = name + "_" + tag
    roughness = folder / (stem + "Roughness_1k.jpg")
    if not roughness.exists():
        return
    rough = Image.open(roughness).convert("L")
    metal_path = folder / (name + "_Metallic_1k.jpg")
    metallic = Image.open(metal_path).convert("L") if metal_path.exists() else Image.new("L", rough.size, 0)
    if metallic.size != rough.size:
        raise ValueError(name + " metallic map resolution differs")
    zero = Image.new("L", rough.size, 0)
    full_occlusion = Image.new("L", rough.size, 255)
    smoothness = rough.point(lambda pixel: 255 - pixel)
    # Terrain/Lit reads green as occlusion; white preserves full ambient light.
    Image.merge("RGBA", (metallic, full_occlusion, zero, smoothness)).save(
        folder / (stem + "MetallicSmoothness_1k.png"), optimize=True)


def pack_cutout(folder: Path, name: str, tag: str = "") -> None:
    stem = name + "_" + tag
    alpha = folder / (stem + "Alpha_1k.png")
    albedo = folder / (stem + "Albedo_1k.png")
    if not albedo.exists():
        albedo = albedo.with_suffix(".jpg")
    if not alpha.exists():
        return
    base = Image.open(albedo).convert("RGB")
    cutout = Image.open(alpha).convert("L")
    if base.size != cutout.size:
        raise ValueError(name + " alpha map resolution differs")
    base.putalpha(cutout)
    base.save(folder / (stem + "Base_1k.png"), optimize=True)


for folder in sorted((ROOT / "Textures").iterdir()):
    if folder.is_dir():
        pack_material(folder, folder.name)
for folder in sorted((ROOT / "Models").iterdir()):
    if folder.is_dir() and folder.name != "LOD":
        pack_material(folder, folder.name)
        pack_cutout(folder, folder.name)
        if folder.name == "Pine":
            for tag in ("Bark", "Twig"):
                pack_material(folder, folder.name, tag)
            pack_cutout(folder, folder.name, "Twig")
