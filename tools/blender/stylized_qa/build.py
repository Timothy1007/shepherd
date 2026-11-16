# 《牧羊人》正式美術風格可行性測試：縫紉工作區資產（Blender 5.x）。
# 在 Blender 裡執行：
#   OUT_UNITY = r"...\unity\Assets\Exploration\StyleQA"; OUT_BLEND = r"...\tools\blender\stylized_qa\stylized_qa_assets.blend"
#   exec(open(r"...\tools\blender\stylized_qa\build.py", encoding="utf-8").read())
# 做法：
#   1. 全部用 bmesh 建真正的立體模型（倒角切面、錐度、曲面放樣），平面著色（flat shading）讓每個面成為一塊色面。
#   2. 每件資產 Smart UV，再用 Cycles「Emission 烘焙」把手繪感底色烘成一張貼圖：
#      固有色 × 色塊筆觸（色階化的雜訊）× 上亮下暗的大漸層，凹處混入偏冷的暗色（AO），邊緣提亮（Bevel 法線差）。
#      光影的明暗分層不烘進去，交給 Unity 的 Shepherd/PainterlyLit shader 即時計算（才能自由轉角度、換燈光）。
#   3. 匯出 FBX（Unity 軸向、套用變換），PNG 貼圖存到 Unity 專案。
# 不使用任何貼片、看板或圖片平面。
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

try:
    OUT_UNITY
except NameError:
    OUT_UNITY = os.path.join(os.path.dirname(bpy.data.filepath or "."), "StyleQA")
try:
    OUT_BLEND
except NameError:
    OUT_BLEND = None

MODELS = os.path.join(OUT_UNITY, "Models")
TEXTURES = os.path.join(OUT_UNITY, "Textures")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEXTURES, exist_ok=True)
rng = random.Random(17)


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)


# 名稱：(固有色, 凹處暗色, 筆觸尺度, 筆觸強度, 邊緣提亮)
PALETTE = {
    "Wood":        ("#8A5634", "#3B211E", 7.0, 0.07, 0.30),
    "WoodChair":   ("#95603A", "#41241F", 8.0, 0.07, 0.30),
    "WoodPale":    ("#B08251", "#5A3A2A", 9.0, 0.06, 0.30),
    "WoodFloor":   ("#6E4A2E", "#2B1C18", 5.0, 0.09, 0.18),
    "WoodTrim":    ("#4F3A2B", "#221816", 6.0, 0.06, 0.22),
    "Brass":       ("#C99A4C", "#5E3F22", 14.0, 0.08, 0.70),
    "Lacquer":     ("#2F2A2D", "#121014", 10.0, 0.06, 0.55),
    "Gold":        ("#D8AE58", "#7A5426", 20.0, 0.05, 0.60),
    "Steel":       ("#AEA9A0", "#4E4B49", 16.0, 0.06, 0.65),
    "ClothCream":  ("#E7DAC3", "#94857C", 6.0, 0.07, 0.25),
    "ClothRose":   ("#A9685F", "#552F35", 6.0, 0.08, 0.25),
    "ClothCharcoal": ("#4C4643", "#232026", 6.0, 0.08, 0.25),
    "ThreadRed":   ("#A23D33", "#4E1C20", 30.0, 0.06, 0.35),
    "ThreadMustard": ("#C9963F", "#64431F", 30.0, 0.06, 0.35),
    "ThreadSlate": ("#5F6C79", "#2B3039", 30.0, 0.06, 0.35),
    "Wall":        ("#5C625A", "#30352F", 3.0, 0.07, 0.20),
    "Plaster":     ("#8C8879", "#4A463F", 3.0, 0.06, 0.20),
    "Glass":       ("#C9C3A8", "#8F8A74", 2.0, 0.03, 0.10),
}


