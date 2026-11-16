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
    "Nail":        ("#E9C2A6", "#A8786A", 6.0, 0.03, 0.10),
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


BAIL_W, BAIL_H = 0.015, 0.012   # 提把斷面（高×深）：比最早的 11×9 mm 粗，又剛好放進握拳的空間


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
        A.cyl(0.017, 0.014, (sx * 0.084, 0, 0.207), "Bronze", rot=(0, 90, 0), segs=8, bevel=0.002)
        A.cyl(0.008, 0.007, (sx * 0.093, 0, 0.207), "Gold", rot=(0, 90, 0), segs=8)
    # 提把：一段一段的直桿拱起來（頂端是手握的地方）
    # 頂端的平直段加寬到 ±6.2 cm：整個拳頭（含拇指）都握在平直段上，不會碰到往下彎的那兩段。
    pts = [(-0.09, 0, 0.207), (-0.097, 0, 0.29), (-0.086, 0, 0.352), (-0.062, 0, 0.378), (0.062, 0, 0.378), (0.086, 0, 0.352),
           (0.097, 0, 0.29), (0.09, 0, 0.207)]
    for i in range(len(pts) - 1):
        bar(A, pts[i], pts[i + 1], BAIL_W, BAIL_H, "Bronze")   # 提把加粗（使用者回饋：原本太細）
    ob = A.build(col)
    # 原點移到提把頂端中央（手握的位置）
    me = ob.data
    for v in me.vertices:
        v.co.z -= 0.378
    return ob


def _v(x, y, z):
    return Vector((x, y, z))


# 手的比例（成人男性，公尺）：近節 ≈ 中節＋遠節；長度 中指 > 無名指 > 食指 > 小指。
# 手的座標：X＝指節連線（左手的拇指在 +X），Y＝手指伸直時的方向，Z＝手背朝上。
FINGERS = [
    # 名稱, 指根關節(MCP), 長度比例, 寬, 厚, 外展角(度，正＝往拇指側)
    ("index",  (0.0265, -0.004, 0.000), 0.93, 0.0190, 0.0170, 3.0),
    ("middle", (0.0075,  0.000, 0.002), 1.00, 0.0195, 0.0175, 0.5),
    ("ring",   (-0.0115, -0.004, 0.000), 0.96, 0.0185, 0.0165, -2.0),
    ("little", (-0.0290, -0.009, -0.003), 0.84, 0.0160, 0.0145, -5.0),
]
PHALANX = (0.047, 0.028, 0.021)   # 中指：近節、中節、遠節

POSES = {
    # 每根手指 (MCP, PIP, DIP) 彎曲角度
    # 握提把：每根手指的角度是用程式搜尋出來的——手指中心線離提把中心至少「提把斷面半對角線＋手指厚度一半＋0.8 mm」，
    # 在這個條件下繞得越緊越好（不穿模、看起來真的握住）。
    "grip":    {"index": (42, 94, 32), "middle": (52, 96, 36), "ring": (44, 96, 36), "little": (32, 86, 46)},
    "relaxed": {"index": (30, 48, 24), "middle": (36, 56, 28), "ring": (42, 62, 30), "little": (48, 66, 32)},
}


def finger_chain(mcp, scale, yaw, angles):
    """回傳 [(關節位置, 方向, 手背方向), ...]：MCP、PIP、DIP、指尖。"""
    a = math.radians(yaw)
    pts = []
    p = Vector(mcp)
    theta = 0.0
    lengths = [PHALANX[0] * scale, PHALANX[1] * scale, PHALANX[2] * scale]
    for k in range(3):
        theta += math.radians(angles[k])
        d = Vector((math.sin(a) * math.cos(theta), math.cos(a) * math.cos(theta), -math.sin(theta)))
        up = Vector((math.sin(a) * math.sin(theta), math.cos(a) * math.sin(theta), math.cos(theta)))
        pts.append((p.copy(), d, up))
        p = p + d * lengths[k]
    pts.append((p.copy(), pts[-1][1], pts[-1][2]))
    return pts


