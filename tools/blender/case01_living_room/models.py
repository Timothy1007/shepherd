"""Case01 v0.6 客廳美術標準間：Blender（bpy＋bmesh）程序建模，匯出 FBX 給 Unity。

慣例：單位公尺；家具原點在地面中心（掛牆物在背面中心）；正面朝 Blender +Y（匯入 Unity 後是 +Z）。
Blender (x, y, z) → Unity (x, z, y)（匯出：axis_forward=-Z、axis_up=Y、bake_space_transform）。
材質只命名（LR_*），貼圖與著色器由 Unity 端依名稱指定。UV 以公尺為單位做方塊投影（貼圖密度一致）。
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

TEX_METERS = {  # 一張貼圖覆蓋幾公尺（方塊投影的比例）
    "LR_WoodMid": 0.8, "LR_WoodDark": 0.8, "LR_SofaFabric": 0.6, "LR_CushionCream": 0.5, "LR_CushionRed": 0.5,
    "LR_Plaid": 0.9, "LR_Curtain": 1.0, "LR_Paint": 1.0, "LR_Brass": 0.3, "LR_Ceramic": 0.4, "LR_Shade": 0.5,
    "LR_Iron": 0.6, "LR_HearthTile": 0.6, "LR_Linen": 0.4, "LR_Plastic": 1.0, "LR_Screen": 1.0, "LR_Ember": 0.3,
    "LR_Glass": 1.0, "LR_Flower": 0.2, "LR_Leaf": 1.0, "LR_Books": 1.0, "LR_Rug": 1.0, "LR_Painting": 1.0,
}


class Part:
    """一個 bmesh 加上材質名稱清單；faces 的 material_index 對應清單位置。"""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.fix = self.bm.faces.layers.int.new("fixuv")

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)


# ---------------------------------------------------------------- 幾何工具

def _set_faces(part, faces, mat_name, smooth):
    mi = part.mat(mat_name)
    for f in faces:
        f.material_index = mi
        f.smooth = smooth


def _merge(part, tmp, mat, smooth):
    """把暫存 bmesh 的幾何併入 part（所有面設為同一材質）。回傳新加入的頂點數。"""
    mi = part.mat(mat)
    for f in tmp.faces:
        f.material_index = mi
        f.smooth = smooth
    me = bpy.data.meshes.new("_tmp")
    tmp.to_mesh(me)
    tmp.free()
    part.bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def _xform(tmp, center, rot):
    if rot is not None:
        from mathutils import Euler
        bmesh.ops.rotate(tmp, verts=list(tmp.verts), cent=Vector((0, 0, 0)), matrix=Euler([math.radians(a) for a in rot]).to_matrix())
    bmesh.ops.translate(tmp, verts=list(tmp.verts), vec=Vector(center))


def rounded_box(part, center, size, radius, mat, cuts=6, puff=0.0, sag=0.0, smooth=True, rot=None):
    """均勻拓樸的圓角方塊（細分立方體投影到圓角盒面）。puff：上下表面中央鼓起；sag：上表面中央下陷（坐墊）。"""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=2.0)
    bmesh.ops.subdivide_edges(tmp, edges=list(tmp.edges), cuts=cuts, use_grid_fill=True)
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    r = min(radius, hx, hy, hz)
    inner = Vector((hx - r, hy - r, hz - r))
    for v in tmp.verts:
        p = Vector((v.co.x * hx, v.co.y * hy, v.co.z * hz))
        q = Vector((max(-inner.x, min(inner.x, p.x)), max(-inner.y, min(inner.y, p.y)), max(-inner.z, min(inner.z, p.z))))
        d = p - q
        if d.length < 1e-6:
            d = Vector((0, 0, 1))
        p = q + d.normalized() * r
        if puff or sag:
            fx = max(0.0, 1 - (p.x / hx) ** 2)
            fy = max(0.0, 1 - (p.y / hy) ** 2)
            bulge = fx * fy
            if p.z > 0:
                p.z += puff * bulge - sag * bulge
            else:
                p.z -= puff * 0.4 * bulge
        v.co = p
    _xform(tmp, center, rot)
    _merge(part, tmp, mat, smooth)


def box(part, center, size, mat, bevel=0.0, segments=2, smooth=False, rot=None):
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    for v in tmp.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=min(bevel, min(size) * 0.45), segments=segments, profile=0.5, affect='EDGES', clamp_overlap=True)
    _xform(tmp, center, rot)
    _merge(part, tmp, mat, smooth or bevel > 0)

def lathe(part, center, profile, mat, steps=24, smooth=True, cap_top=True, cap_bottom=True):
    """旋轉體：profile＝[(半徑, 高度), ...]，由下往上。"""
    bm = part.bm
    rings = []
    for r, z in profile:
        ring = []
        for i in range(steps):
            a = 2 * math.pi * i / steps
            ring.append(bm.verts.new((center[0] + math.cos(a) * r, center[1] + math.sin(a) * r, center[2] + z)))
        rings.append(ring)
    faces = []
    for k in range(len(rings) - 1):
        for i in range(steps):
            j = (i + 1) % steps
            faces.append(bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i])))
    if cap_bottom and profile[0][0] > 1e-4:
        faces.append(bm.faces.new(list(reversed(rings[0]))))
    if cap_top and profile[-1][0] > 1e-4:
        faces.append(bm.faces.new(rings[-1]))
    _set_faces(part, faces, mat, smooth)
    return faces


def extrude_profile(part, profile, length, mat, axis='x', origin=(0, 0, 0), smooth=False):
    """把 2D 輪廓（(前後, 高度)）沿 axis 拉長（線腳、窗台、壁爐架邊）。"""
    bm = part.bm
    ox, oy, oz = origin
    a, b = [], []
    for (u, z) in profile:
        if axis == 'x':
            a.append(bm.verts.new((ox, oy + u, oz + z)))
            b.append(bm.verts.new((ox + length, oy + u, oz + z)))
        else:
            a.append(bm.verts.new((ox + u, oy, oz + z)))
            b.append(bm.verts.new((ox + u, oy + length, oz + z)))
    faces = []
    n = len(profile)
    for i in range(n - 1):
        faces.append(bm.faces.new((a[i], a[i + 1], b[i + 1], b[i])))
    faces.append(bm.faces.new(a[::-1]))
    faces.append(bm.faces.new(b))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    _set_faces(part, faces, mat, smooth)
    return faces


def grid_sheet(part, w, h, nx, ny, fn, mat, thickness=0.01, smooth=True):
    """可變形的布片（窗簾、毯子）：fn(u, v) → (x, y, z)，u、v 為 0～1；加上厚度。"""
    bm = part.bm
    front, back = [], []
    for j in range(ny + 1):
        rf, rb = [], []
        for i in range(nx + 1):
            u, v = i / nx, j / ny
            p = Vector(fn(u, v))
            rf.append(bm.verts.new(p))
            rb.append(bm.verts.new(p + Vector((0, -thickness, 0))))
        front.append(rf)
        back.append(rb)
    faces = []
    for j in range(ny):
        for i in range(nx):
            faces.append(bm.faces.new((front[j][i], front[j][i + 1], front[j + 1][i + 1], front[j + 1][i])))
            faces.append(bm.faces.new((back[j][i], back[j + 1][i], back[j + 1][i + 1], back[j][i + 1])))
    for i in range(nx):
        faces.append(bm.faces.new((front[0][i], back[0][i], back[0][i + 1], front[0][i + 1])))
        faces.append(bm.faces.new((front[ny][i], front[ny][i + 1], back[ny][i + 1], back[ny][i])))
    for j in range(ny):
        faces.append(bm.faces.new((front[j][0], front[j + 1][0], back[j + 1][0], back[j][0])))
        faces.append(bm.faces.new((front[j][nx], back[j][nx], back[j + 1][nx], front[j + 1][nx])))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    _set_faces(part, faces, mat, smooth)
    # 布片用自己的 UV（沿長寬展開）。
    for f in faces:
        for loop in f.loops:
            co = loop.vert.co
            loop[part.uv].uv = (co.x / TEX_METERS.get(mat, 1.0), co.z / TEX_METERS.get(mat, 1.0))
        f[part.fix] = 1
    return faces


def finish(part, sharp_angle=38.0):
    """方塊投影 UV（沒有固定 UV 的面）、依角度標記硬邊，轉成 Blender 物件。"""
    bm = part.bm
    bm.faces.ensure_lookup_table()
    bm.normal_update()

    for f in bm.faces:
        if f[part.fix]:
            continue
        n = f.normal
        m = part.mats[f.material_index] if f.material_index < len(part.mats) else "x"
        s = 1.0 / TEX_METERS.get(m, 1.0)
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            co = loop.vert.co
            if ax == 0:
                uv = (co.y * s, co.z * s)
            elif ax == 1:
                uv = (co.x * s, co.z * s)
            else:
                uv = (co.x * s, co.y * s)
            loop[part.uv].uv = uv
    for e in bm.edges:
        if len(e.link_faces) == 2:
            a, b = e.link_faces
            e.smooth = not (a.normal.angle(b.normal, 0) > math.radians(sharp_angle) or not (a.smooth and b.smooth))
        else:
            e.smooth = False
    me = bpy.data.meshes.new(part.name)
    bm.to_mesh(me)
    bm.free()
    for mname in part.mats:
        mat = bpy.data.materials.get(mname) or bpy.data.materials.new(mname)
        me.materials.append(mat)
    ob = bpy.data.objects.new(part.name, me)
    bpy.context.collection.objects.link(ob)
    return ob


def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, add_leaf_bones=False, path_mode='STRIP',
                             mesh_smooth_type='OFF', use_mesh_modifiers=False)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)


# ---------------------------------------------------------------- 家具

def turned_leg(part, x, y, z0, height, mat, top_block=0.06, r=0.028):
    """車削桌腳：頂部方塊＋車削段（鼓肚、細頸、底端）。"""
    box(part, (x, y, z0 + height - top_block / 2), (r * 2.2, r * 2.2, top_block), mat, bevel=0.004)
    h = height - top_block
    prof = [(r * 0.7, 0), (r * 0.8, 0.02), (r * 0.6, 0.05), (r * 1.15, h * 0.35), (r * 0.7, h * 0.55), (r * 0.9, h * 0.72), (r * 0.65, h * 0.9), (r * 1.0, h)]
    lathe(part, (x, y, z0), prof, mat, steps=14)


def sofa(seats=2, width=1.9, name="Sofa"):
    p = Part(name)
    F, W = "LR_SofaFabric", "LR_WoodDark"
    d, arm_w = 0.9, 0.2
    inner = width - 2 * arm_w
    # 底座與裙邊。
    rounded_box(p, (0, 0, 0.21), (width - 0.04, d - 0.06, 0.26), 0.04, F, cuts=4)
    # 捲邊扶手：側板＋頂部的圓捲。
    for s in (-1, 1):
        x = s * (width / 2 - arm_w / 2)
        rounded_box(p, (x, 0.0, 0.36), (arm_w, d, 0.5), 0.05, F, cuts=5)
        rounded_box(p, (x + s * 0.01, 0.0, 0.6), (arm_w + 0.05, d + 0.02, 0.16), 0.075, F, cuts=6, puff=0.01)
        # 扶手正面的捲紋板。
        rounded_box(p, (x + s * 0.01, d / 2 - 0.005, 0.42), (arm_w + 0.04, 0.05, 0.42), 0.02, F, cuts=3)
    # 靠背框。
    rounded_box(p, (0, -d / 2 + 0.11, 0.6), (inner + 0.02, 0.2, 0.62), 0.06, F, cuts=5, puff=0.015)
    # 座墊與靠墊（分件、中央下陷／鼓起）。
    cw = inner / seats
    for i in range(seats):
        x = -inner / 2 + cw * (i + 0.5)
        rounded_box(p, (x, 0.08, 0.42), (cw - 0.015, d - 0.27, 0.16), 0.06, F, cuts=7, puff=0.02, sag=0.025)
        rounded_box(p, (x, -d / 2 + 0.27, 0.69), (cw - 0.03, 0.18, 0.42), 0.07, F, cuts=7, puff=0.03, rot=(-12, 0, 0))
    # 車削小腳。
    for sx in (-1, 1):
        for sy in (-1, 1):
            lathe(p, (sx * (width / 2 - 0.08), sy * (d / 2 - 0.08), 0), [(0.03, 0), (0.035, 0.03), (0.025, 0.06), (0.04, 0.085)], W, steps=12)
    return finish(p)


def throw_blanket():
    """搭在沙發左扶手上的格紋毯：從座墊邊緣沿扶手圓捲翻過去、在外側垂下，帶幾道淺褶。"""
    p = Part("Throw")
    path = [(-0.6, 0.5), (-0.68, 0.53), (-0.75, 0.62), (-0.8, 0.69), (-0.87, 0.715), (-0.94, 0.7), (-0.99, 0.64), (-1.005, 0.5), (-1.0, 0.32)]
    seg = [math.hypot(path[i + 1][0] - path[i][0], path[i + 1][1] - path[i][1]) for i in range(len(path) - 1)]
    total = sum(seg)

    def along(u):
        d = u * total
        for i, s in enumerate(seg):
            if d <= s or i == len(seg) - 1:
                k = min(1.0, d / s)
                return (path[i][0] + (path[i + 1][0] - path[i][0]) * k, path[i][1] + (path[i + 1][1] - path[i][1]) * k)
            d -= s

    def fn(u, v):
        x, z = along(u)
        y = (v - 0.5) * 0.62
        fold = 0.012 * math.sin(v * 2 * math.pi * 3 + u * 2) * (0.3 + 0.7 * u)
        return (x - fold * 0.5, y, z + 0.012 + fold * 0.3)
    grid_sheet(p, total, 0.62, 18, 12, fn, "LR_Plaid", thickness=0.01)
    return finish(p, sharp_angle=80)

def pillow(name="Pillow"):
    p = Part(name)
    rounded_box(p, (0, 0, 0), (0.44, 0.13, 0.42), 0.05, "LR_CushionCream", cuts=8, puff=0.0)
    # 讓前後鼓起（以 y 為厚度方向）。
    for v in p.bm.verts:
        fx = max(0.0, 1 - (v.co.x / 0.22) ** 2)
        fz = max(0.0, 1 - (v.co.z / 0.21) ** 2)
        v.co.y *= 1.0 + 1.4 * fx * fz
    return finish(p)


def coffee_table(L=1.1, D=0.6, H=0.46, name="CoffeeTable"):
    p = Part(name)
    M = "LR_WoodMid"
    # 桌面（圓角邊＋下緣小線腳）。
    box(p, (0, 0, H - 0.02), (L, D, 0.04), M, bevel=0.012, segments=3)
    box(p, (0, 0, H - 0.045), (L - 0.04, D - 0.04, 0.012), M, bevel=0.004)
    # 桌裙與抽屜。
    box(p, (0, 0, H - 0.1), (L - 0.12, D - 0.1, 0.1), M, bevel=0.004)
    box(p, (0, D / 2 - 0.045, H - 0.1), (L * 0.42, 0.012, 0.072), "LR_WoodDark", bevel=0.003)
    # 抽屜把手：小球。
    rounded_box(p, (0, D / 2 - 0.028, H - 0.1), (0.03, 0.025, 0.03), 0.012, "LR_Brass", cuts=3)
    # 四支車削腳。
    for sx in (-1, 1):
        for sy in (-1, 1):
            turned_leg(p, sx * (L / 2 - 0.07), sy * (D / 2 - 0.07), 0, H - 0.04, M)
    # 下層板。
    box(p, (0, 0, 0.11), (L - 0.12, D - 0.12, 0.022), M, bevel=0.006)
    return finish(p)


def side_table():
    p = Part("SideTable")
    M = "LR_WoodMid"
    S, H = 0.44, 0.58
    box(p, (0, 0, H - 0.015), (S, S, 0.03), M, bevel=0.01, segments=3)
    box(p, (0, 0, H - 0.08), (S - 0.06, S - 0.06, 0.1), M, bevel=0.004)
    box(p, (0, S / 2 - 0.028, H - 0.08), (S * 0.6, 0.01, 0.07), "LR_WoodDark", bevel=0.003)
    rounded_box(p, (0, S / 2 - 0.018, H - 0.08), (0.025, 0.02, 0.025), 0.01, "LR_Brass", cuts=3)
    for sx in (-1, 1):
        for sy in (-1, 1):
            turned_leg(p, sx * (S / 2 - 0.05), sy * (S / 2 - 0.05), 0, H - 0.03, M, r=0.022)
    box(p, (0, 0, 0.12), (S - 0.08, S - 0.08, 0.018), M, bevel=0.005)
    return finish(p)


def bookcase(W=1.3, H=1.9, D=0.32, tops=(0.08, 0.5, 0.92, 1.34)):
    """客廳書櫃：側板、背板（企口條紋）、上方線腳、踢腳、四片層板＋前緣封邊。書本由 Unity 依層板擺放。"""
    p = Part("Bookcase")
    M, Dk = "LR_WoodMid", "LR_WoodDark"
    for s in (-1, 1):
        box(p, (s * (W / 2 - 0.02), 0, H / 2), (0.04, D, H), M, bevel=0.006)
    box(p, (0, -D / 2 + 0.01, H / 2), (W - 0.06, 0.02, H - 0.04), Dk)
    for i in range(1, 9):
        box(p, (-W / 2 + 0.03 + i * (W - 0.06) / 9, -D / 2 + 0.022, H / 2), (0.006, 0.004, H - 0.06), M)
    # 上方線腳（三段階梯）＋頂板。
    box(p, (0, 0, H - 0.01), (W, D, 0.04), M, bevel=0.006)
    box(p, (0, 0.01, H + 0.03), (W + 0.06, D + 0.04, 0.04), M, bevel=0.01, segments=3)
    box(p, (0, 0.02, H + 0.065), (W + 0.1, D + 0.06, 0.03), M, bevel=0.008, segments=2)
    # 踢腳。
    box(p, (0, 0.005, 0.04), (W + 0.02, D + 0.01, 0.08), Dk, bevel=0.006)
    for t in tops:
        box(p, (0, 0.005, t - 0.015), (W - 0.08, D - 0.02, 0.03), M, bevel=0.004)
        box(p, (0, D / 2 - 0.006, t - 0.015), (W - 0.08, 0.014, 0.04), M, bevel=0.004)
    return finish(p)


def book():
    """一本書（厚 1 × 高 1 × 深 1，Unity 依需要縮放）：圓角書背、封面板、內縮的書頁塊。UV 指向書背圖集。"""
    p = Part("Book")
    B, Pg = "LR_Books", "LR_Books"
    # 封面兩片與書背。
    box(p, (-0.44, 0, 0.5), (0.12, 1.0, 1.0), B, bevel=0.02, segments=2)
    box(p, (0.44, 0, 0.5), (0.12, 1.0, 1.0), B, bevel=0.02, segments=2)
    rounded_box(p, (0, 0.46, 0.5), (1.0, 0.1, 1.0), 0.05, B, cuts=3)
    box(p, (0, -0.02, 0.5), (0.78, 0.94, 0.96), "LR_Pages")
    ob = finish(p)
    # UV：書背（+y 面）用 0～7/8 欄由 Unity 以 offset 選色；書頁面用最右欄。
    me = ob.data
    uvl = me.uv_layers.active.data
    for poly in me.polygons:
        m = me.materials[poly.material_index].name
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            if m == "LR_Pages":
                uvl[li].uv = (0.875 + (co.y + 0.5) * 0.12, co.z)
            else:
                uvl[li].uv = ((co.x + 0.5) * 0.125, co.z)
    return ob


def sash_window(Wo=1.06, Ho=1.2, depth=0.15):
    """英式上下拉窗：外框、上下兩扇（含窗櫺）、中間會合橫檔、室內側線腳（窗套）與窗台、窗台下的裙板。
    原點＝窗洞中心（牆厚中央），正面（室內）朝 +Y。玻璃由 Unity 保留原本的透明玻璃。"""
    p = Part("SashWindow")
    P = "LR_Paint"
    jamb = 0.045
    # 外框（四邊，貼著窗洞內側）。
    for s in (-1, 1):
        box(p, (s * (Wo / 2 - jamb / 2), 0, 0), (jamb, depth, Ho), P, bevel=0.004)
    box(p, (0, 0, Ho / 2 - jamb / 2), (Wo, depth, jamb), P, bevel=0.004)
    box(p, (0, 0, -Ho / 2 + jamb / 2), (Wo, depth, jamb), P, bevel=0.004)
    # 上下兩扇：上扇略靠外（y 負），下扇略靠內。
    iw, ih = Wo - 2 * jamb, Ho - 2 * jamb
    for k, (zc, yoff) in enumerate(((ih / 4, -0.02), (-ih / 4, 0.02))):
        st, rl = 0.035, 0.045
        sh = ih / 2 + 0.01
        for s in (-1, 1):
            box(p, (s * (iw / 2 - st / 2), yoff, zc), (st, 0.04, sh), P, bevel=0.004)
        box(p, (0, yoff, zc + sh / 2 - rl / 2), (iw, 0.04, rl if k == 0 else 0.03), P, bevel=0.004)
        box(p, (0, yoff, zc - sh / 2 + rl / 2), (iw, 0.04, 0.03 if k == 0 else rl), P, bevel=0.004)
        box(p, (0, yoff, zc), (0.018, 0.025, sh - 0.06), P, bevel=0.003)   # 直櫺
    # 室內窗套（三邊線腳）。
    ay = depth / 2
    box(p, (0, ay + 0.012, Ho / 2 + 0.04), (Wo + 0.16, 0.024, 0.08), P, bevel=0.006)
    for s in (-1, 1):
        box(p, (s * (Wo / 2 + 0.04), ay + 0.012, 0), (0.08, 0.024, Ho + 0.08), P, bevel=0.006)
    # 窗台與裙板。
    box(p, (0, ay + 0.06, -Ho / 2 - 0.015), (Wo + 0.22, 0.17, 0.035), P, bevel=0.008, segments=2)
    box(p, (0, ay + 0.01, -Ho / 2 - 0.09), (Wo + 0.1, 0.02, 0.11), P, bevel=0.005)
    return finish(p)


def curtains(Wo=1.06, rod_y=0.16, top=2.32):
    """厚重窗簾：鐵桿＋尾飾、左右兩片有褶的落地簾，在約 1.0 m 高處束起。原點＝窗洞中心的地面、牆面（室內側）。"""
    objs = []
    p = Part("CurtainRod")
    rod_len = Wo + 0.7
    rb = rounded_box(p, (0, rod_y, top), (rod_len, 0.026, 0.026), 0.012, "LR_Iron", cuts=3)
    for s in (-1, 1):
        rounded_box(p, (s * (rod_len / 2 + 0.03), rod_y, top), (0.06, 0.06, 0.06), 0.03, "LR_Iron", cuts=4)
        box(p, (s * (Wo / 2 + 0.25), rod_y - 0.06, top), (0.02, 0.12, 0.03), "LR_Iron")
    objs.append(finish(p))
    for side in (-1, 1):
        c = Part("CurtainPanel_" + ("L" if side < 0 else "R"))
        panel_w = 0.42
        x_out = side * (Wo / 2 + 0.33)
        x_in = side * (Wo / 2 - 0.05)
        def fn(u, v, side=side, x_out=x_out):
            z = 0.02 + v * (top - 0.04)
            # 寬度：桿下全寬 → 束帶處收窄 → 下擺略微散開（典型的束起落地簾）。
            if z > 1.0:
                wdt = 0.16 + (panel_w - 0.16) * min(1.0, (z - 1.0) / 0.9) ** 0.8
            else:
                wdt = 0.16 + 0.12 * (1.0 - z) ** 0.9
            x = x_out - side * u * wdt
            amp = 0.035 * (wdt / panel_w)
            y = rod_y - 0.05 + amp * math.sin(u * 2 * math.pi * 3.5) + 0.01 * (1 - v)
            return (x, y, z)
        grid_sheet(c, panel_w, top, 28, 24, fn, "LR_Curtain", thickness=0.012)
        # 束帶（繞在收窄處）。
        rounded_box(c, (x_out - side * 0.09, rod_y - 0.05, 1.0), (0.22, 0.1, 0.05), 0.02, "LR_Curtain", cuts=3)
        objs.append(finish(c, sharp_angle=85))
    return objs


def table_lamp():
    p = Part("TableLamp")
    lathe(p, (0, 0, 0), [(0.0, 0.0), (0.08, 0.0), (0.085, 0.012), (0.06, 0.03), (0.035, 0.04)], "LR_Brass", steps=20)
    lathe(p, (0, 0, 0.04), [(0.035, 0.0), (0.075, 0.06), (0.085, 0.12), (0.06, 0.2), (0.025, 0.24), (0.02, 0.26)], "LR_Ceramic", steps=20)
    lathe(p, (0, 0, 0.3), [(0.012, 0.0), (0.012, 0.12)], "LR_Brass", steps=10)
    # 鼓形燈罩（上小下大，開口、有厚度）。
    sh = Part("LampShade")
    prof_out = [(0.17, 0.0), (0.12, 0.22)]
    prof_in = [(0.115, 0.22), (0.165, 0.0)]
    lathe(sh, (0, 0, 0.3), prof_out + prof_in + [(0.17, 0.0)], "LR_Shade", steps=28, cap_top=False, cap_bottom=False)
    lathe(p, (0, 0, 0.52), [(0.0, 0.0), (0.014, 0.004), (0.014, 0.03), (0.0, 0.04)], "LR_Brass", steps=10)
    return [finish(p), finish(sh, sharp_angle=89)]


def rug(W=2.2, D=1.6):
    p = Part("Rug")
    box(p, (0, 0, 0.006), (W, D, 0.012), "LR_Rug", bevel=0.004)
    ob = None
    for s in (-1, 1):
        for i in range(36):
            y = -D / 2 + 0.03 + i * (D - 0.06) / 35
            box(p, (s * (W / 2 + 0.035), y, 0.003), (0.07, 0.012, 0.004), "LR_Linen")
    ob = finish(p)
    me = ob.data
    uvl = me.uv_layers.active.data
    for poly in me.polygons:
        if me.materials[poly.material_index].name != "LR_Rug":
            continue
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uvl[li].uv = (co.x / W + 0.5, co.y / D + 0.5)
    return ob


def mantelpiece(W=1.5, H=1.18):
    """維多利亞式壁爐：木作壁爐架（壁柱、柱頭、橫楣、線腳台面）、鑄鐵內框（拱形開口＋爐柵）、磁磚爐床。
    原點＝胸牆正面中心的地面，正面朝 +Y。"""
    objs = []
    p = Part("Mantelpiece")
    Wd = "LR_WoodDark"
    pil_w, pil_d = 0.16, 0.08
    for s in (-1, 1):
        x = s * (W / 2 - pil_w / 2)
        box(p, (x, pil_d / 2, (H - 0.12) / 2), (pil_w, pil_d, H - 0.12), Wd, bevel=0.008)
        box(p, (x, pil_d / 2 + 0.006, 0.06), (pil_w + 0.03, pil_d + 0.02, 0.12), Wd, bevel=0.006)          # 柱礎
        box(p, (x, pil_d / 2 + 0.01, H - 0.2), (pil_w + 0.04, pil_d + 0.03, 0.1), Wd, bevel=0.012, segments=3)  # 柱頭（托架）
        box(p, (x, pil_d + 0.004, (H - 0.12) / 2), (pil_w * 0.55, 0.01, H * 0.6), Wd, bevel=0.003)            # 嵌板
    box(p, (0, pil_d / 2, H - 0.2), (W - 2 * pil_w, pil_d - 0.01, 0.18), Wd, bevel=0.006)                   # 橫楣
    box(p, (0, pil_d / 2 + 0.03, H - 0.06), (W + 0.1, pil_d + 0.12, 0.04), Wd, bevel=0.012, segments=3)     # 台面
    box(p, (0, pil_d / 2 + 0.02, H - 0.09), (W + 0.04, pil_d + 0.08, 0.025), Wd, bevel=0.008)
    objs.append(finish(p))
    ir = Part("FireInsert")
    I = "LR_Iron"
    iw, ih = W - 2 * pil_w, H - 0.3
    box(ir, (0, 0.012, ih / 2), (iw, 0.024, ih), I, bevel=0.004)
    # 拱形開口的暗內部＋框邊（以多段方塊近似拱）。
    rounded_box(ir, (0, 0.03, 0.34), (0.56, 0.02, 0.6), 0.2, "LR_Iron", cuts=6)
    for i in range(5):
        box(ir, (-0.18 + i * 0.09, 0.07, 0.17), (0.015, 0.03, 0.18), I, bevel=0.003)  # 爐柵直條
    box(ir, (0, 0.07, 0.26), (0.42, 0.03, 0.02), I)
    box(ir, (0, 0.07, 0.09), (0.42, 0.06, 0.02), I)
    for i in range(9):
        rounded_box(ir, (-0.15 + (i % 5) * 0.075, 0.05 + (i // 5) * 0.03, 0.12 + (i % 3) * 0.015), (0.06, 0.05, 0.04), 0.018, "LR_Ember", cuts=2)
    objs.append(finish(ir))
    h = Part("Hearth")
    box(h, (0, 0.28, 0.015), (W + 0.05, 0.56, 0.03), "LR_HearthTile", bevel=0.004)
    box(h, (0, 0.56, 0.02), (W + 0.09, 0.03, 0.04), "LR_WoodDark", bevel=0.004)
    # 爐具架（撥火棒）。
    lathe(h, (0.62, 0.38, 0.03), [(0.06, 0), (0.06, 0.015), (0.008, 0.02), (0.008, 0.55), (0.02, 0.57), (0.0, 0.6)], "LR_Brass", steps=12)
    objs.append(finish(h))
    return objs


def tv_unit():
    p = Part("TvUnit")
    Wd = "LR_WoodDark"
    W, D, H = 1.0, 0.42, 0.5
    box(p, (0, 0, H / 2 + 0.06), (W, D, H - 0.12), Wd, bevel=0.008)
    box(p, (0, 0, H - 0.01), (W + 0.03, D + 0.03, 0.03), Wd, bevel=0.008, segments=2)
    for s in (-1, 1):
        box(p, (s * W / 4, D / 2 + 0.005, H / 2 + 0.06), (W / 2 - 0.03, 0.012, H - 0.2), "LR_WoodMid", bevel=0.004)
        rounded_box(p, (s * 0.04, D / 2 + 0.018, H / 2 + 0.1), (0.02, 0.02, 0.02), 0.009, "LR_Brass", cuts=2)
    for sx in (-1, 1):
        for sy in (-1, 1):
            lathe(p, (sx * (W / 2 - 0.05), sy * (D / 2 - 0.05), 0), [(0.02, 0), (0.025, 0.04), (0.02, 0.07)], Wd, steps=10)
    tv = Part("Tv")
    box(tv, (0, -0.02, H + 0.035), (0.34, 0.2, 0.02), "LR_Plastic", bevel=0.006)
    box(tv, (0, -0.02, H + 0.12), (0.05, 0.04, 0.16), "LR_Plastic", bevel=0.006)
    box(tv, (0, 0.0, H + 0.48), (0.96, 0.05, 0.57), "LR_Plastic", bevel=0.012, segments=2)
    box(tv, (0, 0.027, H + 0.49), (0.9, 0.006, 0.51), "LR_Screen")
    return [finish(p), finish(tv)]


def frame(w, h, name, depth=0.035, moulding=0.045):
    """相框／畫框：外圍線腳（四邊斜接）、背板。原點在背面中心，正面朝 +Y。"""
    p = Part(name)
    Wd = "LR_WoodDark"
    for (cx, cz, sw, sh) in ((0, h / 2 - moulding / 2, w, moulding), (0, -h / 2 + moulding / 2, w, moulding),
                             (-w / 2 + moulding / 2, 0, moulding, h), (w / 2 - moulding / 2, 0, moulding, h)):
        box(p, (cx, depth / 2, cz), (sw, depth, sh), Wd, bevel=0.008, segments=2)
    box(p, (0, 0.004, 0), (w - moulding, 0.008, h - moulding), Wd)
    return finish(p)


def plant():
    p = Part("PlantPot")
    lathe(p, (0, 0, 0), [(0.0, 0.0), (0.13, 0.0), (0.16, 0.25), (0.175, 0.26), (0.175, 0.29), (0.16, 0.29)], "LR_Ceramic", steps=22, cap_top=False)
    lathe(p, (0, 0, 0.26), [(0.15, 0.0), (0.0, 0.0)], "LR_WoodDark", steps=22)
    leaves = Part("PlantLeaves")
    import random
    rnd = random.Random(7)
    for i in range(16):
        a = rnd.random() * math.pi * 2
        h = 0.35 + rnd.random() * 0.4
        r = 0.05 + rnd.random() * 0.12
        cx, cy = math.cos(a) * r, math.sin(a) * r
        def fn(u, v, a=a, cx=cx, cy=cy, h=h):
            s = 0.28
            x = cx + (u - 0.5) * s * math.cos(a + 1.57)
            y = cy + (u - 0.5) * s * math.sin(a + 1.57) + v * 0.1 * math.sin(a)
            z = 0.26 + v * h + 0.05 * math.sin(u * 3.14)
            return (x + v * 0.12 * math.cos(a), y + v * 0.12 * math.sin(a), z)
        grid_sheet(leaves, 0.28, h, 2, 3, fn, "LR_Leaf", thickness=0.001)
    # 葉卡 UV：每張卡取貼圖的一個 1/2 區塊。
    obj_l = finish(leaves, sharp_angle=89)
    me = obj_l.data
    uvl = me.uv_layers.active.data
    for poly in me.polygons:
        for k, li in enumerate(poly.loop_indices):
            uvl[li].uv = (uvl[li].uv[0] % 1.0, uvl[li].uv[1] % 1.0)
    return [finish(p), obj_l]


def props():
    """小道具：壁爐鐘、燭台、花瓶與花、茶杯與托盤（各自為一個物件）。"""
    out = []
    c = Part("MantelClock")
    rounded_box(c, (0, 0, 0.13), (0.26, 0.1, 0.22), 0.06, "LR_WoodDark", cuts=5)
    box(c, (0, 0, 0.012), (0.3, 0.12, 0.024), "LR_WoodDark", bevel=0.006)
    rounded_box(c, (0, 0.052, 0.15), (0.13, 0.008, 0.13), 0.06, "LR_Ceramic", cuts=4)   # 鐘面（直立、朝前）
    for v in []:
        pass
    out.append(finish(c))
    s = Part("Candlestick")
    lathe(s, (0, 0, 0), [(0.0, 0), (0.05, 0), (0.05, 0.01), (0.015, 0.03), (0.02, 0.12), (0.012, 0.2), (0.03, 0.22), (0.01, 0.23)], "LR_Brass", steps=16)
    lathe(s, (0, 0, 0.23), [(0.011, 0), (0.011, 0.11), (0.0, 0.115)], "LR_Linen", steps=12)
    out.append(finish(s))
    v = Part("FlowerVase")
    lathe(v, (0, 0, 0), [(0.0, 0), (0.045, 0), (0.06, 0.06), (0.04, 0.14), (0.03, 0.17), (0.04, 0.19)], "LR_Glass", steps=18, cap_top=False)
    import random
    rnd = random.Random(3)
    for i in range(9):
        a = rnd.random() * 6.28
        r = 0.03 + rnd.random() * 0.05
        rounded_box(v, (math.cos(a) * r, math.sin(a) * r, 0.24 + rnd.random() * 0.05), (0.07, 0.07, 0.06), 0.03, "LR_Flower", cuts=3, puff=0.01)
    out.append(finish(v))
    t = Part("TeaCup")
    lathe(t, (0, 0, 0), [(0.0, 0), (0.07, 0), (0.075, 0.008), (0.0, 0.008)], "LR_Ceramic", steps=20)
    lathe(t, (0, 0, 0.008), [(0.0, 0), (0.03, 0), (0.042, 0.05), (0.04, 0.052), (0.028, 0.006), (0.0, 0.006)], "LR_Ceramic", steps=20)
    out.append(finish(t))
    return out


def cornice(length=1.0):
    """天花板線板（沿 X 拉長 1 m，Unity 依牆長縮放）：凹弧輪廓。背面貼牆（y=0）、上緣貼天花（z=0）。"""
    p = Part("Cornice")
    prof = [(0.0, 0.0), (0.0, -0.02), (0.012, -0.03), (0.02, -0.05), (0.035, -0.065), (0.06, -0.075), (0.085, -0.08), (0.095, -0.07),
            (0.1, -0.06), (0.11, -0.05), (0.12, 0.0)]
    extrude_profile(p, prof, length, "LR_Paint", axis='x', origin=(-length / 2, 0, 0), smooth=True)
    return finish(p, sharp_angle=50)


def build_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    E = lambda objs, n: export(objs if isinstance(objs, list) else [objs], os.path.join(out_dir, n + ".fbx"))
    E([sofa(2, 1.9, "Sofa"), throw_blanket()], "Sofa")
    E(sofa(1, 0.95, "Armchair"), "Armchair")
    E(pillow(), "Pillow")
    E(coffee_table(), "CoffeeTable")
    E(side_table(), "SideTable")
    E(bookcase(), "Bookcase")
    E(book(), "Book")
    E(sash_window(), "SashWindow")
    E(curtains(), "Curtains")
    E(table_lamp(), "TableLamp")
    E(rug(), "Rug")
    E(mantelpiece(), "Mantelpiece")
    E(tv_unit(), "TvUnit")
    E(frame(0.36, 0.28, "PhotoFrame"), "PhotoFrame")
    E(frame(0.9, 0.66, "PaintingFrame", depth=0.045, moulding=0.07), "PaintingFrame")
    E(frame(0.62, 0.46, "WallFrame"), "WallFrame")
    E(plant(), "Plant")
    E(props(), "Props")
    E(cornice(), "Cornice")