class Asset:
    """一件資產＝一個 bmesh；零件直接建在裡面，各自倒角，最後變成一個物件（保留材質槽）。"""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _finish(self, verts, mat, M, bevel):
        bmesh.ops.transform(self.bm, matrix=M, verts=verts)
        vs = set(verts)
        faces = {f for v in verts for f in v.link_faces if all(x in vs for x in f.verts)}
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            res = bmesh.ops.bevel(self.bm, geom=edges + list(vs), offset=bevel, offset_type='OFFSET', segments=1,
                                  profile=0.5, affect='EDGES', clamp_overlap=True)
            faces |= set(res.get('faces', []))
            faces = {f for f in faces if f.is_valid}
            for v in res.get('verts', []):
                for f in v.link_faces:
                    faces.add(f)
        idx = self.mi(mat)
        for f in faces:
            f.material_index = idx
        return faces

    @staticmethod
    def mat(loc=(0, 0, 0), rot=(0, 0, 0)):
        return Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()

    def cube(self, size, loc, mat, rot=(0, 0, 0), bevel=0.006, taper=1.0, taper_y=None, shear=(0.0, 0.0)):
        """size＝(x,y,z)；taper＝頂面相對底面的縮放（桌腳、抽屜）。"""
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = r['verts']
        ty = taper if taper_y is None else taper_y
        for v in verts:
            top = v.co.z > 0
            sx = size[0] * (taper if top else 1.0)
            sy = size[1] * (ty if top else 1.0)
            v.co = Vector((v.co.x * sx + (shear[0] if top else 0), v.co.y * sy + (shear[1] if top else 0), v.co.z * size[2]))
        return self._finish(verts, mat, self.mat(loc, rot), bevel)

    def cyl(self, r1, depth, loc, mat, rot=(0, 0, 0), segs=12, r2=None, bevel=0.0):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1,
                                  radius2=r1 if r2 is None else r2, depth=depth)
        return self._finish(r['verts'], mat, self.mat(loc, rot), bevel)

    def lathe(self, prof, loc, mat, rot=(0, 0, 0), segs=16, mats=None):
        """prof＝[(半徑, 高度), ...] 由下到上；繞 Z 軸旋轉。mats＝每一段的材質（可選）。"""
        rings = []
        for (rad, z) in prof:
            ring = []
            for i in range(segs):
                a = 2 * math.pi * i / segs
                ring.append(self.bm.verts.new((math.cos(a) * rad, math.sin(a) * rad, z)))
            rings.append(ring)
        allv = [v for ring in rings for v in ring]
        faces = []
        for k in range(len(rings) - 1):
            seg_mat = self.mi(mats[k] if mats else mat)
            for i in range(segs):
                j = (i + 1) % segs
                f = self.bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
                f.material_index = seg_mat
                faces.append(f)
        b = self.bm.faces.new(list(reversed(rings[0])))
        t = self.bm.faces.new(rings[-1])
        b.material_index = self.mi(mats[0] if mats else mat)
        t.material_index = self.mi(mats[-1] if mats else mat)
        bmesh.ops.transform(self.bm, matrix=self.mat(loc, rot), verts=allv)
        return faces

    def loft(self, rings, mat, n=8, expo=4.0, cap=True):
        """rings＝[(中心, 前進方向, 上方向, 寬, 高), ...]；斷面是超橢圓（expo 大＝接近圓角方形）。"""
        prev = first = None
        allv = []
        idx = self.mi(mat)
        for (c, t, u, w, h) in rings:
            c, t, u = Vector(c), Vector(t).normalized(), Vector(u)
            side = t.cross(u).normalized()
            up = side.cross(t).normalized()
            ring = []
            for i in range(n):
                a = 2 * math.pi * (i + 0.5) / n
                ca, sa = math.cos(a), math.sin(a)
                x = math.copysign(abs(ca) ** (2.0 / expo), ca)
                y = math.copysign(abs(sa) ** (2.0 / expo), sa)
                ring.append(self.bm.verts.new(c + side * x * w * 0.5 + up * y * h * 0.5))
            allv += ring
            if prev:
                for i in range(n):
                    j = (i + 1) % n
                    self.bm.faces.new((prev[i], prev[j], ring[j], ring[i])).material_index = idx
            else:
                first = ring
            prev = ring
        if cap:
            self.bm.faces.new(list(reversed(first))).material_index = idx
            self.bm.faces.new(prev).material_index = idx
        return allv

    def sheet(self, grid, mat, thickness=0.004):
        """grid＝[[Vector,...], ...]（列×欄）；交錯對角線三角化成切面，再加厚。"""
        rows, cols = len(grid), len(grid[0])
        vv = [[self.bm.verts.new(grid[r][c]) for c in range(cols)] for r in range(rows)]
        faces = []
        for r in range(rows - 1):
            for c in range(cols - 1):
                a, b, d, e = vv[r][c], vv[r][c + 1], vv[r + 1][c + 1], vv[r + 1][c]
                if (r + c) % 2 == 0:
                    faces += [self.bm.faces.new((a, b, d)), self.bm.faces.new((a, d, e))]
                else:
                    faces += [self.bm.faces.new((a, b, e)), self.bm.faces.new((b, d, e))]
        idx = self.mi(mat)
        res = bmesh.ops.solidify(self.bm, geom=faces, thickness=thickness)
        for f in faces:
            f.material_index = idx
        for g in res['geom']:
            if isinstance(g, bmesh.types.BMFace):
                g.material_index = idx
        return faces

    def build(self, collection):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        collection.objects.link(ob)
        for m in self.mats:
            me.materials.append(bake_material(m))
        return ob


# ------------------------------------------------------------------ 材質（烘焙用）

