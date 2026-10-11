"""Make UV preserving capped LOD meshes from the verified source FBX."""
import os
import sys
import csv
import hashlib
import bpy

cache = sys.argv[sys.argv.index("--") + 1]
source = os.path.join(cache, "CoastalTree_source.fbx")
output = "Assets/Game/Art/GrandVillage/Models"
os.makedirs(output, exist_ok=True)
manifest_path = "Docs/GRAND_VILLAGE_ASSETS.csv"
with open(manifest_path, newline="") as manifest:
    rows = [row for row in csv.reader(manifest) if not row[0].endswith(".fbx")]
with open(manifest_path, "w", newline="") as manifest:
    csv.writer(manifest, lineterminator="\n").writerows(rows)
bpy.ops.import_scene.fbx(filepath=source)
originals = [obj for obj in bpy.context.scene.objects
             if obj.type == "MESH" and not obj.name.startswith("Cube")]
if not originals or any(not obj.data.uv_layers for obj in originals):
    raise RuntimeError("Coastal tree source has missing meshes or UVs")
count = sum(len(obj.data.polygons) for obj in originals)
print("Coastal tree source polygons:", count, flush=True)
for level, cap in enumerate((6000, 2000, 300)):
    copies = []
    for original in originals:
        item = original.copy()
        item.data = original.data.copy()
        matrix = original.matrix_world.copy()
        bpy.context.collection.objects.link(item)
        item.parent = None
        item.matrix_world = matrix
        item.name = "CoastalTree_LOD%d_%s" % (level, original.name)
        if count > cap and len(item.data.polygons) > 8:
            decimate = item.modifiers.new("Capped LOD", "DECIMATE")
            decimate.ratio = max(0.001, min(1.0, cap/count))
            bpy.ops.object.select_all(action="DESELECT")
            item.select_set(True)
            bpy.context.view_layer.objects.active = item
            bpy.ops.object.modifier_apply(modifier=decimate.name)
        copies.append(item)
    bpy.ops.object.select_all(action="DESELECT")
    for item in copies:
        item.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    path = os.path.join(output, "CoastalTree_LOD%d.fbx" % level)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                             axis_forward="-Z", axis_up="Y", path_mode="STRIP")
    triangles = sum(len(x.data.polygons) for x in copies)
    print(path, "polygons", triangles, flush=True)
    with open(manifest_path, "a", newline="") as manifest:
        csv.writer(manifest, lineterminator="\n").writerow((path, "derived from https://polyhaven.com/a/island_tree_02",
            "CC0 https://polyhaven.com/license", "LOD%d %d polygons" % (level, triangles), "",
            hashlib.sha256(open(path,"rb").read()).hexdigest()))
    for item in copies:
        bpy.data.objects.remove(item, do_unlink=True)
