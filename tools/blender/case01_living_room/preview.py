"""預覽：用 Blender Workbench 把每件模型（套上產生的貼圖）各拍一張圖，檢查形體。
blender -b --factory-startup --python preview.py -- <貼圖資料夾> <輸出資料夾>
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import models  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
tex_dir, out_dir = argv[0], argv[1]
os.makedirs(out_dir, exist_ok=True)

ITEMS = [
    ("sofa", lambda: [models.sofa(2, 1.9, "Sofa"), models.throw_blanket()]),
    ("armchair", lambda: [models.sofa(1, 0.95, "Armchair")]),
    ("coffee_table", lambda: [models.coffee_table()]),
    ("bookcase", lambda: [models.bookcase()]),
    ("window_curtains", lambda: [models.sash_window()] + models.curtains()),
    ("lamp_sidetable", lambda: models.table_lamp() + [models.side_table()]),
    ("mantel", lambda: models.mantelpiece()),
    ("tv", lambda: models.tv_unit()),
    ("rug", lambda: [models.rug()]),
    ("plant_props", lambda: models.plant() + models.props()),
]


def setup_material(m):
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    path = os.path.join(tex_dir, m.name + ".png")
    if not os.path.exists(path) and m.name == "LR_Pages":
        path = os.path.join(tex_dir, "LR_Books.png")
    if os.path.exists(path):
        img = bpy.data.images.load(path, check_existing=True)
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = img
        nt.links.new(n.outputs["Color"], bsdf.inputs["Base Color"])
        nt.nodes.active = n


for name, make in ITEMS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = make()
    if name == "window_curtains":
        for o in objs:
            if o.name.startswith("Sash"):
                o.location.z = 1.5
    for m in bpy.data.materials:
        setup_material(m)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.display.shading.show_cavity = True
    sc.display.shading.show_shadows = True
    sc.render.resolution_x, sc.render.resolution_y = 900, 700
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mins = Vector(map(min, mins, w))
            maxs = Vector(map(max, maxs, w))
    c = (mins + maxs) / 2
    size = (maxs - mins).length
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    # 從前方（+Y）偏右上看。
    d = Vector((0.55, 1.0, 0.45)).normalized() * size * 1.35
    cam.location = c + d
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.lens = 45
    sc.render.filepath = os.path.join(out_dir, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("[preview]", name, "verts", sum(len(o.data.vertices) for o in objs), "size", tuple(round(x, 2) for x in (maxs - mins)))