def bake_material(name):
    full = "SQA_" + name
    m = bpy.data.materials.get(full)
    if m:
        return m
    base, cav, bscale, bamt, edge = PALETTE[name]
    m = bpy.data.materials.new(full)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    N = nt.nodes.new
    L = nt.links.new
    out = N('ShaderNodeOutputMaterial')
    emis = N('ShaderNodeEmission')
    L(emis.outputs[0], out.inputs['Surface'])
    tc = N('ShaderNodeTexCoord')
    # 色塊筆觸：雜訊 → 色階（常數插值）→ 亮度係數
    noise = N('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = bscale
    noise.inputs['Detail'].default_value = 1.5
    noise.inputs['Distortion'].default_value = 0.6
    L(tc.outputs['Object'], noise.inputs['Vector'])
    ramp = N('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    steps = [(0.0, 1.0 - bamt), (0.38, 1.0 - bamt * 0.35), (0.52, 1.0 + bamt * 0.25), (0.64, 1.0 + bamt * 0.7)]
    els[0].position, els[0].color = steps[0][0], (steps[0][1],) * 3 + (1,)
    els[1].position, els[1].color = steps[1][0], (steps[1][1],) * 3 + (1,)
    for pos, v in steps[2:]:
        e = els.new(pos)
        e.color = (v,) * 3 + (1,)
    L(noise.outputs['Fac'], ramp.inputs['Fac'])
    col = N('ShaderNodeRGB')
    col.outputs[0].default_value = srgb(base)
    mul1 = N('ShaderNodeMix'); mul1.data_type = 'RGBA'; mul1.blend_type = 'MULTIPLY'
    mul1.inputs['Factor'].default_value = 1.0
    L(col.outputs[0], mul1.inputs['A']); L(ramp.outputs['Color'], mul1.inputs['B'])
    # 上亮下暗的大漸層（物件空間 Z，0～1.0 m）
    sep = N('ShaderNodeSeparateXYZ')
    L(tc.outputs['Object'], sep.inputs[0])
    mr = N('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = 0.0
    mr.inputs['From Max'].default_value = 1.0
    mr.inputs['To Min'].default_value = 0.86
    mr.inputs['To Max'].default_value = 1.06
    L(sep.outputs['Z'], mr.inputs['Value'])
    mul2 = N('ShaderNodeMix'); mul2.data_type = 'RGBA'; mul2.blend_type = 'MULTIPLY'
    mul2.inputs['Factor'].default_value = 1.0
    L(mul1.outputs['Result'], mul2.inputs['A']); L(mr.outputs['Result'], mul2.inputs['B'])
    # 凹處：AO → 混入偏冷的暗色
    ao = N('ShaderNodeAmbientOcclusion')
    ao.inputs['Distance'].default_value = 0.05
    ao.samples = 8
    aomr = N('ShaderNodeMapRange')
    aomr.inputs['From Min'].default_value = 0.25
    aomr.inputs['From Max'].default_value = 0.95
    L(ao.outputs['AO'], aomr.inputs['Value'])
    cavc = N('ShaderNodeRGB')
    cavc.outputs[0].default_value = srgb(cav)
    mixao = N('ShaderNodeMix'); mixao.data_type = 'RGBA'
    L(aomr.outputs['Result'], mixao.inputs['Factor'])
    L(cavc.outputs[0], mixao.inputs['A']); L(mul2.outputs['Result'], mixao.inputs['B'])
    # 邊緣提亮：Bevel 法線和幾何法線的差
    bev = N('ShaderNodeBevel')
    bev.samples = 8
    bev.inputs['Radius'].default_value = 0.004
    geo = N('ShaderNodeNewGeometry')
    dot = N('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    L(bev.outputs['Normal'], dot.inputs[0]); L(geo.outputs['Normal'], dot.inputs[1])
    emr = N('ShaderNodeMapRange')
    emr.inputs['From Min'].default_value = 0.985
    emr.inputs['From Max'].default_value = 0.9995
    emr.inputs['To Min'].default_value = edge
    emr.inputs['To Max'].default_value = 0.0
    L(dot.outputs['Value'], emr.inputs['Value'])
    light = N('ShaderNodeMix'); light.data_type = 'RGBA'; light.blend_type = 'SCREEN'
    L(emr.outputs['Result'], light.inputs['Factor'])
    L(mixao.outputs['Result'], light.inputs['A'])
    light.inputs['B'].default_value = srgb("#FFE7C4")
    L(light.outputs['Result'], emis.inputs['Color'])
    return m


# ------------------------------------------------------------------ 資產

def jit(a=0.6):
    return rng.uniform(-a, a)


def build_table(col):
    A = Asset("SQA_SewingTable")
    W, D, H, T = 1.30, 0.62, 0.76, 0.05
    # 桌面：厚實、前緣倒角大一點，微微不正
    A.cube((W, D, T), (0, 0, H - T / 2), "Wood", rot=(0, jit(0.3), jit(0.4)), bevel=0.012)
    # 左側抽屜櫃
    px0, pw = -W / 2 + 0.04, 0.44
    pcx = px0 + pw / 2
    ph = H - T - 0.07
    A.cube((0.035, D - 0.06, ph), (px0 + 0.0175, 0, 0.07 + ph / 2), "Wood", bevel=0.006)
    A.cube((0.035, D - 0.06, ph), (px0 + pw - 0.0175, 0, 0.07 + ph / 2), "Wood", bevel=0.006)
    A.cube((pw, 0.02, ph), (pcx, D / 2 - 0.04, 0.07 + ph / 2), "WoodTrim", bevel=0.003)  # 背板
    A.cube((pw + 0.02, D - 0.04, 0.07), (pcx, 0, 0.035), "WoodTrim", bevel=0.008, taper=0.97)  # 底座
    A.cube((pw - 0.07, D - 0.08, 0.025), (pcx, 0, 0.085), "Wood", bevel=0.004)
    fy = -(D - 0.06) / 2
    dh = (ph - 0.05) / 2
    for k in range(2):
        z = 0.105 + dh / 2 + k * (dh + 0.012)
        A.cube((pw - 0.08, 0.03, dh - 0.01), (pcx, fy - 0.004, z), "Wood", rot=(0, 0, jit(0.4)), bevel=0.008)
        A.cube((pw - 0.13, 0.012, dh - 0.07), (pcx, fy - 0.022, z), "Wood", bevel=0.004)  # 面板浮雕
        # 黃銅把手：底座＋橫桿
        for sx in (-0.045, 0.045):
            A.cube((0.014, 0.022, 0.018), (pcx + sx, fy - 0.035, z + 0.01), "Brass", bevel=0.003)
        A.cyl(0.0065, 0.11, (pcx, fy - 0.044, z + 0.01), "Brass", rot=(0, 90, 0), segs=8)
    # 右側兩支錐形方腳
    lx = W / 2 - 0.07
    for ly in (-D / 2 + 0.06, D / 2 - 0.06):
        A.cube((0.06, 0.06, H - T), (lx, ly, (H - T) / 2), "Wood", rot=(jit(0.5), jit(0.5), jit(1.0)), bevel=0.006, taper=1.0)
        A.cube((0.075, 0.075, 0.04), (lx, ly, 0.02), "WoodTrim", bevel=0.006, taper=0.85)  # 腳墊
    # 牙板（桌面下的橫條）
    ax0, ax1 = px0 + pw, lx + 0.03
    A.cube((ax1 - ax0, 0.025, 0.09), ((ax0 + ax1) / 2, D / 2 - 0.06, H - T - 0.045), "Wood", bevel=0.004)
    A.cube((0.025, D - 0.12, 0.09), (lx, 0, H - T - 0.045), "Wood", bevel=0.004)
    # 中間的淺抽屜（前面）
    A.cube((ax1 - ax0 - 0.02, 0.03, 0.085), ((ax0 + ax1) / 2, -D / 2 + 0.045, H - T - 0.046), "Wood", rot=(0, 0, jit(0.3)), bevel=0.007)
    cx = (ax0 + ax1) / 2
    for sx in (-0.04, 0.04):
        A.cube((0.012, 0.02, 0.016), (cx + sx, -D / 2 + 0.022, H - T - 0.044), "Brass", bevel=0.003)
    A.cyl(0.006, 0.1, (cx, -D / 2 + 0.014, H - T - 0.044), "Brass", rot=(0, 90, 0), segs=8)
    # 右下橫撐
    A.cube((0.035, D - 0.12, 0.04), (lx, 0, 0.13), "Wood", bevel=0.005)
    return A.build(col)


def build_machine(col):
    A = Asset("SQA_SewingMachine")
    # 木底座
    A.cube((0.46, 0.24, 0.035), (0, 0, 0.0175), "WoodPale", bevel=0.008, taper=0.96)
    # 機床（黑漆金屬板）
    A.cube((0.40, 0.19, 0.03), (0, 0, 0.05), "Lacquer", bevel=0.007, taper=0.98)
    A.cube((0.07, 0.05, 0.003), (-0.12, -0.03, 0.066), "Steel", bevel=0.001)  # 針板
    A.cube((0.05, 0.045, 0.003), (-0.05, -0.03, 0.066), "Steel", bevel=0.001)  # 滑板
    # 立柱（右側，向上收窄、略往前傾）
    A.loft([((0.115, 0.01, 0.065), (0, 0, 1), (0, 1, 0), 0.085, 0.075),
            ((0.115, 0.012, 0.14), (0, 0, 1), (0, 1, 0), 0.07, 0.064),
            ((0.11, 0.012, 0.215), (-0.15, 0, 1), (0, 1, 0), 0.065, 0.06)], "Lacquer", n=10, expo=3.2)
    # 機臂：從立柱頂往左，略拱起，到機頭漸細（招牌的弧線）
    pts = [(0.12, 0.012, 0.225), (0.06, 0.012, 0.245), (-0.02, 0.01, 0.248), (-0.09, 0.008, 0.238), (-0.135, 0.006, 0.228)]
    rings = []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        t = Vector(b) - Vector(a)
        w = 0.07 - 0.022 * (i / (len(pts) - 1))
        rings.append((p, (-t.x, -t.y, -t.z), (0, 0, 1), 0.06 - 0.008 * (i / 4), w))
    A.loft(rings, "Lacquer", n=10, expo=3.0)
    # 機頭（左端的方塊＋面板）
    A.cube((0.05, 0.062, 0.11), (-0.155, 0.006, 0.19), "Lacquer", bevel=0.008, taper=0.92)
    A.cube((0.004, 0.05, 0.085), (-0.182, 0.006, 0.19), "Steel", bevel=0.0015)  # 面板
    # 針桿、壓腳、針
    A.cyl(0.0045, 0.07, (-0.162, -0.012, 0.115), "Steel", segs=8)
    A.cyl(0.0035, 0.05, (-0.152, -0.012, 0.12), "Steel", segs=8)
    A.cube((0.012, 0.022, 0.006), (-0.152, -0.016, 0.073), "Steel", bevel=0.002)
    A.cyl(0.0012, 0.02, (-0.162, -0.012, 0.075), "Steel", segs=6)
    A.cube((0.008, 0.012, 0.012), (-0.165, -0.035, 0.205), "Steel", bevel=0.002)  # 挑線桿
    # 手輪（右側，沿 X 軸）：多邊形切面＋輪輻
    A.lathe([(0.012, -0.018), (0.07, -0.016), (0.072, -0.004), (0.066, 0.012), (0.056, 0.016), (0.056, 0.008),
             (0.02, 0.008), (0.016, 0.022), (0.006, 0.024)], (0.175, 0.012, 0.205), "Steel", rot=(0, 90, 0), segs=20,
            mats=["Steel", "Steel", "Lacquer", "Lacquer", "Lacquer", "Lacquer", "Steel", "Steel"])
    for k in range(5):
        a = 72 * k + 18
        A.cube((0.008, 0.01, 0.042), (0.168, 0.012 + math.cos(math.radians(a)) * 0.035, 0.205 + math.sin(math.radians(a)) * 0.035),
               "Lacquer", rot=(-a + 90, 0, 0), bevel=0.002)
    # 線軸座與線軸、繞線器
    A.cyl(0.0025, 0.05, (0.02, 0.012, 0.29), "Steel", segs=6)
    A.lathe([(0.0, 0.0), (0.016, 0.0), (0.016, 0.004), (0.011, 0.005), (0.0125, 0.008), (0.0125, 0.03),
             (0.011, 0.033), (0.016, 0.034), (0.016, 0.038), (0.0, 0.038)], (0.02, 0.012, 0.272), "WoodPale", segs=12,
            mats=["WoodPale", "WoodPale", "WoodPale", "ThreadRed", "ThreadRed", "ThreadRed", "WoodPale", "WoodPale", "WoodPale"])
    A.lathe([(0.0, 0), (0.016, 0), (0.016, 0.01), (0.0, 0.012)], (0.075, -0.036, 0.235), "Steel", rot=(90, 0, 0), segs=12)
    # 金色裝飾：機臂側面的菱形與細線、立柱上的花紋
    for (x, z, s) in [(-0.06, 0.244, 0.022), (0.0, 0.248, 0.028), (0.06, 0.243, 0.022)]:
        A.cube((s, 0.0016, s), (x, -0.021 + abs(x) * 0.05, z), "Gold", rot=(0, 45, 0), bevel=0.0)
    A.cube((0.2, 0.0016, 0.004), (-0.02, -0.0215, 0.268), "Gold", bevel=0.0)
    A.cube((0.2, 0.0016, 0.004), (-0.02, -0.0215, 0.222), "Gold", bevel=0.0)
    A.cube((0.024, 0.0016, 0.05), (0.113, -0.027, 0.14), "Gold", rot=(0, 0, 0), bevel=0.0)
    A.cube((0.34, 0.0016, 0.004), (0, -0.0965, 0.05), "Gold", bevel=0.0)  # 機床前緣
    return A.build(col)


def build_chair(col):
    A = Asset("SQA_Chair")
    SH = 0.45
    # 座面：前寬後窄、前緣倒圓角（大倒角）
    A.cube((0.44, 0.41, 0.035), (0, 0, SH - 0.0175), "WoodChair", bevel=0.01, taper=1.0, taper_y=0.97)
    # 前腳（略外八）
    for sx in (-1, 1):
        A.cube((0.038, 0.038, SH - 0.035), (sx * 0.185, -0.165, (SH - 0.035) / 2), "WoodChair",
               rot=(-2, sx * 2, jit(2)), bevel=0.005, taper=1.12)
    # 後腳＋靠背立柱（一體，後仰）
    for sx in (-1, 1):
        A.loft([((sx * 0.185, 0.175, 0.0), (0, -0.05, 1), (0, 1, 0), 0.038, 0.04),
                ((sx * 0.185, 0.17, SH - 0.02), (0, 0.02, 1), (0, 1, 0), 0.04, 0.042),
                ((sx * 0.18, 0.215, 0.93), (0, 0.16, 1), (0, 1, 0), 0.034, 0.034)], "WoodChair", n=8, expo=6)
    # 靠背：兩條彎曲橫板（中間往後凹）
    for z, hgt in ((0.88, 0.075), (0.70, 0.05)):
        yb = 0.17 + (z - SH) * 0.1
        rings = []
        for i in range(7):
            x = -0.17 + 0.34 * i / 6
            y = yb + 0.022 * (1 - (x / 0.17) ** 2)
            rings.append(((x, y, z), (1, 0, 0), (0, 0, 1), 0.02, hgt))
        A.loft(rings, "WoodChair", n=8, expo=8)
    # 橫撐
    for (a, b) in [((-0.185, -0.16, 0.16), (-0.185, 0.17, 0.16)), ((0.185, -0.16, 0.16), (0.185, 0.17, 0.16)),
                   ((-0.185, 0.0, 0.18), (0.185, 0.0, 0.18))]:
        a, b = Vector(a), Vector(b)
        t = b - a
        rings = [(a + t * f, t, (0, 0, 1) if abs(t.z) < 0.9 else (1, 0, 0), 0.022 * (1 + 0.15 * math.sin(f * math.pi)),
                  0.022 * (1 + 0.15 * math.sin(f * math.pi))) for f in (0, 0.5, 1)]
        A.loft(rings, "WoodChair", n=8, expo=2.2)
    # 座面下的框
    A.cube((0.36, 0.025, 0.05), (0, -0.16, SH - 0.06), "WoodChair", bevel=0.004)
    A.cube((0.025, 0.31, 0.05), (-0.17, 0.0, SH - 0.06), "WoodChair", bevel=0.004)
    A.cube((0.025, 0.31, 0.05), (0.17, 0.0, SH - 0.06), "WoodChair", bevel=0.004)
    return A.build(col)


def drape(A, path, width, mat, cols=9, fold_amp=0.012, fold_freq=3.0, jag=0.05, jag_ends=(False, True), thick=0.004, seed=0):
    """沿路徑（XZ 平面的 Y-Z 折線：[(y,z),...]）鋪一塊布，寬度沿 X；越往下垂摺痕越深；兩端可做撕裂般的鋸齒下擺。"""
    r = random.Random(seed)
    P = [Vector((0, y, z)) for (y, z) in path]
    seglen = [0.0]
    for i in range(1, len(P)):
        seglen.append(seglen[-1] + (P[i] - P[i - 1]).length)
    total = seglen[-1]
    grid = []
    for ri, s0 in enumerate(seglen):
        p = P[ri]
        a = P[max(ri - 1, 0)]; b = P[min(ri + 1, len(P) - 1)]
        t = (b - a).normalized()
        nrm = Vector((0, -t.z, t.y))  # 路徑法線（在 Y-Z 平面）
        hang = max(0.0, (P[0].z - p.z)) if True else 0
        row = []
        for c in range(cols):
            u = c / (cols - 1)
            x = (u - 0.5) * width
            amp = fold_amp * (0.25 + min(hang / 0.25, 1.0) * 1.6)
            off = math.sin(u * math.pi * fold_freq * 2 + seed) * amp + r.uniform(-0.003, 0.003)
            q = p + Vector((x, 0, 0)) + nrm * off
            row.append(q)
        grid.append(row)
    # 鋸齒下擺：最後一列（以及第一列）每隔一個點往外拉長
    for end, flag in ((0, jag_ends[0]), (len(grid) - 1, jag_ends[1])):
        if not flag:
            continue
        nb = grid[end - 1] if end > 0 else grid[1]
        for c in range(cols):
            d = (grid[end][c] - nb[c]).normalized()
            grid[end][c] = grid[end][c] + d * (jag * (0.9 if c % 2 == 0 else 0.15) * r.uniform(0.6, 1.3))
    A.sheet(grid, mat, thickness=thick)


def build_cloth(col):
    """桌面一角垂下來的布（米白）、折好的一疊布（三色）。座標以桌面高度＝0。"""
    A = Asset("SQA_ClothDrape")
    # 從桌面上往前緣垂下：路徑 y 往 -（前方）
    path = [(0.18, 0.004), (0.06, 0.006), (-0.06, 0.004), (-0.135, 0.0), (-0.165, -0.035), (-0.172, -0.12), (-0.17, -0.22), (-0.165, -0.32)]
    drape(A, path, 0.42, "ClothCream", cols=9, fold_amp=0.01, fold_freq=2.0, jag=0.06, seed=3)
    return A.build(col)


def build_cloth_rose(col):
    A = Asset("SQA_ClothRose")
    path2 = [(0.14, 0.006), (0.02, 0.008), (-0.1, 0.006), (-0.135, -0.02), (-0.145, -0.1), (-0.142, -0.19)]
    drape(A, path2, 0.24, "ClothRose", cols=7, fold_amp=0.008, fold_freq=1.5, jag=0.045, seed=5)
    return A.build(col)


def build_cloth_chair(col):
    """搭在椅背上的布：越過橫板、前後兩邊垂下。座標以橫板頂＝原點。"""
    A = Asset("SQA_ClothChair")
    path = [(-0.06, -0.28), (-0.05, -0.17), (-0.035, -0.07), (-0.018, -0.01), (0.0, 0.012), (0.022, -0.005), (0.04, -0.06), (0.05, -0.15)]
    drape(A, path, 0.2, "ClothCharcoal", cols=7, fold_amp=0.01, fold_freq=1.3, jag=0.05, jag_ends=(True, True), seed=8)
    return A.build(col)


def build_fabric_stack(col):
    A = Asset("SQA_FabricStack")
    z = 0.0
    for i, (mat, h) in enumerate([("ClothCharcoal", 0.03), ("ClothRose", 0.026), ("ClothCream", 0.03)]):
        A.cube((0.30 - i * 0.012, 0.21 - i * 0.008, h), (jit(0.01) * 0.6, jit(0.01) * 0.6, z + h / 2), mat,
               rot=(0, 0, jit(4)), bevel=0.009, taper=0.985, shear=(jit(0.006), 0))
        z += h
    return A.build(col)


def build_spools(col):
    A = Asset("SQA_Spools")
    for (x, y, mat, s) in [(0.0, 0.0, "ThreadMustard", 1.0), (0.05, 0.02, "ThreadRed", 0.9), (0.03, -0.045, "ThreadSlate", 1.1),
                           (-0.05, -0.02, "ThreadCream" if False else "ThreadRed", 0.85)]:
        prof = [(0.0, 0.0), (0.019, 0.0), (0.019, 0.006), (0.013, 0.007), (0.0155, 0.01), (0.0155, 0.036), (0.013, 0.039),
                (0.019, 0.04), (0.019, 0.046), (0.0, 0.046)]
        prof = [(r * s, z * s) for (r, z) in prof]
        A.lathe(prof, (x, y, 0), "WoodPale", rot=(0, 0, rng.uniform(0, 30)), segs=12,
                mats=["WoodPale", "WoodPale", "WoodPale", mat, mat, mat, "WoodPale", "WoodPale", "WoodPale"])
    # 一個倒下來的線軸＋一小段垂下的線
    prof = [(0.0, 0.0), (0.017, 0.0), (0.017, 0.005), (0.012, 0.006), (0.014, 0.009), (0.014, 0.032), (0.012, 0.035), (0.017, 0.036), (0.017, 0.041), (0.0, 0.041)]
    A.lathe(prof, (-0.02, 0.06, 0.017), "WoodPale", rot=(90, 0, 25), segs=12,
            mats=["WoodPale", "WoodPale", "WoodPale", "ThreadSlate", "ThreadSlate", "ThreadSlate", "WoodPale", "WoodPale", "WoodPale"])
    return A.build(col)


def build_stage(col):
    """展示用的小房間：木地板、兩面牆（護牆板）、窗框。只是讓資產有個環境，不是正式場景。"""
    A = Asset("SQA_Stage")
    S = 3.6
    pw = 0.16
    n = int(S / pw)
    for i in range(n):
        x = -S / 2 + pw * (i + 0.5)
        y0 = -S / 2
        while y0 < S / 2 - 0.01:
            L = min(rng.uniform(0.9, 1.8), S / 2 - y0)
            A.cube((pw - 0.006, L - 0.006, 0.03), (x, y0 + L / 2, -0.015 + rng.uniform(-0.002, 0.002)), "WoodFloor",
                   rot=(0, 0, rng.uniform(-0.15, 0.15)), bevel=0.004)
            y0 += L
    WH = 3.0
    # 背牆（+Y）留窗洞：窗台 0.95～2.25，x -0.55～0.75
    wx0, wx1, wz0, wz1 = -0.55, 0.75, 0.95, 2.25
    t = 0.12
    yW = S / 2 + t / 2
    A.cube((wx0 + S / 2, t, WH), ((-S / 2 + wx0) / 2, yW, WH / 2), "Wall", bevel=0.0)
    A.cube((S / 2 - wx1, t, WH), ((wx1 + S / 2) / 2, yW, WH / 2), "Wall", bevel=0.0)
    A.cube((wx1 - wx0, t, wz0), ((wx0 + wx1) / 2, yW, wz0 / 2), "Wall", bevel=0.0)
    A.cube((wx1 - wx0, t, WH - wz1), ((wx0 + wx1) / 2, yW, (wz1 + WH) / 2), "Wall", bevel=0.0)
    # 左牆（-X）
    A.cube((t, S, WH), (-S / 2 - t / 2, 0, WH / 2), "Wall", bevel=0.0)
    # 護牆板與踢腳
    for (sz, loc) in [((S, 0.025, 0.9), (0, S / 2 - 0.0125, 0.45)), ((0.025, S, 0.9), (-S / 2 + 0.0125, 0, 0.45))]:
        A.cube(sz, loc, "WoodTrim", bevel=0.0)
    A.cube((S, 0.045, 0.05), (0, S / 2 - 0.03, 0.92), "WoodTrim", bevel=0.008)
    A.cube((0.045, S, 0.05), (-S / 2 + 0.03, 0, 0.92), "WoodTrim", bevel=0.008)
    for k in range(9):
        A.cube((0.012, 0.03, 0.78), (-S / 2 + 0.2 + k * 0.4, S / 2 - 0.03, 0.47), "WoodTrim", bevel=0.003)
        A.cube((0.03, 0.012, 0.78), (-S / 2 - 0.0 + 0.03, -S / 2 + 0.2 + k * 0.4, 0.47), "WoodTrim", bevel=0.003)
    # 窗框與窗格（中間沒有玻璃，光直接照進來）
    fw = 0.06
    for (sz, loc) in [((wx1 - wx0 + 0.1, 0.16, fw), ((wx0 + wx1) / 2, yW - 0.03, wz0 - fw / 2 + 0.01)),
                      ((wx1 - wx0 + 0.1, 0.1, fw), ((wx0 + wx1) / 2, yW, wz1 + fw / 2 - 0.01)),
                      ((fw, 0.1, wz1 - wz0), (wx0 + fw / 2 - 0.02, yW, (wz0 + wz1) / 2)),
                      ((fw, 0.1, wz1 - wz0), (wx1 - fw / 2 + 0.02, yW, (wz0 + wz1) / 2))]:
        A.cube(sz, loc, "WoodTrim", bevel=0.008)
    for i in range(1, 4):
        x = wx0 + (wx1 - wx0) * i / 4
        A.cube((0.025, 0.05, wz1 - wz0), (x, yW, (wz0 + wz1) / 2), "WoodTrim", bevel=0.004)
    for j in range(1, 4):
        z = wz0 + (wz1 - wz0) * j / 4
        A.cube((wx1 - wx0, 0.05, 0.025), ((wx0 + wx1) / 2, yW, z), "WoodTrim", bevel=0.004)
    return A.build(col)


# ------------------------------------------------------------------ UV、烘焙、匯出

def select_only(ob):
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob


def uv_unwrap(ob):
    select_only(ob)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')


def bake(ob, res):
    img_name = ob.name + "_Paint"
    img = bpy.data.images.get(img_name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(img_name, res, res, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    nodes_added = []
    for slot in ob.material_slots:
        nt = slot.material.node_tree
        n = nt.nodes.new('ShaderNodeTexImage')
        n.image = img
        nt.nodes.active = n
        nodes_added.append((nt, n))
    select_only(ob)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 24
    sc.cycles.device = 'CPU'
    sc.render.bake.margin = 6
    bpy.ops.object.bake(type='EMIT')
    path = os.path.join(TEXTURES, img_name + ".png")
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    for nt, n in nodes_added:
        nt.nodes.remove(n)
    return path


def export_fbx(ob):
    select_only(ob)
    path = os.path.join(MODELS, ob.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, object_types={'MESH'}, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False,
                             axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    return path


def run():
    sc = bpy.data.scenes.get("StyleQA") or bpy.data.scenes.new("StyleQA")
    bpy.context.window.scene = sc
    for o in list(sc.collection.all_objects):
        bpy.data.objects.remove(o, do_unlink=True)
    col = sc.collection
    builders = [(build_table, 2048), (build_machine, 1024), (build_chair, 1024), (build_cloth_rose, 512), (build_cloth, 1024),
                (build_cloth_chair, 512), (build_fabric_stack, 512), (build_spools, 512), (build_stage, 2048)]
    report = []
    for fn, res in builders:
        ob = fn(col)
        if ob.name == "SQA_ClothDrape":
            pass
        uv_unwrap(ob)
        bake(ob, res)
        export_fbx(ob)
        me = ob.data
        report.append("%s: %d verts, %d tris, %d mats, tex %d" % (ob.name, len(me.vertices), sum(len(p.vertices) - 2 for p in me.polygons), len(me.materials), res))
    # 預覽排列（Blender 內部看用；Unity 裡的位置由 QA 場景決定）
    layout = {"SQA_SewingTable": (0, 0, 0), "SQA_SewingMachine": (-0.2, -0.02, 0.76), "SQA_Chair": (0.1, -0.62, 0),
              "SQA_ClothDrape": (0.32, -0.02, 0.76), "SQA_ClothRose": (0.40, -0.03, 0.765), "SQA_ClothChair": (0.1, -0.43, 0.915),
              "SQA_FabricStack": (0.45, 0.1, 0.76), "SQA_Spools": (0.12, 0.12, 0.76), "SQA_Stage": (0, 0.9, 0)}
    for n, p in layout.items():
        o = bpy.data.objects.get(n)
        if o:
            o.location = p
    if OUT_BLEND:
        for img in bpy.data.images:
            if img.name.startswith("SQA_") and not img.packed_file:
                img.pack()
        ids = {sc} | {o for o in sc.collection.all_objects}
        bpy.data.libraries.write(OUT_BLEND, ids, fake_user=True, compress=True)
    return report


REPORT = run()
print("\n".join(REPORT))
