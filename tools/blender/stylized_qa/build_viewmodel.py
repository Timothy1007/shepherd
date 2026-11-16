# 《牧羊人》美術風格 QA 第二部分：第一人稱的提燈與左手（Blender 5.x）。
# 和 build.py 同一套做法（bmesh 立體零件、平面著色、Cycles 烘焙手繪底色、FBX）。
# 在 Blender 裡執行：
#   OUT_UNITY = r"...\unity\Assets\Exploration\StyleQA"; OUT_BLEND = r"...\tools\blender\stylized_qa\stylized_qa_viewmodel.blend"
#   exec(open(r"...\tools\blender\stylized_qa\build_viewmodel.py", encoding="utf-8").read())
# 座標：Blender Z 朝上。提燈的原點＝提把頂端中央（手握的位置），提燈往 -Z 垂下；
# 左手的原點＝握著的那段提把（沿 X 軸），手背朝 +Z、手指往 +Y 繞過提把，前臂往 -Y 延伸。
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

_here = os.path.dirname(OUT_BLEND) if globals().get("OUT_BLEND") else r"C:\Users\USER\shepherd\tools\blender\stylized_qa"
SQA_LIBRARY_ONLY = True
exec(open(os.path.join(_here, "build.py"), encoding="utf-8").read())

PALETTE.update({
    "Bronze":      ("#6E5240", "#2A1D1B", 12.0, 0.08, 0.22),
    "BronzeDark":  ("#463428", "#1C1414", 12.0, 0.07, 0.18),
    "LanternGlass": ("#F3C57E", "#C98A48", 4.0, 0.05, 0.05),
    "Flame":       ("#FFF1CF", "#FFD08A", 3.0, 0.02, 0.0),
    "Glove":       ("#3A3532", "#181519", 9.0, 0.06, 0.10),
    "Skin":        ("#D19A74", "#8A4E44", 6.0, 0.04, 0.08),
    "Sleeve":      ("#E3D5BD", "#8F8079", 5.0, 0.06, 0.10),
    "Strap":       ("#5A3E2C", "#24171A", 10.0, 0.06, 0.12),
})


def star(A, center, normal, up, size, mat, thick=0.0015):
    """四角星（參考圖提燈頂蓋上的星形紋）：平的星形多邊形加厚，貼在面上。"""
    n = Vector(normal).normalized()
    u = (Vector(up) - n * Vector(up).dot(n)).normalized()
    s = u.cross(n).normalized()
    pts = []
    for i in range(8):
        a = math.pi / 2 + i * math.pi / 4
        r = size if i % 2 == 0 else size * 0.3
        pts.append(Vector(center) + (s * math.cos(a) + u * math.sin(a)) * r)
    top = [A.bm.verts.new(p + n * thick) for p in pts]
    bot = [A.bm.verts.new(p) for p in pts]
    idx = A.mi(mat)
    A.bm.faces.new(top).material_index = idx
    A.bm.faces.new(list(reversed(bot))).material_index = idx
    for i in range(8):
        j = (i + 1) % 8
        A.bm.faces.new((bot[i], bot[j], top[j], top[i])).material_index = idx


def bar(A, a, b, w, h, mat, up=(0, 1, 0), bevel=0.0015):
    """兩點之間的方形桿（提把的一段一段直桿）。"""
    a, b = Vector(a), Vector(b)
    d = b - a
    L = d.length
    z = d.normalized()
    x = Vector(up).cross(z).normalized()
    y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = (a + b) / 2
    r = bmesh.ops.create_cube(A.bm, size=1.0)
    for v in r['verts']:
        v.co = Vector((v.co.x * w, v.co.y * h, v.co.z * (L + w * 0.5)))
    A._finish(r['verts'], mat, M, bevel)


