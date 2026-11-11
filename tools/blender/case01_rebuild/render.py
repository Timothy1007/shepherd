"""Case01 全屋重製 v2：Blender 參考算圖（與 Unity 同機位比對用）與暫用家庭合照。

- reference(out, tex_dir)：在 room.build() 之後呼叫，把 RB_* 材質接上 pbr.py 產生的貼圖，依空物件放燈，從 E_CamDoor 等機位用 Cycles 算圖。
- photo(out)：暫用合照——海邊遠景，母女從背後、手牽手走在潮線邊（人物小、背影），舊相紙處理。
  這不是正式美術資產；正式版需要插畫或攝影授權（見 CASE01_HOUSE_REBUILD_V2_PLAN.md）。
"""
import math
import os
import importlib.util

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))


def _principled(m):
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs[0])
    return nt, b


def _tex(nt, path, non_color=False):
    if not os.path.exists(path):
        return None
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(path, check_existing=True)
    if non_color:
        t.image.colorspace_settings.name = 'Non-Color'
    return t


def setup_materials(tex_dir, photo_path):
    for m in bpy.data.materials:
        n = m.name
        if not n.startswith("RB_"):
            continue
        nt, b = _principled(m)
        special = {"RB_Glass": ((0.6, 0.65, 0.7), 0.02), "RB_Mirror": ((0.85, 0.86, 0.88), 0.02), "RB_Screen": ((0.01, 0.012, 0.015), 0.08),
                   "RB_Plastic": ((0.04, 0.04, 0.045), 0.35), "RB_Paper": ((0.85, 0.83, 0.76), 0.8)}
        if n in special:
            c, r = special[n]
            b.inputs["Base Color"].default_value = (*c, 1)
            b.inputs["Roughness"].default_value = r
            if n == "RB_Mirror":
                b.inputs["Metallic"].default_value = 1.0
            if n == "RB_Glass":
                b.inputs["Transmission Weight"].default_value = 0.9
            continue
        if n == "RB_Coal":
            b.inputs["Base Color"].default_value = (0.05, 0.03, 0.02, 1)
            b.inputs["Emission Color"].default_value = (1.0, 0.35, 0.08, 1)
            b.inputs["Emission Strength"].default_value = 0.6
            continue
        if n == "RB_Photo":
            t = _tex(nt, photo_path)
            if t:
                nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
            b.inputs["Roughness"].default_value = 0.35
            continue
        a = _tex(nt, os.path.join(tex_dir, n + "_A.jpg"))
        if a:
            nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
        nm = _tex(nt, os.path.join(tex_dir, n + "_N.png"), True)
        if nm:
            nmap = nt.nodes.new("ShaderNodeNormalMap")
            nt.links.new(nm.outputs["Color"], nmap.inputs["Color"])
            nt.links.new(nmap.outputs["Normal"], b.inputs["Normal"])
        ms = _tex(nt, os.path.join(tex_dir, n + "_MS.png"), True)
        if ms:
            inv = nt.nodes.new("ShaderNodeMath")
            inv.operation = 'SUBTRACT'
            inv.inputs[0].default_value = 1.0
            nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
            nt.links.new(inv.outputs[0], b.inputs["Roughness"])
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            nt.links.new(ms.outputs["Color"], sep.inputs[0])
            nt.links.new(sep.outputs[0], b.inputs["Metallic"])


def _light(name, kind, loc, energy, color, size=0.1, rot=None, spot=None):
    l = bpy.data.lights.new(name, kind)
    l.energy = energy
    l.color = color
    if kind in ('POINT', 'SPOT'):
        l.shadow_soft_size = size
    if spot:
        l.spot_size = math.radians(spot)
        l.spot_blend = 0.6
    o = bpy.data.objects.new(name, l)
    o.location = loc
    if rot:
        o.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.collection.objects.link(o)
    return o


def setup_lights(ambient=True, lamps=True):
    ob = {o.name: o for o in bpy.data.objects}
    scn = bpy.context.scene
    w = bpy.data.worlds.new("Night")
    scn.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.065, 0.1, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.6 if ambient else 0.05
    if lamps:
        _light("TableLamp", 'POINT', ob["E_TableLamp"].location, 55, (1.0, 0.72, 0.45), 0.12)
        _light("FloorLamp", 'POINT', ob["E_FloorLamp"].location, 60, (1.0, 0.75, 0.5), 0.15)
        _light("Fire", 'POINT', ob["E_FireLight"].location, 18, (1.0, 0.45, 0.15), 0.2)
        _light("HallLamp", 'POINT', ob["E_HallLamp"].location, 30, (1.0, 0.8, 0.6), 0.1)
    if ambient:
        _light("Street", 'SPOT', ob["E_StreetLamp"].location, 450, (1.0, 0.68, 0.38), 0.3, rot=(70, 0, 0), spot=60)
        _light("Moon", 'SUN', ob["E_Moon"].location, 0.25, (0.6, 0.7, 1.0), rot=(55, 0, 15))


def camera(name, lens=None, fov_v=55.0):
    ob = {o.name: o for o in bpy.data.objects}
    e = ob[name]
    cam = bpy.data.objects.new("Cam_" + name, bpy.data.cameras.new("Cam_" + name))
    bpy.context.collection.objects.link(cam)
    cam.location = e.location
    target = Vector((0.6, 2.1, 1.2)) if name != "E_CamFire" else Vector((3.4, 3.6, 1.1))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(fov_v)
    bpy.context.scene.camera = cam
    return cam


