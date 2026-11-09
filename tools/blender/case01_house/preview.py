"""預覽已匯出的 FBX（套上兩個資料夾的貼圖），每個模型一張 Workbench 圖。
blender -b --factory-startup --python preview.py -- <FBX 資料夾> <客廳貼圖資料夾> <輸出資料夾> 名稱1 名稱2 ...
"""
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
fbx_dir, living_dir, out_dir = argv[0], argv[1], argv[2]
names = argv[3:]
os.makedirs(out_dir, exist_ok=True)


def tex_for(name):
    for d in (fbx_dir, living_dir):
        p = os.path.join(d, name + ".png")
        if os.path.exists(p):
            return p
    return None


for n in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(fbx_dir, n + ".fbx"))
    for m in bpy.data.materials:
        m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        p = tex_for(m.name.split(".")[0])
        if p and bsdf:
            t = nt.nodes.new("ShaderNodeTexImage")
            t.image = bpy.data.images.load(p, check_existing=True)
            nt.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
            nt.nodes.active = t
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.display.shading.show_cavity = True
    sc.render.resolution_x, sc.render.resolution_y = 800, 620
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mins = Vector(map(min, mins, w))
            maxs = Vector(map(max, maxs, w))
    c = (mins + maxs) / 2
    size = (maxs - mins).length
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    # 匯入後 Blender 的前方是 -Y（FBX 軸轉換）；從前方偏右上看。
    d = Vector((0.55, -1.0, 0.45)).normalized() * size * 1.35
    cam.location = c + d
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out_dir, n + ".png")
    bpy.ops.render.render(write_still=True)
    print("[preview]", n, tuple(round(x, 2) for x in (maxs - mins)))