def build_lantern(col):
    A = Asset("SQA_Lantern")
    PH = -120.0  # 六角形：讓一個面正對 -Y（前方）
    # 底座：階梯狀
    A.lathe([(0.0, 0.0), (0.076, 0.0), (0.078, 0.012), (0.068, 0.014), (0.068, 0.03), (0.074, 0.032), (0.074, 0.045), (0.0, 0.046)],
            (0, 0, 0), "Bronze", segs=6, phase=PH,
            mats=["Bronze", "Bronze", "BronzeDark", "BronzeDark", "Bronze", "Bronze", "Bronze"])
    # 玻璃（六角柱，下窄、三分之一處最寬、往上收）
    A.lathe([(0.0, 0.046), (0.061, 0.046), (0.072, 0.086), (0.056, 0.18), (0.0, 0.18)], (0, 0, 0), "LanternGlass", segs=6, phase=PH)
    # 火焰：拉長的六角水晶
    A.lathe([(0.0, 0.07), (0.018, 0.098), (0.013, 0.13), (0.0, 0.165)], (0, 0, 0), "Flame", segs=6, phase=PH + 30)
    A.lathe([(0.0, 0.05), (0.022, 0.05), (0.02, 0.07), (0.0, 0.072)], (0, 0, 0), "BronzeDark", segs=6, phase=PH)  # 燈芯座
    # 角柱：沿著玻璃輪廓的六根粗桿
    prof = [(0.046, 0.066), (0.086, 0.078), (0.18, 0.061)]
    for i in range(6):
        a = math.radians(PH + 60 * i)
        rad = Vector((math.cos(a), math.sin(a), 0))
        rings = []
        for k, (z, r) in enumerate(prof):
            z0, r0 = prof[max(k - 1, 0)]
            z1, r1 = prof[min(k + 1, 2)]
            t = Vector((rad.x * (r1 - r0), rad.y * (r1 - r0), z1 - z0))
            rings.append((Vector((rad.x * r, rad.y * r, z)), t, rad, 0.014, 0.016))
        A.loft(rings, "Bronze", n=8, expo=7)
    # 橫框：下、中、上
    for (z, r, h) in [(0.046, 0.068, 0.012), (0.12, 0.073, 0.008), (0.178, 0.064, 0.014)]:
        A.lathe([(r - 0.012, z - h / 2), (r, z - h / 2), (r, z + h / 2), (r - 0.012, z + h / 2)], (0, 0, 0), "Bronze", segs=6, phase=PH)
    # 屋頂：外伸的簷＋六角錐
    A.lathe([(0.04, 0.185), (0.086, 0.188), (0.088, 0.198), (0.034, 0.247), (0.0, 0.25)], (0, 0, 0), "Bronze", segs=6, phase=PH,
            mats=["BronzeDark", "Bronze", "Bronze", "Bronze"])
    # 屋頂上的星形紋（前、左後、右後三面）
    r_m, z_m = 0.06, 0.219
    for k in (0, 2, 4):
        a = math.radians(-90 + 120 * k / 2 * 1.0)
        a = math.radians(-90 + 60 * k)
        radial = Vector((math.cos(a), math.sin(a), 0))
        apo = r_m * math.cos(math.radians(30))
        n = (radial * 0.05 + Vector((0, 0, 1)) * 0.0465).normalized()
        star(A, radial * apo + Vector((0, 0, z_m)) + n * 0.0015, n, Vector((0, 0, 1)), 0.016, "Gold")
    # 煙囪：小六角柱＋通氣孔＋尖蓋
    A.lathe([(0.0, 0.245), (0.027, 0.245), (0.027, 0.28), (0.0, 0.28)], (0, 0, 0), "Bronze", segs=6, phase=PH)
    for k in range(6):
        a = math.radians(-90 + 60 * k)
        radial = Vector((math.cos(a), math.sin(a), 0))
        A.cube((0.012, 0.004, 0.012), radial * 0.0235 + Vector((0, 0, 0.263)), "BronzeDark",
               rot=(0, 0, math.degrees(a) + 90), bevel=0.0)
    A.lathe([(0.0, 0.278), (0.036, 0.279), (0.036, 0.285), (0.006, 0.305), (0.0, 0.307)], (0, 0, 0), "Bronze", segs=6, phase=PH)
    # 提把的轉軸（左右兩側的圓頭螺栓）
    for sx in (-1, 1):
        A.cyl(0.013, 0.012, (sx * 0.084, 0, 0.207), "Bronze", rot=(0, 90, 0), segs=8, bevel=0.002)
        A.cyl(0.006, 0.006, (sx * 0.092, 0, 0.207), "Gold", rot=(0, 90, 0), segs=8)
    # 提把：一段一段的直桿拱起來（頂端是手握的地方）
    pts = [(-0.09, 0, 0.207), (-0.094, 0, 0.29), (-0.074, 0, 0.356), (-0.048, 0, 0.378), (0.048, 0, 0.378), (0.074, 0, 0.356),
           (0.094, 0, 0.29), (0.09, 0, 0.207)]
    for i in range(len(pts) - 1):
        bar(A, pts[i], pts[i + 1], 0.011, 0.009, "Bronze")
    ob = A.build(col)
    # 原點移到提把頂端中央（手握的位置）
    me = ob.data
    for v in me.vertices:
        v.co.z -= 0.378
    return ob


