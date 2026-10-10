"""Derive capped game meshes from checked Poly Haven source FBX files.

blender -b -t 2 --python Tools/build_free_reference_lods.py -- CACHE_DIR
The original high density FBX remains outside Assets; output contains UV and
material slots for the Unity URP prefab builder. Review silhouettes in Unity.
"""

import os
import sys

import bpy


OUTPUT = "Assets/Game/Art/FreeReference/Models"
SOURCES = (
    ("BermudaGrass", (550, 250, 80)),
    ("WeatheredRock", (3500, 900, 200)),
    ("CeramicPot", (3600,)),
)


def convert(cache: str, name: str, targets: tuple) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.join(cache, name + "_source.fbx"))
    meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
    if not meshes:
        raise RuntimeError("Source has no mesh: " + name)
    face_count = sum(len(item.data.polygons) for item in meshes)
    print(name, "source polygons", face_count, flush=True)
    for level, target in enumerate(targets):
        copies = []
        for original in meshes:
            item = original.copy()
            item.data = original.data.copy()
            world = original.matrix_world.copy()
            bpy.context.collection.objects.link(item)
            item.parent = None
            item.matrix_world = world
            item.name = name + "_LOD" + str(level) + "_" + original.name
            if target < face_count and len(item.data.polygons) > 8:
                modifier = item.modifiers.new("Capped game LOD", "DECIMATE")
                modifier.ratio = max(0.001, min(1.0, target / face_count))
                bpy.ops.object.select_all(action="DESELECT")
                item.select_set(True)
                bpy.context.view_layer.objects.active = item
                bpy.ops.object.modifier_apply(modifier=modifier.name)
            if not item.data.uv_layers:
                raise RuntimeError("Source UV missing: " + name + " " + item.name)
            copies.append(item)
        bpy.ops.object.select_all(action="DESELECT")
        for item in copies:
            item.select_set(True)
        bpy.context.view_layer.objects.active = copies[0]
        filename = name + ("_LOD" + str(level) if len(targets) > 1 else "") + ".fbx"
        path = os.path.join(OUTPUT, filename)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                                 axis_forward="-Z", axis_up="Y", path_mode="STRIP")
        print(path, "polygons", sum(len(item.data.polygons) for item in copies), flush=True)
        for item in copies:
            bpy.data.objects.remove(item, do_unlink=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) != 1:
        raise SystemExit("Expected CACHE_DIR")
    os.makedirs(OUTPUT, exist_ok=True)
    for source, levels in SOURCES:
        convert(args[0], source, levels)