def render(path, w=1280, h=720, samples=128, exposure=0.6):
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'
    scn.cycles.samples = samples
    scn.cycles.use_denoising = True
    scn.render.resolution_x = w
    scn.render.resolution_y = h
    scn.render.image_settings.file_format = 'PNG'
    try:
        scn.view_settings.view_transform = 'AgX'
    except Exception:
        pass
    scn.view_settings.exposure = exposure
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)


def reference(out, tex_dir, photo_path):
    os.makedirs(out, exist_ok=True)
    setup_materials(tex_dir, photo_path)
    setup_lights()
    camera("E_CamDoor")
    render(os.path.join(out, "blender_ref_door.png"))
    camera("E_CamFire")
    render(os.path.join(out, "blender_ref_fire.png"))
    print("[Case01Rebuild] reference renders ->", out)


# ---------------------------------------------------------------- 暫用合照

def photo(out_path):
    spec = importlib.util.spec_from_file_location("ih_photo", os.path.join(HERE, "..", "case01_immersive", "photo.py"))
    ph = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(ph)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    sand = ph.mat("Sand", (0.72, 0.64, 0.5), 0.95)
    wet = ph.mat("WetSand", (0.42, 0.38, 0.32), 0.25)
    sea = ph.mat("Sea", (0.12, 0.2, 0.24), 0.08)
    foam = ph.mat("Foam", (0.85, 0.86, 0.84), 0.6)
    rock = ph.mat("Headland", (0.28, 0.27, 0.25), 0.9)
    grass = ph.mat("Grass", (0.24, 0.3, 0.16), 0.9)
    # 沙灘（往遠處漸濕）、海、浪花線、遠處岬角。
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 10, 0))
    g = bpy.context.active_object
    g.scale = (60, 40, 1)
    g.data.materials.append(sand)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 24, 0.004))
    w = bpy.context.active_object
    w.scale = (60, 8, 1)
    w.data.materials.append(wet)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 70, 0.01))
    s = bpy.context.active_object
    s.scale = (200, 90, 1)
    s.data.materials.append(sea)
    for i, y in enumerate((27.5, 29.0, 31.5)):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, y, 0.015))
        f = bpy.context.active_object
        f.scale = (80, 0.25 + i * 0.1, 0.01)
        f.data.materials.append(foam)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=(-55, 120, -6))
    h = bpy.context.active_object
    h.scale = (45, 18, 16)
    h.data.materials.append(rock)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=(-55, 120, 2))
    h2 = bpy.context.active_object
    h2.scale = (44, 17, 9)
    h2.data.materials.append(grass)
    # 人物（背影、手牽手、走向海）。
    knit = ph.mat("Cardigan", (0.42, 0.2, 0.2), 0.9)
    skirt = ph.mat("Skirt", (0.24, 0.24, 0.3), 0.85)
    tights = ph.mat("Tights", (0.16, 0.13, 0.13), 0.7)
    shoes = ph.mat("Shoes", (0.12, 0.08, 0.06), 0.4)
    skin = ph.mat("Skin", (0.78, 0.6, 0.5), 0.55)
    grey = ph.mat("HairGrey", (0.38, 0.33, 0.29), 0.6)
    coat = ph.mat("Coat", (0.62, 0.45, 0.16), 0.85)
    jeans = ph.mat("Jeans", (0.13, 0.17, 0.27), 0.85)
    boots = ph.mat("Boots", (0.2, 0.13, 0.08), 0.45)
    dark = ph.mat("HairDark", (0.08, 0.06, 0.05), 0.5)
    scarf = ph.mat("Scarf", (0.55, 0.52, 0.48), 0.9)
    # person() 讓人物面向 −Y（鏡頭）；這裡要背影，所以多轉 180°。
    m, mp = ph.person("Margaret", -0.35, 21.0, 1.6, 180 + 8, knit, skirt, shoes, skin, grey, skirt=True, reach=(0.36, 0.1, 1.05), buttons=None, bob=True)
    for p in mp:
        if p.name.endswith("_body"):
            p.data.materials[1] = tights
    e, ep = ph.person("Eleanor", 0.32, 21.15, 1.67, 180 - 6, coat, jeans, boots, skin, dark, long_hair=True, scarf=scarf, coat=True)
    # 光：傍晚、低角度的暖陽，雲層讓天空有層次。
    world = bpy.data.worlds.new("Sky")
    scn.world = world
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    sky = nt.nodes.new("ShaderNodeTexSky")
    try:
        sky.sky_type = 'NISHITA'
        sky.sun_elevation = math.radians(9)
        sky.sun_rotation = math.radians(200)
        sky.air_density = 1.4
        sky.dust_density = 3.0
    except Exception:
        pass
    nt.links.new(sky.outputs[0], bg.inputs[0])
    bg.inputs[1].default_value = 0.22
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(80), 0, math.radians(200))
    sun.data.energy = 1.6
    sun.data.angle = math.radians(3)
    sun.data.color = (1.0, 0.8, 0.6)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    cam.location = (1.4, 5.0, 1.45)
    cam.data.lens = 70
    cam.data.dof.use_dof = True
    cam.data.dof.focus_distance = 16.1
    cam.data.dof.aperture_fstop = 4.0
    t = Vector((0.0, 21.1, 1.0))
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.camera = cam
    Wd, Hd, B = 2048, 1536, 72
    tmp = os.path.join(os.path.dirname(out_path), "_photo_raw.png")
    render(tmp, Wd - 2 * B, Hd - 2 * B, samples=96, exposure=-1.3)
    a = ph.load(tmp)
    os.remove(tmp)
    ph.save(ph.bordered(ph.age(a, 7), Wd, Hd, B, 9), out_path)
    print("[Case01Rebuild] photo ->", out_path)
