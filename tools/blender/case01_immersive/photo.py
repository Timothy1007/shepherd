"""Case01 客廳家庭合照：在 Blender 裡搭一個小場景（十七號門前：磚牆、綠色前門與黃銅門牌 17、石階、人行道、盆栽），
放兩個風格化人物——母親瑪格麗特（59 歲，開襟毛衣與裙子、灰棕短髮）與女兒艾琳（28 歲，芥末色大衣、牛仔褲、深色長髮），
用 Cycles 算圖，再做舊相紙處理（褪色、偏暖、暗角、顆粒、白邊）。
同時輸出 QA 用的「異常候選」：艾琳的影像半透明、旁邊多一道錯位的殘影（存在記錄不一致的測試版本，不代表正式劇情）。
人物是風格化的人偶（Skin 修改器＋細分），臉部只有簡化的五官：近看能辨認兩個人、表情與互動，但不是寫實肖像。
"""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
P1 = os.path.join(REPO, "unity", "Assets", "Exploration", "Case01Art", "Phase1", "Resources", "Case01Phase1")


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, color, rough=0.6, metal=0.0, tex=None, scale=(1, 1), emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if tex and os.path.exists(tex):
        img = bpy.data.images.load(tex)
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = img
        mapping = nt.nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (scale[0], scale[1], 1)
        coord = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], t.inputs["Vector"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = 'RGBA'
        mix.blend_type = 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[6].default_value = (*color, 1)
        nt.links.new(t.outputs["Color"], mix.inputs[7])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return m


def add_box(name, center, size, m, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = ob.modifiers.new("b", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
    ob.data.materials.append(m)
    bpy.ops.object.shade_smooth()
    return ob


def add_plane(name, center, size, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_plane_add(size=1, location=center, rotation=[math.radians(a) for a in rot])
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = (size[0], size[1], 1)
    bpy.ops.object.transform_apply(scale=True)
    ob.data.materials.append(m)
    # UV 以公尺計（讓磚塊與石板比例正確）。
    uv = ob.data.uv_layers.active.data
    for poly in ob.data.polygons:
        for li in poly.loop_indices:
            co = ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv[li].uv = (co.x * size[0] if False else co.x, co.y)
    return ob


def ellipsoid(name, center, radii, m, segs=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=segs // 2, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = radii
    bpy.ops.object.transform_apply(scale=True)
    ob.data.materials.append(m)
    bpy.ops.object.shade_smooth()
    return ob


def skin_body(name, joints, edges, radii):
    me = bpy.data.meshes.new(name)
    me.from_pydata([Vector(j) for j in joints], edges, [])
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    sk = ob.modifiers.new("skin", 'SKIN')
    sub = ob.modifiers.new("sub", 'SUBSURF')
    sub.levels = 2
    sub.render_levels = 2
    for i, r in enumerate(radii):
        ob.data.skin_vertices[0].data[i].radius = r
    ob.data.skin_vertices[0].data[0].use_root = True
    bpy.ops.object.modifier_apply(modifier="skin")
    bpy.ops.object.modifier_apply(modifier="sub")
    bpy.ops.object.shade_smooth()
    ob.select_set(False)
    return ob


def person(name, x, y, H, yaw, top, bottom, shoes, skin, hair, skirt=False, long_hair=False, reach=None, scarf=None, coat=False, buttons=None, bob=False):
    """一個站立的人。H：身高。top／bottom／shoes／skin／hair：材質。reach：右手伸向的點（相對腳底）。"""
    s = H / 1.64
    J = {
        "pelvis": (0, 0, 0.92), "spine": (0, 0, 1.08), "chest": (0, 0.005, 1.25), "neck": (0, 0.01, 1.43),
        "lsh": (-0.17, 0, 1.36), "rsh": (0.17, 0, 1.36), "lel": (-0.195, 0.0, 1.1), "rel": (0.195, 0.03, 1.11),
        "lha": (-0.19, 0.06, 0.87), "rha": (0.18, 0.1, 0.88),
        "lhip": (-0.085, 0, 0.88), "rhip": (0.085, 0, 0.88), "lkn": (-0.09, 0.015, 0.48), "rkn": (0.08, 0.05, 0.49),
        "lan": (-0.095, 0, 0.08), "ran": (0.11, 0.02, 0.08), "lto": (-0.11, 0.13, 0.03), "rto": (0.15, 0.13, 0.03),
    }
    if reach is not None:
        # 伸出去的手（reach 的 x 為負＝左手，正＝右手）。
        side = "l" if reach[0] < 0 else "r"
        J[side + "el"] = (0.26 * (1 if side == "r" else -1), 0.12, 1.12)
        J[side + "ha"] = reach
    names = list(J.keys())
    idx = {n: i for i, n in enumerate(names)}
    E = [("pelvis", "spine"), ("spine", "chest"), ("chest", "neck"), ("chest", "lsh"), ("chest", "rsh"), ("lsh", "lel"), ("lel", "lha"),
         ("rsh", "rel"), ("rel", "rha"), ("pelvis", "lhip"), ("pelvis", "rhip"), ("lhip", "lkn"), ("lkn", "lan"), ("lan", "lto"),
         ("rhip", "rkn"), ("rkn", "ran"), ("ran", "rto")]
    R = {"pelvis": (0.15, 0.1), "spine": (0.13, 0.09), "chest": (0.155, 0.1), "neck": (0.048, 0.048), "lsh": (0.06, 0.06), "rsh": (0.06, 0.06),
         "lel": (0.045, 0.045), "rel": (0.045, 0.045), "lha": (0.036, 0.03), "rha": (0.036, 0.03), "lhip": (0.08, 0.08), "rhip": (0.08, 0.08),
         "lkn": (0.055, 0.055), "rkn": (0.055, 0.055), "lan": (0.04, 0.04), "ran": (0.04, 0.04), "lto": (0.045, 0.035), "rto": (0.045, 0.035)}
    joints = [tuple(c * s for c in J[n]) for n in names]
    body = skin_body(name + "_body", joints, [(idx[a], idx[b]) for a, b in E], [tuple(r * s for r in R[n]) for n in names])
    for m in (top, bottom, shoes, skin):
        body.data.materials.append(m)
    # 依高度分材質：鞋、下身（褲／襪）、上身（毛衣／大衣）、手。
    for poly in body.data.polygons:
        c = poly.center
        z = c.z / s
        if z < 0.11:
            poly.material_index = 2
        elif z < 0.9:
            poly.material_index = 1
        elif abs(c.x / s) > 0.18 and z < 0.93 or (reach is not None and (Vector(c) / s - Vector(reach)).length < 0.06):
            poly.material_index = 3
        else:
            poly.material_index = 0
    parts = [body]
    if skirt:
        bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=0.25 * s, radius2=0.16 * s, depth=0.42 * s, location=(0, 0, 0.71 * s))
        sk = bpy.context.active_object
        sk.data.materials.append(bottom)
        bpy.ops.object.shade_smooth()
        parts.append(sk)
    if coat:
        # 大衣下擺：從腰到大腿中段，略張開。
        bpy.ops.mesh.primitive_cone_add(vertices=32, radius1=0.2 * s, radius2=0.165 * s, depth=0.36 * s, location=(0, 0, 0.78 * s))
        ct = bpy.context.active_object
        ct.data.materials.append(top)
        bpy.ops.object.shade_smooth()
        parts.append(ct)
        add_box(name + "_belt", (0, 0, 0.96 * s), (0.31 * s, 0.21 * s, 0.035 * s), top, 0.01)
        parts.append(bpy.context.active_object)
    if buttons is not None:
        for i in range(5):
            parts.append(ellipsoid(name + "_btn", (0, 0.098 * s, (0.98 + i * 0.075) * s), (0.009 * s, 0.006 * s, 0.009 * s), buttons, 10))
        # 開襟領口。
        parts.append(add_box(name + "_collar", (0, 0.05 * s, 1.4 * s), (0.17 * s, 0.09 * s, 0.03 * s), top, 0.01))
    # 頭、頭髮、五官。
    head = ellipsoid(name + "_head", (0, 0.015 * s, 1.55 * s), (0.088 * s, 0.1 * s, 0.115 * s), skin)
    parts.append(head)
    hz = 1.575 * s
    parts.append(ellipsoid(name + "_hair", (0, -0.01 * s, hz + 0.012 * s), (0.097 * s, 0.104 * s, 0.112 * s), hair))
    for sx in (-1, 1):
        parts.append(ellipsoid(name + "_ear", (sx * 0.088 * s, 0.0, 1.55 * s), (0.014 * s, 0.022 * s, 0.03 * s), skin, 12))
    if bob:
        parts.append(ellipsoid(name + "_bob", (0, -0.02 * s, 1.535 * s), (0.106 * s, 0.1 * s, 0.085 * s), hair))
    if long_hair:
        for sx in (-1, 1):
            parts.append(ellipsoid(name + "_strand", (sx * 0.075 * s, 0.02 * s, 1.45 * s), (0.03 * s, 0.035 * s, 0.13 * s), hair))
        parts.append(ellipsoid(name + "_hairback", (0, -0.06 * s, 1.43 * s), (0.1 * s, 0.06 * s, 0.17 * s), hair))
    dark = bpy.data.materials.get("Eye") or mat("Eye", (0.04, 0.03, 0.03), 0.3)
    lip = bpy.data.materials.get("Lip") or mat("Lip", (0.45, 0.2, 0.18), 0.5)
    fy = 0.11 * s
    for sx in (-1, 1):
        parts.append(ellipsoid(name + "_eye", (sx * 0.032 * s, fy, 1.565 * s), (0.011 * s, 0.006 * s, 0.009 * s), dark, 12))
        parts.append(add_box(name + "_brow", (sx * 0.033 * s, fy - 0.004 * s, 1.588 * s), (0.03 * s, 0.006 * s, 0.006 * s), hair))
    parts.append(ellipsoid(name + "_nose", (0, fy + 0.008 * s, 1.54 * s), (0.012 * s, 0.014 * s, 0.018 * s), skin, 12))
    parts.append(add_box(name + "_mouth", (0, fy - 0.004 * s, 1.507 * s), (0.034 * s, 0.006 * s, 0.006 * s), lip))
    if scarf is not None:
        bpy.ops.mesh.primitive_torus_add(major_radius=0.06 * s, minor_radius=0.028 * s, location=(0, 0.01 * s, 1.42 * s))
        t = bpy.context.active_object
        t.data.materials.append(scarf)
        bpy.ops.object.shade_smooth()
        parts.append(t)
    for p in parts:
        p.rotation_euler.z += math.radians(yaw)
        p.location = Vector((x, y, 0)) + Vector(p.location).copy() if False else p.location
    # 整組移動與轉向：建立一個空物件當父物件。
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for p in parts:
        p.rotation_euler.z -= math.radians(yaw)
        p.parent = root
    root.location = (x, y, 0)
    # 人物模型面向本地 +Y；鏡頭在 −Y，所以轉 180°（再加上各自的偏角）。
    root.rotation_euler.z = math.radians(180 + yaw)
    return root, parts


def build_scene():
    reset()
    scn = bpy.context.scene
    brick = mat("Brick", (0.85, 0.75, 0.7), 0.85, tex=os.path.join(P1, "P1_Brick.png"), scale=(1, 1))
    pave = mat("Pave", (0.9, 0.9, 0.9), 0.9, tex=os.path.join(P1, "P1_Pavement.png"))
    stone = mat("Stone", (0.75, 0.73, 0.7), 0.8, tex=os.path.join(P1, "P1_Stone.png"))
    door = mat("Door", (0.85, 0.95, 0.85), 0.45, tex=os.path.join(P1, "P1_DoorGreen.png"))
    paint = mat("Trim", (0.9, 0.89, 0.85), 0.5)
    brass = mat("Brass", (0.78, 0.6, 0.3), 0.3, 0.9)
    glass = mat("Glass", (0.025, 0.03, 0.035), 0.22)
    soil = mat("Pot", (0.55, 0.3, 0.2), 0.7)
    leaf = mat("Leaf", (0.16, 0.3, 0.14), 0.6)

    # 磚牆（6 × 4 m），門洞處挖空用另一片蓋住：簡化成牆在後、門框與門板在前 2 cm。
    wall = add_box("Wall", (0, 2.1, 2.0), (6.0, 0.2, 4.0), brick)
    for poly in wall.data.polygons:
        pass
    _uv_box(wall, 1.0)
    # 門框、門板、門楣窗、門牌。
    add_box("DoorFrame", (0.45, 1.97, 1.15), (1.12, 0.08, 2.3), paint, 0.01)
    d = add_box("DoorLeaf", (0.45, 1.93, 1.08), (0.92, 0.05, 2.08), door, 0.006)
    _uv_box(d, 1.0)
    for zz in (0.55, 1.45):
        for xx in (0.25, 0.65):
            add_box("Panel", (xx, 1.9, zz), (0.3, 0.02, 0.62 if zz < 1 else 0.7), door, 0.01)
    # 門楣窗：嵌在門框裡（不凸出），四格玻璃。
    add_box("FrameTop", (0.45, 1.97, 2.42), (1.12, 0.08, 0.3), paint, 0.01)
    add_box("Fanlight", (0.45, 1.93, 2.42), (0.9, 0.02, 0.22), glass)
    for xx in (0.225, 0.45, 0.675):
        add_box("FanBar", (xx, 1.915, 2.42), (0.025, 0.02, 0.22), paint)
    add_box("Knob", (0.82, 1.89, 1.05), (0.05, 0.04, 0.05), brass, 0.02)
    add_box("Letterbox", (0.45, 1.89, 1.0), (0.26, 0.02, 0.06), brass, 0.005)
    cu = bpy.data.curves.new("num", 'FONT')
    cu.body = "17"
    cu.size = 0.17
    cu.extrude = 0.006
    cu.align_x = 'CENTER'
    num = bpy.data.objects.new("Number", cu)
    bpy.context.collection.objects.link(num)
    num.location = (0.45, 1.885, 1.62)
    num.rotation_euler = (math.radians(90), 0, 0)
    num.data.materials.append(brass)
    # 左側的上下拉窗：外框、上下兩扇、每扇 2×3 格玻璃，裡面掛著淺色窗簾。
    add_box("WinReveal", (-1.55, 2.0, 1.45), (1.08, 0.06, 1.52), mat("Reveal", (0.2, 0.17, 0.15), 0.9))
    curtain = mat("Curtain", (0.72, 0.66, 0.55), 0.95)
    add_box("CurtainL", (-1.88, 1.99, 1.45), (0.36, 0.02, 1.4), curtain)
    add_box("CurtainR", (-1.22, 1.99, 1.45), (0.3, 0.02, 1.4), curtain)
    add_box("WinGlass", (-1.55, 1.975, 1.45), (0.96, 0.01, 1.4), glass)
    for zc in (1.1, 1.8):
        add_box("Sash", (-1.55, 1.955, zc), (1.0, 0.035, 0.05), paint)
        for k in (-1, 0, 1):
            add_box("BarV", (-1.55 + k * 0.17 if k else -1.55, 1.955, zc), (0.02, 0.03, 0.68), paint) if k else None
        add_box("BarH", (-1.55, 1.955, zc + (0.17 if zc > 1.5 else -0.17)), (0.96, 0.03, 0.02), paint)
    add_box("SashMeet", (-1.55, 1.95, 1.45), (1.0, 0.05, 0.06), paint)
    for xx in (-2.07, -1.03):
        add_box("WinJamb", (xx, 1.96, 1.45), (0.06, 0.06, 1.5), paint)
    add_box("WinHead", (-1.55, 1.96, 2.2), (1.1, 0.07, 0.07), paint)
    add_box("Sill", (-1.55, 1.9, 0.66), (1.25, 0.16, 0.06), stone, 0.01)
    # 石階與人行道。
    st = add_box("Step", (0.45, 1.72, 0.08), (1.4, 0.42, 0.16), stone, 0.01)
    _uv_box(st, 1.0)
    g = add_box("Pavement", (0, -0.5, -0.02), (8.0, 6.0, 0.04), pave)
    _uv_box(g, 1.2)
    add_box("Kerb", (0, -2.6, 0.02), (8.0, 0.25, 0.12), stone, 0.01)
    # 門邊的盆栽。
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.17, depth=0.34, location=(1.25, 1.75, 0.17))
    bpy.context.active_object.data.materials.append(soil)
    ellipsoid("Shrub", (1.25, 1.75, 0.55), (0.24, 0.24, 0.3), leaf)

    # 人物：母親在左（矮一些、手輕放在女兒手臂上），女兒在右。
    knit = mat("Cardigan", (0.42, 0.18, 0.2), 0.9)
    skirt = mat("Skirt", (0.22, 0.22, 0.28), 0.85)
    tights = mat("Tights", (0.16, 0.13, 0.13), 0.7)
    shoes = mat("Shoes", (0.12, 0.08, 0.06), 0.4)
    skin = mat("Skin", (0.78, 0.6, 0.5), 0.55)
    grey = mat("HairGrey", (0.36, 0.31, 0.27), 0.6)
    coat = mat("Coat", (0.62, 0.45, 0.16), 0.85)
    jeans = mat("Jeans", (0.13, 0.17, 0.27), 0.85)
    boots = mat("Boots", (0.2, 0.13, 0.08), 0.45)
    darkhair = mat("HairDark", (0.08, 0.06, 0.05), 0.5)
    scarf = mat("Scarf", (0.55, 0.52, 0.48), 0.9)
    mom, mom_parts = person("Margaret", 0.12, 1.15, 1.6, 12, knit, skirt, shoes, skin, grey, skirt=True, reach=(-0.36, 0.1, 1.05), buttons=brass, bob=True)
    for p in mom_parts:
        if p.name.endswith("_body"):
            # 裙子下的腿用褲襪材質。
            p.data.materials[1] = tights
    elle, elle_parts = person("Eleanor", 0.66, 1.08, 1.67, -10, coat, jeans, boots, skin, darkhair, long_hair=True, scarf=scarf, coat=True)

    # 光：陰天的天空＋偏暖的柔和陽光。
    world = bpy.data.worlds.new("Sky")
    scn.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.68, 0.76, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(52), math.radians(8), math.radians(-38))
    sun.data.energy = 3.4
    sun.data.angle = math.radians(14)
    sun.data.color = (1.0, 0.92, 0.82)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    bpy.context.collection.objects.link(cam)
    cam.location = (0.38, -2.75, 1.32)
    cam.data.lens = 38
    target = Vector((0.38, 1.3, 1.08))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.camera = cam
    return elle_parts


def _uv_box(ob, meters):
    me = ob.data
    if not me.uv_layers:
        me.uv_layers.new()
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = ob.matrix_world @ me.vertices[me.loops[li].vertex_index].co
            if ax == 0:
                uv[li].uv = (co.y / meters, co.z / meters)
            elif ax == 1:
                uv[li].uv = (co.x / meters, co.z / meters)
            else:
                uv[li].uv = (co.x / meters, co.y / meters)


def render(path, w, h, samples=64):
    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'
    scn.cycles.samples = samples
    try:
        scn.cycles.use_denoising = True
    except Exception:
        pass
    scn.render.resolution_x = w
    scn.render.resolution_y = h
    scn.render.resolution_percentage = 100
    scn.render.image_settings.file_format = 'PNG'
    try:
        scn.view_settings.view_transform = 'AgX'
    except Exception:
        pass
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]
    bpy.data.images.remove(img)
    return a


def age(rgb, seed):
    rng = np.random.default_rng(seed)
    h, w = rgb.shape[:2]
    g = rgb @ np.array([0.3, 0.59, 0.11])
    out = rgb * 0.72 + g[..., None] * 0.28
    out = out * np.array([1.06, 1.0, 0.86]) + np.array([0.035, 0.03, 0.02])
    out = 0.05 + out * 0.9
    y, x = np.mgrid[0:h, 0:w]
    d = ((x / w - 0.5) ** 2 + (y / h - 0.5) ** 2)
    out *= (1 - 0.55 * np.clip(d * 1.9, 0, 1))[..., None]
    out += rng.normal(0, 0.018, (h, w))[..., None]
    for _ in range(14):
        cx, cy, r = rng.integers(0, w), rng.integers(0, h), rng.integers(1, 3)
        out[max(0, cy - r):cy + r, max(0, cx - r):cx + r] += 0.12
    return np.clip(out, 0, 1)


def bordered(img, W, H, b, seed):
    out = np.ones((H, W, 3)) * np.array([0.95, 0.93, 0.87])
    rng = np.random.default_rng(seed)
    out += rng.normal(0, 0.01, (H, W))[..., None]
    out[b:b + img.shape[0], b:b + img.shape[1]] = img
    return np.clip(out, 0, 1)


def save(rgb, path):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h)
    px = np.concatenate([rgb, np.ones((h, w, 1))], axis=2)[::-1]
    img.pixels.foreach_set(px.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def build(out_dir, tmp_dir):
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(tmp_dir, exist_ok=True)
    W, H, B = 2048, 1536, 72
    iw, ih = W - 2 * B, H - 2 * B
    elle = build_scene()
    full = os.path.join(tmp_dir, "photo_full.png")
    render(full, iw, ih)
    for p in elle:
        p.hide_render = True
    plate = os.path.join(tmp_dir, "photo_plate.png")
    render(plate, iw, ih)
    a = load(full)
    b = load(plate)
    normal = bordered(age(a, 3), W, H, B, 5)
    save(normal, os.path.join(out_dir, "IH_FamilyPhoto.png"))
    # 異常候選：艾琳半透明＋錯位殘影（不是正式劇情真相）。
    diff = a - b
    ghost = b + diff * 0.3
    shift = np.roll(diff, 46, axis=1) * 0.16
    mask = (np.abs(np.roll(diff, 46, axis=1)).sum(axis=2) > 0.02)[..., None]
    ghost = ghost + shift * mask
    anomaly = bordered(age(np.clip(ghost, 0, 1), 3), W, H, B, 5)
    save(anomaly, os.path.join(out_dir, "IH_FamilyPhotoAnomaly.png"))
    print("[Case01Immersive] photo ->", out_dir)