def build_finger(A, s, mcp, scale, w, h, yaw, angles, glove_frac=0.5):
    """一根手指＝一條連續的管子（關節處稍微鼓起），指尖收圓；近節的前半段戴手套，之後露出皮膚，加指甲。"""
    mcp = Vector((mcp[0] * s, mcp[1], mcp[2]))
    ch = finger_chain(mcp, scale, yaw * s, angles)
    (P0, d1, u1), (P1, d2, u2), (P2, d3, u3), (T, _, _) = ch
    def lerp(a, b, f): return a + (b - a) * f
    def avg(a, b): return (a + b).normalized()
    Lp = (P1 - P0).length
    skin = [
        (lerp(P0, P1, glove_frac - 0.12), d1, u1, w * 0.97, h * 0.95),
        (P1, avg(d1, d2), avg(u1, u2), w * 1.02, h * 1.0),
        (lerp(P1, P2, 0.5), d2, u2, w * 0.93, h * 0.9),
        (P2, avg(d2, d3), avg(u2, u3), w * 0.92, h * 0.88),
        (lerp(P2, T, 0.55), d3, u3, w * 0.87, h * 0.83),
        (lerp(P2, T, 0.86), d3, u3, w * 0.74, h * 0.7),
        (T, d3, u3, w * 0.36, h * 0.32),
    ]
    A.loft(skin, "Skin", n=10, expo=2.3)
    g = 1.07
    glove = [
        (P0 - d1 * 0.014, d1, u1, w * 1.06 * g, h * 1.15 * g),
        (P0, d1, u1, w * 1.08 * g, h * 1.06 * g),
        (lerp(P0, P1, glove_frac), d1, u1, w * 0.99 * g, h * 0.97 * g),
    ]
    A.loft(glove, "Glove", n=10, expo=2.3)
    # 指甲：遠節手背側的一小片
    c = lerp(P2, T, 0.58) + u3 * (h * 0.83 * 0.5 - 0.0004)
    side = d3.cross(u3).normalized()
    nail = []
    for (x, y) in ((-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5)):
        nail.append(c + side * x * w * 0.55 + d3 * y * 0.011)
    top = [A.bm.verts.new(p + u3 * 0.0012) for p in nail]
    bot = [A.bm.verts.new(p) for p in nail]
    idx = A.mi("Nail")
    A.bm.faces.new(top).material_index = idx
    A.bm.faces.new(list(reversed(bot))).material_index = idx
    for i in range(4):
        j = (i + 1) % 4
        A.bm.faces.new((bot[i], bot[j], top[j], top[i])).material_index = idx
    return ch


def circumcenter_yz(a, b, c):
    ax, ay = a.y, a.z; bx, by = b.y, b.z; cx, cy = c.y, c.z
    d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by))
    ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d
    uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d
    return ux, uy


WRIST_BEND = None   # 前臂改成獨立物件（執行時轉向），不在模型裡彎


def build_arm(col, side="left", pose="grip"):
    """
    第一人稱的手與前臂。s＝+1 左手（拇指在 +X）、-1 右手。
    原點＝握住的提把（沿 X 軸）中心（grip 姿勢），或手心前方（relaxed 姿勢）。手指往 +Y、手背朝 +Z、前臂往 -Y。
    平滑著色（手是有機的形體，用 shader 的三段明暗做出插畫的色塊；家具才用平面切面）。
    """
    s = 1 if side == "left" else -1
    A = Asset("SQA_Arm" + side.capitalize())
    A.smooth = True
    chains = {}
    for (name, mcp, sc, w, h, yaw) in FINGERS:
        chains[name] = build_finger(A, s, mcp, sc, w, h, yaw, POSES[pose][name])
    up = (0, 0, 1)
    # 手背／手掌（戴手套）：指節那一端稍微拱起，往手腕收窄、變厚
    A.loft([((0.000 * s, 0.006, -0.004), (0, -1, 0), up, 0.084, 0.026),
            ((0.001 * s, -0.012, -0.004), (0, -1, 0), up, 0.088, 0.032),
            ((0.002 * s, -0.048, -0.005), (0, -1, 0), up, 0.088, 0.036),
            ((0.002 * s, -0.080, -0.006), (0, -1, 0), up, 0.075, 0.040),
            ((0.000 * s, -0.100, -0.004), (0, -1, 0), up, 0.060, 0.040)], "Glove", n=14, expo=2.5)
    # 指節上的皮革護套
    A.loft([((-0.034 * s, -0.008, 0.012), (s, 0, 0), (0, 0.25, 1), 0.016, 0.006),
            ((0.0, -0.004, 0.015), (s, 0, 0), (0, 0.25, 1), 0.018, 0.007),
            ((0.034 * s, -0.008, 0.012), (s, 0, 0), (0, 0.25, 1), 0.016, 0.006)], "Strap", n=8, expo=2.2)
    # 大魚際（拇指根部的肌肉）
    A.loft([((0.026 * s, -0.092, -0.014), (0.25 * s, 1, -0.1), up, 0.028, 0.026),
            ((0.036 * s, -0.062, -0.019), (0.2 * s, 1, -0.15), up, 0.032, 0.030),
            ((0.034 * s, -0.034, -0.021), (0.0, 1, -0.2), up, 0.026, 0.024)], "Glove", n=10, expo=2.2)
    # 拇指：從手腕旁邊長出來，掌骨（戴手套）→ 近節（手套）→ 遠節（皮膚）
    ix = chains["index"]
    (I0, _, _), (I1, _, _), (I2, di2, ui2), _ = ix
    M = (I1 + I2) / 2
    if pose == "grip":
        # 握拳：拇指從下面繞上來，指尖壓在食指中節的外側
        tip = M + Vector((0.004 * s, 0.004, -0.013))
    else:
        tip = I0 + Vector((0.016 * s, 0.026, -0.022))
    cmc = Vector((0.030 * s, -0.082, -0.010))
    mcpT = Vector((0.046 * s, -0.050, -0.022))
    ip = (mcpT + tip) / 2 + Vector((0.006 * s, 0.002, -0.010))
    def tube(pts, ws, hs, mat):
        rings = []
        for k, p in enumerate(pts):
            a = pts[max(k - 1, 0)]; b = pts[min(k + 1, len(pts) - 1)]
            t = (b - a)
            rings.append((p, t, Vector((0, 0, 1)) if abs(t.normalized().z) < 0.9 else Vector((s, 0, 0)), ws[k], hs[k]))
        A.loft(rings, mat, n=10, expo=2.3)
    tube([cmc, mcpT, (mcpT + ip) / 2], [0.030, 0.026, 0.022], [0.026, 0.023, 0.020], "Glove")
    tube([(mcpT + ip) / 2 * 0.6 + mcpT * 0.4, ip, (ip + tip) / 2, tip + (tip - ip).normalized() * 0.002],
         [0.0205, 0.0205, 0.019, 0.010], [0.0185, 0.0185, 0.0165, 0.009], "Skin")
    # 手腕：兩圈貼著的皮帶
    for (y, tilt) in ((-0.094, 5), (-0.106, -4)):
        u = (0, math.sin(math.radians(tilt)), 1)
        A.loft([((0, y + 0.005, -0.004), (0, -1, 0), u, 0.0625, 0.0425), ((0, y - 0.005, -0.004), (0, -1, 0), u, 0.0625, 0.0425)],
               "Strap", n=12, expo=2.3)
    # 手套袖口（短短一截，前臂的袖子會蓋住它；前臂是另一個物件，執行時自己轉向畫面左下角）
    A.loft([((0, -0.098, -0.004), (0, -1, 0), up, 0.06, 0.041), ((0, -0.116, -0.005), (0, -1, 0), up, 0.063, 0.045)], "Glove", n=12, expo=2.3)
    # 原點：握把中心（中指 MCP、PIP、DIP 三點的外接圓圓心）
    (m0, _, _), (m1, _, _), (m2, _, _), _ = chains["middle"]
    cy, cz = circumcenter_yz(m0, m1, m2)
    r_center = sum(((p - Vector((p.x, cy, cz))).length for p in (m0, m1, m2))) / 3
    if pose == "grip":
        # 實際算過：手指和手掌圍出來的空間，最大內切圓在 (y 0, z -0.030)，半徑約 0.009 m，
        # 剛好握住加粗後的提把（斷面 16×13 mm）。
        cy, cz = 0.0, -0.030
        r_center = 0.009 + 0.0175 / 2
    else:
        cy, cz = 0.02, -0.03
    bmesh.ops.translate(A.bm, verts=A.bm.verts, vec=Vector((0, -cy, -cz)))
    ob = A.build(col)
    ob["grip_inner_radius"] = r_center - 0.0175 / 2
    print("%s: grip center (%.4f, %.4f), finger-centre radius %.4f, inner radius %.4f" % (ob.name, cy, cz, r_center, r_center - 0.0175 / 2))
    return ob