def finger(A, x, s, width, glove_to_tip=False):
    """左手一根手指：從指根繞過提把（X 軸）往下、往回收。前兩節戴手套，最後一節露出皮膚（無指手套）。"""
    K = Vector((x, -0.006, 0.036))
    P1 = Vector((x, 0.026 * s, 0.027))
    P2 = Vector((x, 0.031 * s, -0.005))
    T = Vector((x, 0.009, -0.026))
    up = (1, 0, 0)
    def seg(a, b, t0, t1, w0, w1, mat):
        A.loft([(a, b - a, up, t0, w0), (b, b - a, up, t1, w1)], mat, n=8, expo=2.2)
    seg(K - (P1 - K).normalized() * 0.006, P1, 0.021, 0.019, width + 0.002, width, "Glove")
    seg(P1, P2, 0.019, 0.017, width, width * 0.95, "Glove")
    seg(P2 - (P2 - P1).normalized() * 0.002, T, 0.016, 0.013, width * 0.92, width * 0.82, "Glove" if glove_to_tip else "Skin")


def build_arm_left(col):
    A = Asset("SQA_ArmLeft")
    # 手指（左手、手心朝下：拇指在 +X）
    finger(A, 0.0285, 0.95, 0.0205)
    finger(A, 0.0095, 1.0, 0.021)
    finger(A, -0.0095, 0.96, 0.0205)
    finger(A, -0.0275, 0.82, 0.0185)
    # 手背／手掌
    up = (0, 0, 1)
    A.loft([((0, -0.002, 0.031), (0, -1, 0), up, 0.088, 0.032), ((0, -0.045, 0.026), (0, -1, 0), up, 0.084, 0.044),
            ((0, -0.088, 0.022), (0, -1, 0), up, 0.064, 0.044)], "Glove", n=8, expo=3.0)
    A.cube((0.074, 0.05, 0.026), (0.004, -0.04, 0.004), "Glove", bevel=0.006)  # 手掌根
    # 指節的護套（手套上的一條皮革）
    A.cube((0.09, 0.016, 0.012), (0, -0.012, 0.047), "Strap", rot=(-14, 0, 0), bevel=0.003)
    # 拇指：從手掌側繞到前面壓住食指
    tp = [Vector((0.046, -0.058, 0.012)), Vector((0.056, -0.024, 0.002)), Vector((0.044, 0.006, -0.012)), Vector((0.026, 0.022, -0.017))]
    A.loft([(tp[0], tp[1] - tp[0], (0, 0, 1), 0.026, 0.024), (tp[1], tp[2] - tp[0], (0, 0, 1), 0.022, 0.021)], "Glove", n=8, expo=2.2)
    A.loft([(tp[1], tp[2] - tp[1], (0, 0, 1), 0.021, 0.02), (tp[2], tp[3] - tp[1], (0, 0, 1), 0.019, 0.019)], "Glove", n=8, expo=2.2)
    A.loft([(tp[2], tp[3] - tp[2], (0, 0, 1), 0.018, 0.018), (tp[3], tp[3] - tp[2], (0, 0, 1), 0.015, 0.015)], "Skin", n=8, expo=2.2)
    # 手腕：纏繞的皮帶兩圈
    for (y, tilt) in ((-0.083, 6), (-0.097, -5)):
        A.loft([((0, y + 0.006, 0.022), (0, -1, 0.0), (0, math.sin(math.radians(tilt)), 1), 0.07, 0.052),
                ((0, y - 0.006, 0.022), (0, -1, 0.0), (0, math.sin(math.radians(tilt)), 1), 0.07, 0.052)], "Strap", n=8, expo=2.4)
    # 前臂：手套的袖口＋米白色纏布袖子（下擺鋸齒）
    A.loft([((0, -0.09, 0.022), (0, -1, 0), up, 0.062, 0.048), ((0, -0.13, 0.02), (0, -1, 0), up, 0.07, 0.056)], "Glove", n=8, expo=2.4)
    vs = A.loft([((0, -0.112, 0.022), (0, -1, -0.05), up, 0.086, 0.07), ((0, -0.2, 0.012), (0, -1, -0.05), up, 0.094, 0.08),
                 ((0, -0.3, 0.0), (0, -1, -0.05), up, 0.1, 0.086), ((0, -0.42, -0.014), (0, -1, -0.05), up, 0.104, 0.09)],
                "Sleeve", n=10, expo=2.2)
    for i in range(10):
        v = vs[i]
        v.co.y += 0.012 if i % 2 == 0 else -0.003
    # 纏布：幾圈斜斜的布條
    # （纏布條試過做成凸出的幾何，第一人稱透視下像鋸齒，先拿掉；之後改用貼圖畫布紋）
    for (y, tilt, w) in ():
        rr = 1.0 + (-y - 0.11) * 0.06
        A.loft([((0, y + w / 2, 0.016 + y * 0.12), (0, -1, 0), (0, math.sin(math.radians(tilt)), 1), 0.093 * rr + 0.0025, 0.079 * rr + 0.0025),
                ((0, y - w / 2, 0.014 + y * 0.12), (0, -1, 0), (0, math.sin(math.radians(tilt)), 1), 0.093 * rr + 0.0025, 0.079 * rr + 0.0025)],
               "Sleeve", n=10, expo=2.2)
    return A.build(col)


def run_viewmodel():
    sc = bpy.data.scenes.get("StyleQA_Viewmodel") or bpy.data.scenes.new("StyleQA_Viewmodel")
    bpy.context.window.scene = sc
    for o in list(sc.collection.all_objects):
        bpy.data.objects.remove(o, do_unlink=True)
    report = []
    for fn, res in [(build_lantern, 1024), (build_arm_left, 1024)]:
        ob = fn(sc.collection)
        uv_unwrap(ob)
        bake(ob, res)
        export_fbx(ob)
        me = ob.data
        report.append("%s: %d verts, %d tris, %d mats, tex %d" % (ob.name, len(me.vertices), sum(len(p.vertices) - 2 for p in me.polygons), len(me.materials), res))
    if globals().get("OUT_BLEND"):
        for img in bpy.data.images:
            if img.name.startswith("SQA_") and not img.packed_file:
                img.pack()
        bpy.data.libraries.write(OUT_BLEND, {sc} | set(sc.collection.all_objects), fake_user=True, compress=True)
    return report


print("\n".join(run_viewmodel()))
