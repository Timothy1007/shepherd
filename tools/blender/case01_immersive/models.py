"""Case01 沉浸式住宅 QA：生活痕跡小物件與燈具（Blender bpy＋bmesh 程序建模，匯出 FBX 給 Unity）。

沿用 case01_living_room/models.py 的工具與慣例：單位公尺；原點在地面（或掛牆物背面）中心；正面朝 Blender +Y（Unity +Z）。
材質只命名：沿用 LR_*／P1_* 的既有材質（Unity 端 Case01ArtKit 對應），新增的 IH_* 由 Case01ImmersiveHouseQA 指定。
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
import importlib.util  # noqa: E402

from mathutils import Vector  # noqa: E402

_spec = importlib.util.spec_from_file_location("lr_models", os.path.join(HERE, "..", "case01_living_room", "models.py"))
lr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lr)
Part, box, rounded_box, lathe, grid_sheet, finish, export = lr.Part, lr.box, lr.rounded_box, lr.lathe, lr.grid_sheet, lr.finish, lr.export

for k, v in {"IH_Paper": 0.3, "IH_Rubber": 0.3, "IH_Kraft": 0.3, "IH_Lens": 1.0, "IH_Umbrella": 0.5, "IH_Wool": 0.4, "IH_Newsprint": 0.4, "IH_Suede": 0.3}.items():
    lr.TEX_METERS[k] = v


def torus(part, center, R, r, mat, steps=20, tube=8, axis='y', arc=1.0, smooth=True, start=0.0):
    """圓環（杯把、鑰匙圈）。axis：環的法線軸；arc：0～1 只做一段。"""
    bm = part.bm
    rings = []
    n = steps if arc >= 1.0 else steps + 1
    for i in range(n):
        a = math.radians(start) + 2 * math.pi * arc * i / steps
        ring = []
        for j in range(tube):
            b = 2 * math.pi * j / tube
            rr = R + r * math.cos(b)
            p = Vector((rr * math.cos(a), rr * math.sin(a), r * math.sin(b)))
            if axis == 'y':
                p = Vector((p.x, p.z, p.y))
            elif axis == 'x':
                p = Vector((p.z, p.x, p.y))
            ring.append(bm.verts.new(Vector(center) + p))
        rings.append(ring)
    faces = []
    for i in range(n if arc >= 1.0 else n - 1):
        a, b2 = rings[i], rings[(i + 1) % n]
        for j in range(tube):
            k = (j + 1) % tube
            faces.append(bm.faces.new((a[j], a[k], b2[k], b2[j])))
    lr._set_faces(part, faces, mat, smooth)


def wall_sconce():
    """壁燈：黃銅圓背板、彎臂、布燈罩（開口朝上），燈泡位置在燈罩內（Unity 端放點光源）。原點＝背板背面中心。"""
    p = Part("WallSconce")
    B = "LR_Brass"
    lathe(p, (0, 0, 0), [(0.0, 0.0), (0.06, 0.0), (0.065, 0.008), (0.058, 0.018), (0.0, 0.02)], B, steps=24)
    for v in p.bm.verts:
        y, z = v.co.y, v.co.z
        v.co.y, v.co.z = z, y
    # 彎臂：從背板往前、再往上（一串小圓柱段）。
    pts = [(0, 0.02, 0.0), (0, 0.07, -0.01), (0, 0.12, 0.0), (0, 0.15, 0.04), (0, 0.16, 0.09)]
    for a, b in zip(pts, pts[1:]):
        mid = Vector(a).lerp(Vector(b), 0.5)
        d = Vector(b) - Vector(a)
        ang = math.degrees(math.atan2(d.y, d.z))
        tmp_part = Part("_t")
        lathe(tmp_part, (0, 0, -d.length / 2), [(0.009, 0), (0.009, d.length)], B, steps=10)
        # 直接搬進主件：手動旋轉。
        import bmesh
        from mathutils import Euler
        bmesh.ops.rotate(tmp_part.bm, verts=list(tmp_part.bm.verts), cent=Vector((0, 0, 0)), matrix=Euler((math.radians(-ang + 0), 0, 0)).to_matrix())
        bmesh.ops.translate(tmp_part.bm, verts=list(tmp_part.bm.verts), vec=mid)
        lr._merge(p, tmp_part.bm, B, True)
    # 燈座杯與燈泡頸。
    lathe(p, (0, 0.16, 0.09), [(0.012, 0), (0.026, 0.01), (0.026, 0.035), (0.02, 0.045)], B, steps=16)
    lathe(p, (0, 0.16, 0.135), [(0.012, 0), (0.024, 0.02), (0.026, 0.04), (0.018, 0.055), (0.0, 0.062)], "IH_Lens", steps=14)
    # 燈罩：截錐、開口朝上，帶厚度。
    lathe(p, (0, 0.16, 0.1), [(0.05, 0.0), (0.085, 0.13), (0.081, 0.132), (0.047, 0.004)], "LR_Shade", steps=28, cap_top=False, cap_bottom=False)
    return finish(p)


def flashlight():
    """手持手電筒（舊式金屬筒身、橡膠握紋、玻璃鏡片）。原點＝筒身中心，鏡片朝 +Y。"""
    p = Part("Flashlight")
    L = 0.2
    prof = [(0.0, 0.0), (0.016, 0.0), (0.017, 0.006)]
    # 筒身（尾蓋→握把→頸→燈頭）。
    prof += [(0.0165, 0.02), (0.0165, 0.11), (0.018, 0.118), (0.0225, 0.15), (0.0245, 0.17), (0.0245, 0.195), (0.022, 0.2)]
    faces = lathe(p, (0, 0, 0), prof, "P1_Steel", steps=24, cap_top=False)
    # 握把的橡膠環。
    for i in range(7):
        z = 0.03 + i * 0.011
        lathe(p, (0, 0, z), [(0.0166, 0.0), (0.0178, 0.002), (0.0178, 0.0055), (0.0166, 0.0075)], "IH_Rubber", steps=24, cap_top=False, cap_bottom=False)
    # 鏡片（略凹）與開關。
    lathe(p, (0, 0, 0.196), [(0.0215, 0.0), (0.012, 0.002), (0.0, 0.0025)], "IH_Lens", steps=24, cap_bottom=False)
    box(p, (0, 0.017, 0.125), (0.008, 0.006, 0.014), "IH_Rubber", bevel=0.002)
    ob = finish(p)
    # 筒身沿 +Y（Unity 的 +Z＝前方）。
    for v in ob.data.vertices:
        x, y, z = v.co
        v.co = (x, z - L / 2, -y)
    return ob


def _shoe(p, cx, yaw, length, mat_upper, heel=0.0, boot=0.0):
    """一隻鞋：鞋底（橡膠）＋鞋身（圓角、前低後高）＋鞋口。cx：中心 x；yaw：度。"""
    import bmesh
    from mathutils import Euler
    tmp = Part("_shoe")
    w = length * 0.36
    # 鞋底：前圓後方。
    rounded_box(tmp, (0, 0, 0.012), (w, length, 0.024), 0.02, "IH_Rubber", cuts=4)
    if heel > 0:
        box(tmp, (0, -length * 0.38, heel / 2), (w * 0.7, length * 0.2, heel), "IH_Rubber", bevel=0.004)
    # 鞋身：前段低圓、後段高。
    rounded_box(tmp, (0, length * 0.12, 0.045 + heel * 0.5), (w * 0.95, length * 0.7, 0.06), 0.028, mat_upper, cuts=5, puff=0.01)
    rounded_box(tmp, (0, -length * 0.22, 0.06 + heel * 0.7), (w * 0.98, length * 0.45, 0.085 + boot), 0.03, mat_upper, cuts=5)
    # 鞋口（深色內裡）。
    rounded_box(tmp, (0, -length * 0.2, 0.1 + heel * 0.7 + boot), (w * 0.7, length * 0.32, 0.012), 0.005, "LR_Plastic", cuts=2)
    bmesh.ops.rotate(tmp.bm, verts=list(tmp.bm.verts), cent=Vector((0, 0, 0)), matrix=Euler((0, 0, math.radians(yaw))).to_matrix())
    bmesh.ops.translate(tmp.bm, verts=list(tmp.bm.verts), vec=Vector((cx, 0, 0)))
    for m in tmp.mats:
        p.mat(m)
    import bpy
    me = bpy.data.meshes.new("_s")
    # 保留各面材質：依名稱重新對應索引。
    remap = [p.mat(m) for m in tmp.mats]
    for f in tmp.bm.faces:
        f.material_index = remap[f.material_index]
    tmp.bm.to_mesh(me)
    tmp.bm.free()
    p.bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def shoes_pair():
    """一雙常穿的女用皮鞋（一隻稍微歪掉，像隨手脫下）。"""
    p = Part("ShoesPair")
    _shoe(p, -0.06, 4, 0.25, "P1_Leather", heel=0.025)
    _shoe(p, 0.07, -11, 0.25, "P1_Leather", heel=0.025)
    return finish(p)


def boots_pair():
    """一雙短靴（麂皮色，靠牆並排）。"""
    p = Part("BootsPair")
    _shoe(p, -0.065, 0, 0.27, "IH_Suede", heel=0.03, boot=0.09)
    _shoe(p, 0.065, 2, 0.27, "IH_Suede", heel=0.03, boot=0.09)
    return finish(p)


def coat_hanging(name="CoatHanging", mat="P1_Coat"):
    """掛在衣鉤上的大衣：肩部圓起、往下垂並有布褶，下擺微張。原點＝掛鉤點（背面），布料垂在 −Z 方向。"""
    p = Part(name)

    def fn(u, v):
        # u：左右 0～1；v：上到下 0～1。
        width = 0.18 + 0.32 * min(1.0, v * 3.0) + 0.06 * v
        x = (u - 0.5) * width
        z = -v * 1.0 - 0.03
        shoulder = math.exp(-((v - 0.08) / 0.07) ** 2) * 0.05
        fold = math.sin(u * math.pi * 5 + v * 1.3) * 0.012 * min(1.0, v * 2)
        bulge = math.sin(u * math.pi) * (0.06 + shoulder)
        return (x, 0.04 + bulge + fold, z)

    grid_sheet(p, 0.5, 1.0, 18, 22, fn, mat, thickness=0.012)
    # 衣領（翻領）與掛環。
    rounded_box(p, (0, 0.07, -0.06), (0.2, 0.06, 0.06), 0.025, mat, cuts=4)
    torus(p, (0, 0.03, -0.01), 0.012, 0.003, "LR_Linen", steps=12, tube=6, axis='y')
    return finish(p)


def coat_hook_rail():
    """牆上的木製掛衣板＋三個黃銅掛鉤（往前伸、末端圓頭）。原點＝背面中心。"""
    p = Part("HookRail")
    box(p, (0, 0.012, 0), (0.62, 0.024, 0.09), "LR_WoodDark", bevel=0.006)
    for x in (-0.22, 0.0, 0.22):
        box(p, (x, 0.05, 0.004), (0.012, 0.055, 0.012), "LR_Brass", bevel=0.004)
        box(p, (x, 0.074, 0.02), (0.012, 0.01, 0.03), "LR_Brass", bevel=0.004)
        lathe(p, (x, 0.074, 0.034), [(0.0, 0.0), (0.009, 0.002), (0.01, 0.008), (0.0, 0.014)], "LR_Brass", steps=12)
    return finish(p)

def letter_stack():
    """一疊信：幾個信封錯開疊放，一封已拆開、信紙露出一半。原點＝底面中心。"""
    p = Part("LetterStack")
    rng = [(0.0, 0.0, 0, "IH_Paper"), (0.012, -0.008, 7, "IH_Kraft"), (-0.01, 0.006, -5, "IH_Paper"), (0.006, 0.01, 12, "IH_Paper")]
    z = 0.0
    for dx, dy, yaw, m in rng:
        box(p, (dx, dy, z + 0.0015), (0.22, 0.11, 0.003), m, rot=(0, 0, yaw))
        z += 0.0032
    # 拆開的那封：信紙抽出一半。
    box(p, (0.03, 0.03, z + 0.002), (0.2, 0.095, 0.0015), "IH_Paper", rot=(0, 0, 18))
    return finish(p)


def mug():
    """陶瓷馬克杯（杯底留一點茶漬的顏色由貼圖負責）。"""
    p = Part("Mug")
    lathe(p, (0, 0, 0), [(0.0, 0.0), (0.036, 0.0), (0.04, 0.006), (0.041, 0.09), (0.043, 0.095), (0.037, 0.095), (0.036, 0.012), (0.0, 0.012)], "LR_Ceramic", steps=28)
    torus(p, (0.041, 0, 0.05), 0.024, 0.006, "LR_Ceramic", steps=16, tube=8, axis='y', arc=0.5, start=-90)
    ob = finish(p)
    # 杯把旋轉：弧段原本在 xz 平面以 (0.048, 0.05) 為圓心，從右側開口朝內，轉 -110° 讓開口貼著杯身。
    return ob


def remote_control():
    """電視遙控器（深色塑膠、按鍵）。"""
    p = Part("Remote")
    rounded_box(p, (0, 0, 0.011), (0.05, 0.17, 0.022), 0.01, "LR_Plastic", cuts=4)
    for i in range(4):
        for j in range(3):
            rounded_box(p, (-0.014 + j * 0.014, 0.03 - i * 0.017, 0.0225), (0.009, 0.009, 0.004), 0.002, "IH_Rubber", cuts=2)
    rounded_box(p, (0, 0.062, 0.0225), (0.012, 0.012, 0.004), 0.003, "LR_CushionRed", cuts=2)
    return finish(p)


def key_bowl():
    """玄關的陶碗＋一串鑰匙。"""
    p = Part("KeyBowl")
    lathe(p, (0, 0, 0), [(0.0, 0.0), (0.04, 0.0), (0.075, 0.025), (0.09, 0.05), (0.086, 0.052), (0.071, 0.028), (0.038, 0.006), (0.0, 0.006)], "LR_Ceramic", steps=32)
    torus(p, (0.01, 0.0, 0.012), 0.014, 0.0018, "P1_Steel", steps=14, tube=5, axis='z')
    for i, a in enumerate((20, 75, 140)):
        ang = math.radians(a)
        cx, cy = 0.01 + math.cos(ang) * 0.03, math.sin(ang) * 0.03
        box(p, (cx, cy, 0.01), (0.034, 0.011, 0.0025), "LR_Brass" if i != 1 else "P1_Steel", bevel=0.001, rot=(0, 0, a))
    return finish(p)


def umbrella_closed():
    """收起來的長柄傘（布面有摺痕、彎柄）。原點＝傘尖接地點，靠在傘架裡用。"""
    p = Part("Umbrella")
    steps = 16
    prof = [(0.003, 0.0), (0.006, 0.03), (0.03, 0.12), (0.034, 0.42), (0.02, 0.6), (0.008, 0.66)]
    faces = lathe(p, (0, 0, 0), prof, "IH_Umbrella", steps=steps)
    # 摺痕：偶數頂點往內收。
    for f in faces:
        for v in f.verts:
            a = math.atan2(v.co.y, v.co.x)
            k = 0.82 + 0.18 * abs(math.cos(a * 4))
            v.co.x *= k
            v.co.y *= k
    lathe(p, (0, 0, 0.66), [(0.006, 0), (0.006, 0.18), (0.01, 0.2)], "P1_Steel", steps=10)
    torus(p, (0.03, 0, 0.86), 0.03, 0.009, "LR_WoodDark", steps=14, tube=8, axis='y', arc=0.6, start=180)
    return finish(p)


def newspaper():
    """對折的報紙（略微翹起的紙張）。"""
    p = Part("Newspaper")

    def fn(u, v):
        x = (u - 0.5) * 0.3
        y = (v - 0.5) * 0.42
        z = 0.004 + 0.012 * (abs(u - 0.5) * 2) ** 3 + 0.004 * math.sin(v * math.pi)
        return (x, z, y)

    faces = grid_sheet(p, 0.3, 0.42, 8, 10, fn, "IH_Newsprint", thickness=0.006)
    ob = finish(p)
    for v in ob.data.vertices:
        x, y, z = v.co
        v.co = (x, z, y)
    return ob


def cushion_tilted(name="CushionTilted", mat="LR_CushionCream"):
    """一個被靠過、有凹陷的方形抱枕。"""
    p = Part(name)
    rounded_box(p, (0, 0, 0.2), (0.42, 0.14, 0.4), 0.06, mat, cuts=7, puff=0.03, sag=0.02, rot=(0, 0, 0))
    return finish(p)


def baluster(height=0.86):
    """車削欄杆柱（維多利亞式：方底、瓶身、細頸、方頂）。原點＝底部中心。"""
    p = Part("Baluster")
    W = "LR_WoodDark"
    box(p, (0, 0, 0.06), (0.042, 0.042, 0.12), W, bevel=0.004)
    h = height - 0.2
    prof = [(0.016, 0.0), (0.019, 0.01), (0.013, 0.03), (0.02, h * 0.28), (0.012, h * 0.55), (0.016, h * 0.66), (0.011, h * 0.8), (0.015, h * 0.94), (0.017, h)]
    lathe(p, (0, 0, 0.12), prof, W, steps=14)
    box(p, (0, 0, height - 0.04), (0.042, 0.042, 0.08), W, bevel=0.004)
    return finish(p)


def newel_post(height=1.18):
    """起步柱：方柱、凹槽面板、帽頂與球飾。原點＝底部中心。"""
    p = Part("NewelPost")
    W = "LR_WoodDark"
    box(p, (0, 0, 0.08), (0.13, 0.13, 0.16), W, bevel=0.01)
    box(p, (0, 0, height * 0.5), (0.105, 0.105, height - 0.2), W, bevel=0.008)
    for s in (-1, 1):
        box(p, (s * 0.054, 0, height * 0.5), (0.004, 0.06, height - 0.42), "LR_WoodMid", bevel=0.001)
        box(p, (0, s * 0.054, height * 0.5), (0.06, 0.004, height - 0.42), "LR_WoodMid", bevel=0.001)
    box(p, (0, 0, height - 0.08), (0.14, 0.14, 0.05), W, bevel=0.01)
    lathe(p, (0, 0, height - 0.055), [(0.05, 0.0), (0.03, 0.02), (0.045, 0.05), (0.04, 0.08), (0.0, 0.1)], W, steps=18)
    return finish(p)


def handrail_segment():
    """1 m 長的扶手（蘑菇形斷面），沿 +Y；Unity 端沿長度縮放。原點＝起點中心。"""
    p = Part("Handrail")
    prof = [(-0.028, -0.03), (0.028, -0.03), (0.03, -0.02), (0.022, -0.012), (0.034, 0.0), (0.034, 0.014), (0.024, 0.028), (0.0, 0.034), (-0.024, 0.028), (-0.034, 0.014), (-0.034, 0.0), (-0.022, -0.012), (-0.03, -0.02)]
    lr.extrude_profile(p, prof, 1.0, "LR_WoodDark", axis='y', origin=(0, 0, 0), smooth=True)
    return finish(p, sharp_angle=60)


def stair_rod():
    """黃銅壓毯棍（1 m，沿 X）與兩端的固定座。"""
    p = Part("StairRod")
    lr.extrude_profile(p, [(math.cos(a) * 0.006, math.sin(a) * 0.006 + 0.006) for a in [i * math.pi / 6 for i in range(12)]], 1.0, "LR_Brass", axis='x', origin=(-0.5, 0, 0), smooth=True)
    for x in (-0.5, 0.5):
        box(p, (x, 0.0, 0.008), (0.012, 0.018, 0.016), "LR_Brass", bevel=0.003)
    return finish(p)


def build_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    jobs = [
        ("WallSconce", wall_sconce), ("Flashlight", flashlight), ("ShoesPair", shoes_pair), ("BootsPair", boots_pair),
        ("CoatHanging", coat_hanging), ("CoatHangingRed", lambda: coat_hanging("CoatHangingRed", "P1_CoatRed")), ("LetterStack", letter_stack),
        ("Mug", mug), ("Remote", remote_control), ("KeyBowl", key_bowl), ("Umbrella", umbrella_closed), ("Newspaper", newspaper),
        ("Baluster", baluster), ("NewelPost", newel_post), ("Handrail", handrail_segment), ("StairRod", stair_rod),
        ("HookRail", coat_hook_rail), ("CushionTilted", cushion_tilted), ("CushionTiltedRed", lambda: cushion_tilted("CushionTiltedRed", "LR_CushionRed")),
    ]
    for name, fn in jobs:
        ob = fn()
        export([ob], os.path.join(out_dir, name + ".fbx"))
        print("[Case01Immersive] model", name)
