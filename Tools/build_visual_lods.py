"""Run with Blender: blender -b -t 2 --python Tools/build_visual_lods.py -- SOURCE_DIR OUTPUT_DIR.

Source FBX files are CC0 assets from Poly Haven, identified in Docs/PVT_VISUAL_ASSETS.md.
The derived FBX files are committed so Unity users do not need Blender.
"""

import os
import sys

import bpy


def export_lods(source_dir, output_dir, source_name, object_prefix, asset_name, ratios):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.join(source_dir, source_name + "_source.fbx"))
    sources = [obj for obj in bpy.context.scene.objects
               if obj.type == "MESH" and obj.name.startswith(object_prefix)]
    if not sources:
        raise RuntimeError("No mesh found for " + source_name)
    source = sorted(sources, key=lambda obj: obj.name)[0]
    for level, ratio in enumerate(ratios):
        obj = source.copy()
        obj.data = source.data.copy()
        bpy.context.collection.objects.link(obj)
        obj.name = asset_name + "_LOD" + str(level)
        modifier = obj.modifiers.new("Visual LOD", "DECIMATE")
        modifier.ratio = ratio
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        path = os.path.join(output_dir, asset_name + "_LOD" + str(level) + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                                 axis_forward="-Z", axis_up="Y", path_mode="STRIP")
        print(asset_name, level, len(obj.data.polygons), path, flush=True)
        bpy.data.objects.remove(obj, do_unlink=True)


def export_prop(source_dir, output_dir, source_name, asset_name):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=os.path.join(source_dir, source_name + "_source.fbx"))
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("No mesh found for " + source_name)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    path = os.path.join(output_dir, asset_name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             axis_forward="-Z", axis_up="Y", path_mode="STRIP")
    print(asset_name, sum(len(obj.data.polygons) for obj in meshes), path, flush=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) != 2:
        raise SystemExit("Expected SOURCE_DIR OUTPUT_DIR")
    os.makedirs(args[1], exist_ok=True)
    export_lods(args[0], args[1], "pine_sapling_small", "pine_sapling_small_a",
                "Pine", (0.10, 0.025, 0.006))
    export_lods(args[0], args[1], "shrub_03", "shrub_03_a", "Shrub",
                (1.0, 0.35, 0.10))
    export_prop(args[0], args[1], "large_castle_door", "CastleDoor")
    export_prop(args[0], args[1], "large_iron_gate", "IronGate")
    export_prop(args[0], args[1], "gothic_statue", "GothicStatue")