WRIST_PIVOT = Vector((0.0, -0.100, -0.004))   # 手腕的轉軸（手的座標，平移到握把中心之前）


def build_forearm(col, side="left"):
    """
    前臂（米白色袖子）：和手分開，原點在手腕。Unity 裡讓手保持掌心朝下握著提把，
    前臂另外朝畫面左下角（右手是右下角）伸出去——不管低頭抬頭，前臂都從畫面邊緣進來，不會擋在提燈旁邊。
    """
    s = 1 if side == "left" else -1
    F = Asset("SQA_Forearm" + side.capitalize())
    F.smooth = True
    up = (0, 0, 1)
    vs = F.loft([((0, -0.108, -0.005), (0, -1, -0.03), up, 0.084, 0.07), ((0, -0.17, -0.007), (0, -1, -0.03), up, 0.088, 0.076),
                 ((0, -0.25, -0.01), (0, -1, -0.03), up, 0.094, 0.082), ((0, -0.36, -0.013), (0, -1, -0.03), up, 0.1, 0.088),
                 ((0, -0.48, -0.016), (0, -1, -0.03), up, 0.104, 0.092)], "Sleeve", n=12, expo=2.2)
    for i in range(12):
        vs[i].co.y += 0.012 if i % 2 == 0 else -0.002   # 鋸齒袖口
    bmesh.ops.translate(F.bm, verts=F.bm.verts, vec=-WRIST_PIVOT)
    return F.build(col)


def build_forearm_left(col):
    return build_forearm(col, "left")


def build_forearm_right(col):
    return build_forearm(col, "right")


def build_arm_left(col):
    return build_arm(col, "left", "grip")


def build_arm_right(col):
    return build_arm(col, "right", "relaxed")


def run_viewmodel():
    sc = bpy.data.scenes.get("StyleQA_Viewmodel") or bpy.data.scenes.new("StyleQA_Viewmodel")
    bpy.context.window.scene = sc
    for o in list(sc.collection.all_objects):
        bpy.data.objects.remove(o, do_unlink=True)
    report = []
    for fn, res in [(build_lantern, 1024), (build_arm_left, 1024), (build_arm_right, 1024), (build_forearm_left, 512), (build_forearm_right, 512)]:
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


if globals().get("SQA_VM_LIBRARY") is not True:   # 調整握拳角度時只借用函式、不烘焙
    print("\n".join(run_viewmodel()))
