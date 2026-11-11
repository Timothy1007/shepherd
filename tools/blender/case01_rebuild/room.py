"""Case01 全屋重製 v2：新客廳（前廳）——從新平面重新建模，不沿用舊 R3 建築或舊家具。

座標（Blender，公尺）：x＝房間寬（0 → 3.6，x=0 是與 15 號相鄰的共用牆／壁爐牆），y＝深（0＝臨街前牆，4.2＝後牆），z＝高（0 → 2.75）。
走廊在 x>3.6 一側（寬 1.05 m）；客廳門在 x=3.6 牆、y 3.15～3.97（82 cm 淨寬，往客廳內開、半掩）。
物件命名：A_ 建築（Unity 端加 MeshCollider）、F_ 家具（加 BoxCollider）、D_ 小物（無碰撞）、E_ 空物件（燈、出生點、照片、機位）。
材質只命名（RB_*），貼圖由 pbr.py 產生；Unity 端與 Blender 參考算圖都用同一組貼圖。
"""
import math
import os
import importlib.util

import bmesh
import bpy
from mathutils import Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("lr_models", os.path.join(HERE, "..", "case01_living_room", "models.py"))
lr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lr)
Part = lr.Part

W, D, H = 3.6, 4.2, 2.75
HALL = 1.05
T_PARTY, T_FRONT, T_INNER = 0.23, 0.3, 0.12
DOOR_Y0, DOOR_Y1, DOOR_H = 3.15, 3.97, 2.03
WIN_X0, WIN_X1, WIN_Z0, WIN_Z1 = 1.22, 2.38, 0.72, 2.42
CB_Y0, CB_Y1, CB_D = 1.35, 2.85, 0.36      # 壁爐凸牆（chimney breast）
PICTURE_RAIL = 2.12

for k, v in {"RB_Floor": 1.0, "RB_Wallpaper": 0.53, "RB_Paint": 1.0, "RB_Ceiling": 1.0, "RB_Trim": 0.6, "RB_Mantel": 0.7, "RB_Oak": 0.7,
             "RB_Velvet": 0.6, "RB_Tweed": 0.5, "RB_Tile": 0.15, "RB_HearthTile": 0.3, "RB_CastIron": 0.5, "RB_Brass": 0.3, "RB_Gilt": 0.3,
             "RB_Radiator": 0.5, "RB_Linen": 0.4, "RB_Plaid": 0.7, "RB_Curtain": 0.8, "RB_Shade": 0.5, "RB_Ceramic": 0.4, "RB_Cushion": 0.4,
             "RB_Glass": 1.0, "RB_Mirror": 1.0, "RB_Screen": 1.0, "RB_Plastic": 1.0, "RB_Coal": 0.3, "RB_Paper": 0.3, "RB_Leaf": 0.2, "RB_Books": 1.0}.items():
    lr.TEX_METERS[k] = v

OBJS = []


def done(part, sharp=38.0):
    ob = lr.finish(part, sharp)
    OBJS.append(ob)
    return ob


def box(p, c, s, m, bevel=0.0, seg=2, rot=None):
    lr.box(p, c, s, m, bevel=bevel, segments=seg, rot=rot)


def faces_since(p, n0):
    p.bm.faces.ensure_lookup_table()
    return [p.bm.faces[i] for i in range(n0, len(p.bm.faces))]


def set_uv_rect(p, faces, u0, v0, u1, v1, axis_u=0, axis_v=2):
    """把一組面的 UV 映射到圖集裡的一格（書脊、照片）。"""
    if not faces:
        return
    xs = [l.vert.co[axis_u] for f in faces for l in f.loops]
    ys = [l.vert.co[axis_v] for f in faces for l in f.loops]
    a0, a1, b0, b1 = min(xs), max(xs), min(ys), max(ys)
    for f in faces:
        for l in f.loops:
            u = (l.vert.co[axis_u] - a0) / max(1e-6, a1 - a0)
            v = (l.vert.co[axis_v] - b0) / max(1e-6, b1 - b0)
            l[p.uv].uv = (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)
        f[p.fix] = 1


def empty(name, loc, rot=(0, 0, 0)):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    e.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.collection.objects.link(e)
    OBJS.append(e)
    return e


# ---------------------------------------------------------------- 建築

def profile_run(p, prof, a, b, mat, inward):
    """沿牆面從 a 走到 b 的線腳（prof＝[(離牆距離, 高度)…]），inward＝朝室內的單位向量（水平）。"""
    a, b = Vector(a), Vector(b)
    d = (b - a)
    L = d.length
    dirv = d.normalized()
    bm = p.bm
    ra, rb = [], []
    for (u, z) in prof:
        off = Vector(inward) * u
        ra.append(bm.verts.new(a + off + Vector((0, 0, z))))
        rb.append(bm.verts.new(b + off + Vector((0, 0, z))))
    faces = []
    for i in range(len(prof) - 1):
        faces.append(bm.faces.new((ra[i], rb[i], rb[i + 1], ra[i + 1])))
    faces.append(bm.faces.new((ra[-1], rb[-1], rb[0], ra[0])))
    faces.append(bm.faces.new(ra[::-1]))
    faces.append(bm.faces.new(rb))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    lr._set_faces(p, faces, mat, True)
    # 端面朝外的法線修正：讓整條線腳的面朝室內。
    return faces


SKIRT = [(0.0, 0.0), (0.022, 0.0), (0.022, 0.12), (0.016, 0.13), (0.019, 0.145), (0.012, 0.165), (0.006, 0.175), (0.0, 0.18)]
RAIL = [(0.0, -0.025), (0.008, -0.025), (0.022, -0.012), (0.024, 0.0), (0.018, 0.014), (0.006, 0.02), (0.0, 0.02)]
CORNICE = [(0.0, -0.16), (0.012, -0.155), (0.02, -0.135), (0.045, -0.11), (0.08, -0.07), (0.11, -0.03), (0.13, -0.012), (0.145, 0.0), (0.0, 0.0)]


def walls():
    wp = Part("A_Walls")
    pt = Part("A_WallsUpper")
    # 每面牆兩層：picture rail 以下壁紙、以上舊漆（同一面以 z 切開）。
    def wall_piece(c, s):
        z0, z1 = c[2] - s[2] / 2, c[2] + s[2] / 2
        if z1 <= PICTURE_RAIL:
            box(wp, c, s, "RB_Wallpaper")
        elif z0 >= PICTURE_RAIL:
            box(pt, c, s, "RB_Paint")
        else:
            lo = PICTURE_RAIL - z0
            box(wp, (c[0], c[1], z0 + lo / 2), (s[0], s[1], lo), "RB_Wallpaper")
            hi = z1 - PICTURE_RAIL
            box(pt, (c[0], c[1], PICTURE_RAIL + hi / 2), (s[0], s[1], hi), "RB_Paint")

    # 壁爐牆（x=0）：凸牆兩側的凹室與凸牆本身。
    wall_piece((-T_PARTY / 2, D / 2, H / 2), (T_PARTY, D + 2 * T_FRONT, H))
    wall_piece((CB_D / 2, (CB_Y0 + CB_Y1) / 2, H / 2), (CB_D, CB_Y1 - CB_Y0, H))
    # 對面（x=W，走廊牆，含門洞）。
    xw = W + T_INNER / 2
    wall_piece((xw, DOOR_Y0 / 2, H / 2), (T_INNER, DOOR_Y0, H))
    wall_piece((xw, (DOOR_Y1 + D) / 2, H / 2), (T_INNER, D - DOOR_Y1, H))
    wall_piece((xw, (DOOR_Y0 + DOOR_Y1) / 2, (DOOR_H + H) / 2), (T_INNER, DOOR_Y1 - DOOR_Y0, H - DOOR_H))
    # 後牆（y=D）。
    wall_piece((W / 2, D + T_INNER / 2, H / 2), (W + 2 * T_INNER, T_INNER, H))
    # 前牆（y=0，含窗洞與窗台下的牆）。
    yf = -T_FRONT / 2
    wall_piece((WIN_X0 / 2, yf, H / 2), (WIN_X0, T_FRONT, H))
    wall_piece(((WIN_X1 + W) / 2, yf, H / 2), (W - WIN_X1, T_FRONT, H))
    wall_piece(((WIN_X0 + WIN_X1) / 2, yf, WIN_Z0 / 2), (WIN_X1 - WIN_X0, T_FRONT, WIN_Z0))
    wall_piece(((WIN_X0 + WIN_X1) / 2, yf, (WIN_Z1 + H) / 2), (WIN_X1 - WIN_X0, T_FRONT, H - WIN_Z1))
    done(wp, 30)
    done(pt, 30)
    # 地板：一塊一塊的板（長度不一、錯縫、微小高低差），板與板之間留縫。
    fp = Part("A_Floor")
    import random
    rnd = random.Random(7)
    bw = 0.14
    x = 0.0
    row = 0
    while x < W - 0.01:
        y = -rnd.random() * 1.2
        while y < D:
            L = 0.9 + rnd.random() * 1.8
            y0, y1 = max(0.0, y), min(D, y + L)
            if y1 - y0 > 0.05:
                dz = rnd.random() * 0.002
                n0 = len(fp.bm.faces)
                box(fp, (x + bw / 2, (y0 + y1) / 2, -0.011 + dz), (bw - 0.004, y1 - y0 - 0.003, 0.022), "RB_Floor", bevel=0.0015, seg=1)
                # 每塊板取貼圖的不同位置（顏色與年輪不重複）。
                fs = faces_since(fp, n0)
                ou, ov = rnd.random(), rnd.random()
                for f in fs:
                    for l in f.loops:
                        co = l.vert.co
                        l[fp.uv].uv = (co.y / 1.0 + ov, (co.x - x) / 1.0 * 1.0 + (row % 7) / 7.0)
                    f[fp.fix] = 1
            y += L
        x += bw
        row += 1
    done(fp, 50)
    # 天花板與灰泥線腳、天花圓花。
    cp = Part("A_Ceiling")
    box(cp, (W / 2, D / 2, H + 0.05), (W, D, 0.1), "RB_Ceiling")
    done(cp)
    tr = Part("A_Mouldings")
    runs = [((0.0, 0.0, 0.0), (0.0, CB_Y0, 0.0), (1, 0, 0)), ((CB_D, CB_Y0, 0.0), (CB_D, CB_Y1, 0.0), (1, 0, 0)), ((0.0, CB_Y1, 0.0), (0.0, D, 0.0), (1, 0, 0)),
            ((0.0, CB_Y0, 0.0), (CB_D, CB_Y0, 0.0), (0, -1, 0)), ((CB_D, CB_Y1, 0.0), (0.0, CB_Y1, 0.0), (0, 1, 0)),
            ((0.0, D, 0.0), (W, D, 0.0), (0, -1, 0)), ((W, D, 0.0), (W, DOOR_Y1, 0.0), (-1, 0, 0)), ((W, DOOR_Y0, 0.0), (W, 0.0, 0.0), (-1, 0, 0)),
            ((W, 0.0, 0.0), (0.0, 0.0, 0.0), (0, 1, 0))]
    for a, b, n in runs:
        profile_run(tr, SKIRT, a, b, "RB_Trim", n)
        a2, b2 = Vector(a) + Vector((0, 0, PICTURE_RAIL)), Vector(b) + Vector((0, 0, PICTURE_RAIL))
        if not (abs(a[1]) < 1e-3 and abs(b[1]) < 1e-3):
            profile_run(tr, RAIL, a2, b2, "RB_Trim", n)
        else:
            # 前牆的掛畫線在窗洞處斷開。
            profile_run(tr, RAIL, a2, Vector((WIN_X1 + 0.08, 0, PICTURE_RAIL)), "RB_Trim", n)
            profile_run(tr, RAIL, Vector((WIN_X0 - 0.08, 0, PICTURE_RAIL)), b2, "RB_Trim", n)
        profile_run(tr, CORNICE, Vector(a) + Vector((0, 0, H)), Vector(b) + Vector((0, 0, H)), "RB_Ceiling", n)
    # 去掉門洞處的踢腳線（上面的 runs 已經在門洞斷開）。
    done(tr, 50)
    rose = Part("D_CeilingRose")
    lr.lathe(rose, (W / 2, D / 2 - 0.1, H - 0.045), [(0.0, 0.045), (0.24, 0.045), (0.235, 0.035), (0.2, 0.03), (0.19, 0.022), (0.15, 0.02), (0.12, 0.01), (0.07, 0.0), (0.0, -0.01)], "RB_Ceiling", steps=48)
    for i in range(16):
        a = i * math.pi * 2 / 16
        box(rose, (W / 2 + math.cos(a) * 0.17, D / 2 - 0.1 + math.sin(a) * 0.17, H - 0.022), (0.05, 0.022, 0.012), "RB_Ceiling", bevel=0.004, rot=(0, 0, math.degrees(a)))
    done(rose)


def window():
    """上下拉窗：窗框、上下兩扇（各 2×2 格、窗角凸耳）、內側窗台板、窗洞側板、窗簾桿與窗簾、窗下的鑄鐵暖氣片。"""
    p = Part("A_Window")
    T = "RB_Trim"
    w, h = WIN_X1 - WIN_X0, WIN_Z1 - WIN_Z0
    cx = (WIN_X0 + WIN_X1) / 2
    yb = -T_FRONT + 0.08
    # 窗洞側板（內側）。
    for x in (WIN_X0 + 0.01, WIN_X1 - 0.01):
        box(p, (x, -T_FRONT / 2 + 0.04, (WIN_Z0 + WIN_Z1) / 2), (0.02, T_FRONT - 0.08, h), T)
    box(p, (cx, -T_FRONT / 2 + 0.04, WIN_Z1 - 0.01), (w, T_FRONT - 0.08, 0.02), T)
    # 窗台板（凸出牆面、圓邊）與下方飾條。
    box(p, (cx, -T_FRONT / 2 + 0.06, WIN_Z0 - 0.015), (w + 0.16, T_FRONT + 0.03, 0.03), T, bevel=0.008)
    box(p, (cx, 0.012, WIN_Z0 - 0.05), (w + 0.08, 0.02, 0.04), T, bevel=0.004)
    # 外框。
    fw = 0.06
    for x in (WIN_X0 + fw / 2 + 0.02, WIN_X1 - fw / 2 - 0.02):
        box(p, (x, yb, (WIN_Z0 + WIN_Z1) / 2), (fw, 0.09, h), T, bevel=0.004)
    box(p, (cx, yb, WIN_Z1 - fw / 2 - 0.02), (w - 0.04, 0.09, fw), T, bevel=0.004)
    box(p, (cx, yb, WIN_Z0 + 0.03), (w - 0.04, 0.1, 0.05), T, bevel=0.004)
    # 兩扇窗：上扇在外、下扇在內，各自的窗框、中梃與窗格條。
    inner_w = w - 0.04 - 2 * fw
    sh = (h - 0.05 - fw) / 2
    for i, (y, z0) in enumerate([(yb - 0.025, WIN_Z0 + 0.055), (yb + 0.025, WIN_Z0 + 0.055 + sh - 0.02)]):
        zc = z0 + sh / 2
        for x in (cx - inner_w / 2 + 0.025, cx + inner_w / 2 - 0.025):
            box(p, (x, y, zc), (0.05, 0.04, sh), T, bevel=0.003)
        box(p, (cx, y, z0 + 0.025), (inner_w, 0.04, 0.05), T, bevel=0.003)
        box(p, (cx, y, z0 + sh - 0.02), (inner_w, 0.04, 0.04), T, bevel=0.003)
        box(p, (cx, y, zc), (0.022, 0.03, sh - 0.06), T, bevel=0.002)
        box(p, (cx, y, zc), (inner_w - 0.06, 0.03, 0.022), T, bevel=0.002)
        if i == 1:
            for x in (cx - inner_w / 2 + 0.025, cx + inner_w / 2 - 0.025):
                box(p, (x, y, z0 - 0.04), (0.04, 0.035, 0.08), T, bevel=0.01)
    box(p, (cx, yb, WIN_Z0 + 0.055 + sh), (0.08, 0.02, 0.015), "RB_Brass", bevel=0.003)
    done(p)
    g = Part("D_WindowGlass")
    box(g, (cx, yb - 0.025, (WIN_Z0 + WIN_Z1) / 2), (inner_w - 0.04, 0.004, h - 0.12), "RB_Glass")
    box(g, (cx, yb + 0.025, (WIN_Z0 + WIN_Z1) / 2), (inner_w - 0.04, 0.004, h - 0.12), "RB_Glass")
    done(g)
    # 窗簾桿與窗簾（帶褶、下擺垂地、左右收攏）。
    cp = Part("D_CurtainPole")
    lr.extrude_profile(cp, [(math.cos(a) * 0.014, math.sin(a) * 0.014) for a in [i * math.pi / 6 for i in range(12)]], w + 0.6, "RB_Brass", axis='x', origin=(WIN_X0 - 0.3, 0.08, WIN_Z1 + 0.14), smooth=True)
    for x in (WIN_X0 - 0.31, WIN_X1 + 0.31):
        lr.lathe(cp, (x, 0.08, WIN_Z1 + 0.14 - 0.03), [(0.0, 0.0), (0.03, 0.01), (0.035, 0.03), (0.02, 0.05), (0.0, 0.06)], "RB_Brass", steps=14)
    done(cp)
    for side, x0 in ((-1, WIN_X0 - 0.3), (1, WIN_X1 + 0.3)):
        cu = Part("D_Curtain" + ("L" if side < 0 else "R"))
        width = 0.5

        def fn(u, v, x0=x0, side=side, width=width):
            gather = 1.0 - 0.45 * max(0.0, (0.55 - v)) / 0.55 * 0.0
            x = x0 + side * -1 * u * width
            folds = math.sin(u * math.pi * 9) * 0.035 + math.sin(u * math.pi * 23 + v * 2) * 0.006
            tie = math.exp(-((v - 0.62) / 0.12) ** 2) * 0.12 * u
            return (x + side * tie, 0.1 + folds + 0.02 * v, WIN_Z1 + 0.11 - v * (WIN_Z1 + 0.11 - 0.01))
        lr.grid_sheet(cu, width, 2.5, 26, 30, fn, "RB_Curtain", thickness=0.006)
        done(cu, 70)
    # 窗下的鑄鐵柱式暖氣片（16 片、上下圓角、閥門與管）。
    rp = Part("F_Radiator")
    n = 16
    rx0 = cx - n * 0.06 / 2
    for i in range(n):
        x = rx0 + i * 0.06 + 0.03
        for yy in (0.07, 0.15):
            lr.rounded_box(rp, (x, yy, 0.38), (0.045, 0.05, 0.56), 0.022, "RB_Radiator", cuts=3)
        box(rp, (x, 0.11, 0.62), (0.045, 0.1, 0.035), "RB_Radiator", bevel=0.01)
        box(rp, (x, 0.11, 0.14), (0.045, 0.1, 0.035), "RB_Radiator", bevel=0.01)
    for x in (rx0 - 0.03, rx0 + n * 0.06 + 0.03):
        lr.lathe(rp, (x, 0.11, 0.0), [(0.012, 0.0), (0.012, 0.12), (0.022, 0.13), (0.022, 0.17), (0.012, 0.18)], "RB_Brass", steps=12)
    for x in (rx0 - 0.01, rx0 + n * 0.06 + 0.01):
        for z in (0.14, 0.62):
            lr.lathe(rp, (x, 0.11, z - 0.02), [(0.018, 0.0), (0.018, 0.04)], "RB_Radiator", steps=12)
    done(rp)


def door():
    """四片嵌板門（往客廳內開、半掩 35°）＋門框與門頭線、黃銅門把；門外是一小段走廊（磁磚、壁紙、遠處的樓梯起步）。"""
    T = "RB_Trim"
    ar = Part("A_DoorCasing")
    ARC = [(0.0, 0.0), (0.012, 0.0), (0.02, 0.01), (0.018, 0.03), (0.012, 0.05), (0.008, 0.07), (0.0, 0.075)]
    # 門框（門洞側板）。
    for y in (DOOR_Y0, DOOR_Y1):
        box(ar, (W + T_INNER / 2, y + (0.012 if y == DOOR_Y0 else -0.012), DOOR_H / 2), (T_INNER + 0.02, 0.024, DOOR_H), T)
    box(ar, (W + T_INNER / 2, (DOOR_Y0 + DOOR_Y1) / 2, DOOR_H - 0.012), (T_INNER + 0.02, DOOR_Y1 - DOOR_Y0, 0.024), T)
    # 門頭線與兩側門套線（室內那面）：沿門洞外緣的線腳。
    for y, n in ((DOOR_Y0 - 0.075, (-1, 0, 0)), (DOOR_Y1, (-1, 0, 0))):
        pass
    for (a, b) in (((W, DOOR_Y0, 0), (W, DOOR_Y0, DOOR_H + 0.075)), ((W, DOOR_Y1, 0), (W, DOOR_Y1, DOOR_H + 0.075))):
        n0 = len(ar.bm.faces)
        box(ar, (W - 0.012, a[1] + (-0.0375 if a[1] == DOOR_Y0 else 0.0375), (DOOR_H + 0.075) / 2), (0.024, 0.075, DOOR_H + 0.075), T, bevel=0.006)
    box(ar, (W - 0.012, (DOOR_Y0 + DOOR_Y1) / 2, DOOR_H + 0.0375), (0.024, DOOR_Y1 - DOOR_Y0 + 0.15, 0.075), T, bevel=0.006)
    for y in (DOOR_Y0 - 0.0375, DOOR_Y1 + 0.0375):
        box(ar, (W - 0.016, y, 0.11), (0.032, 0.09, 0.22), T, bevel=0.005)
    done(ar)
    # 門扇（在原點建立，再轉到門軸位置）。
    dp = Part("F_Door")
    dw, dt = DOOR_Y1 - DOOR_Y0 - 0.006, 0.044
    box(dp, (0, dw / 2, DOOR_H / 2 - 0.004), (dt, dw, DOOR_H - 0.012), T, bevel=0.004)
    for (zc, hh) in ((0.5, 0.62), (1.42, 0.84)):
        for yc in (dw * 0.27, dw * 0.73):
            for s in (-1, 1):
                box(dp, (s * (dt / 2 + 0.003), yc, zc), (0.006, dw * 0.34, hh), T, bevel=0.003)
                box(dp, (s * (dt / 2 + 0.007), yc, zc), (0.004, dw * 0.27, hh - 0.07), T, bevel=0.002)
    for s in (-1, 1):
        lr.lathe(dp, (s * (dt / 2 + 0.002), dw - 0.07, 1.02), [(0.025, 0.0), (0.025, 0.005)], "RB_Brass", steps=16)
        lr.lathe(dp, (s * (dt / 2 + 0.025), dw - 0.07, 1.02), [(0.0, -0.02), (0.02, -0.018), (0.024, -0.005), (0.02, 0.012), (0.0, 0.02)], "RB_Brass", steps=16)
        box(dp, (s * (dt / 2 + 0.012), dw - 0.07, 1.02), (0.024, 0.012, 0.012), "RB_Brass")
    for z in (0.25, 1.75):
        box(dp, (0, 0.004, z), (dt + 0.004, 0.012, 0.1), "RB_Brass")
    ob = done(dp)
    ob.location = (W - 0.005, DOOR_Y0 + 0.003, 0.0)
    ob.rotation_euler = (0, 0, math.radians(35))
    # 門外的走廊：磁磚地、壁紙牆、遠處的樓梯起步與欄杆（只在門縫看得到的範圍）。
    hp = Part("A_HallStub")
    box(hp, (W + T_INNER + HALL / 2, 2.6, -0.011), (HALL, 4.0, 0.022), "RB_HearthTile")
    box(hp, (W + T_INNER + HALL + 0.06, 2.6, H / 2), (0.12, 4.0, H), "RB_Wallpaper")
    box(hp, (W + T_INNER + HALL / 2, 4.65, H / 2), (HALL, 0.12, H), "RB_Wallpaper")
    box(hp, (W + T_INNER + HALL / 2, 2.6, H + 0.05), (HALL, 4.0, 0.1), "RB_Ceiling")
    for i in range(5):
        box(hp, (W + T_INNER + HALL / 2, 4.3 - i * 0.26, 0.105 + i * 0.21), (HALL - 0.1, 0.26, 0.21 + i * 0.42), "RB_Mantel")
    done(hp)


def fireplace():
    """壁爐：木製壁爐架（壁柱、托架、層板線腳）、鑄鐵爐膛（拱形開口、爐罩）、兩側的釉面磁磚、磁磚爐床與黃銅擋板、炭火（微光）。"""
    cy = (CB_Y0 + CB_Y1) / 2
    xf = CB_D
    m = Part("F_Mantel")
    M = "RB_Mantel"
    mw, mh = 1.36, 1.14
    for s in (-1, 1):
        y = cy + s * (mw / 2 - 0.085)
        box(m, (xf + 0.07, y, 0.5), (0.14, 0.17, 1.0), M, bevel=0.008)
        box(m, (xf + 0.085, y, 0.5), (0.02, 0.11, 0.84), M, bevel=0.004)
        # 托架（S 形用三段方塊疊出曲線）。
        for i, (dz, dd) in enumerate(((0.0, 0.16), (0.04, 0.13), (0.08, 0.1))):
            box(m, (xf + 0.07 + dd * 0.3, y, 1.0 + dz + 0.02), (0.12 + dd * 0.4, 0.15, 0.04), M, bevel=0.01)
    box(m, (xf + 0.04, cy, 1.04), (0.08, mw - 0.34, 0.2), M, bevel=0.006)
    box(m, (xf + 0.12, cy, mh + 0.01), (0.25, mw + 0.12, 0.04), M, bevel=0.012)
    box(m, (xf + 0.1, cy, mh - 0.025), (0.2, mw + 0.06, 0.03), M, bevel=0.01)
    box(m, (xf + 0.13, cy, mh + 0.045), (0.24, mw + 0.1, 0.03), M, bevel=0.012)
    done(m)
    ci = Part("F_FireInsert")
    C = "RB_CastIron"
    iw, ih = mw - 0.34, 0.86
    box(ci, (xf + 0.02, cy, ih / 2 + 0.02), (0.04, iw, ih), C, bevel=0.006)
    # 拱形開口：用一圈小方塊排出拱邊。
    for i in range(13):
        a = math.pi * i / 12
        box(ci, (xf + 0.05, cy + math.cos(a) * 0.27, 0.52 + math.sin(a) * 0.22), (0.035, 0.07, 0.07), C, bevel=0.01, rot=(math.degrees(a) - 90, 0, 0))
    for s in (-1, 1):
        box(ci, (xf + 0.05, cy + s * 0.27, 0.3), (0.035, 0.07, 0.46), C, bevel=0.008)
    box(ci, (xf + 0.08, cy, 0.79), (0.07, 0.62, 0.04), C, bevel=0.01)
    # 爐膛內：黑色背板與爐柵、炭塊。
    box(ci, (xf - 0.12, cy, 0.4), (0.04, 0.5, 0.7), C)
    for i in range(5):
        box(ci, (xf + 0.02, cy - 0.2 + i * 0.1, 0.2), (0.16, 0.012, 0.1), C)
    box(ci, (xf + 0.02, cy, 0.15), (0.18, 0.46, 0.02), C)
    done(ci)
    coal = Part("D_Coals")
    import random
    rnd = random.Random(3)
    for i in range(22):
        lr.rounded_box(coal, (xf - 0.02 + rnd.random() * 0.1, cy - 0.18 + rnd.random() * 0.36, 0.18 + rnd.random() * 0.07), (0.06, 0.07, 0.05), 0.02, "RB_Coal", cuts=2)
    done(coal)
    tl = Part("F_FireTiles")
    for s in (-1, 1):
        n0 = len(tl.bm.faces)
        box(tl, (xf + 0.03, cy + s * 0.37, 0.48), (0.012, 0.15, 0.6), "RB_Tile")
        fs = faces_since(tl, n0)
        for f in fs:
            for l in f.loops:
                co = l.vert.co
                l[tl.uv].uv = ((co.y - (cy + s * 0.37 - 0.075)) / 0.15, (co.z - 0.18) / 0.3)
            f[tl.fix] = 1
    box(tl, (xf + 0.25, cy, 0.03), (0.5, mw + 0.08, 0.06), "RB_HearthTile", bevel=0.006)
    done(tl)
    fd = Part("D_Fender")
    for y0, y1 in ((cy - 0.6, cy + 0.6),):
        lr.extrude_profile(fd, [(math.cos(a) * 0.012, math.sin(a) * 0.012 + 0.07) for a in [i * math.pi / 6 for i in range(12)]], 1.2, "RB_Brass", axis='y', origin=(xf + 0.46, cy - 0.6, 0.0), smooth=True)
    for y in (cy - 0.6, cy + 0.6):
        lr.extrude_profile(fd, [(math.cos(a) * 0.012, math.sin(a) * 0.012 + 0.07) for a in [i * math.pi / 6 for i in range(12)]], 0.4, "RB_Brass", axis='x', origin=(xf + 0.06, y, 0.0), smooth=True)
    done(fd)
    # 壁爐上方的鍍金鏡（斜邊鏡面）。
    mi = Part("D_OverMirror")
    mw2, mh2 = 0.95, 0.72
    zc = mh + 0.1 + mh2 / 2 + 0.06
    for (dy, dz, sy, sz) in ((0, mh2 / 2, mw2, 0.07), (0, -mh2 / 2, mw2, 0.07), (mw2 / 2, 0, 0.07, mh2), (-mw2 / 2, 0, 0.07, mh2)):
        box(mi, (xf + 0.025, cy + dy, zc + dz), (0.05, sy, sz), "RB_Gilt", bevel=0.015)
    box(mi, (xf + 0.04, cy, zc + mh2 / 2 + 0.06), (0.03, 0.3, 0.08), "RB_Gilt", bevel=0.012)
    done(mi)
    mg = Part("D_MirrorGlass")
    box(mg, (xf + 0.012, cy, zc), (0.006, mw2 - 0.06, mh2 - 0.06), "RB_Mirror")
    done(mg)
    empty("E_FireLight", (xf + 0.12, cy, 0.3))
    empty("E_MirrorProbe", (1.6, 2.1, 1.4))
    return cy, xf, mh


def alcove_shelves(cy, xf):
    """左側凹室（y 0.0～CB_Y0）：下方帶門的矮櫃＋上方四層開放書架；書一本一本建（高度、厚度、傾斜、平放疊書）。"""
    import random
    rnd = random.Random(11)
    p = Part("F_AlcoveCupboard")
    T = "RB_Trim"
    y0, y1 = 0.05, CB_Y0 - 0.02
    depth = 0.34
    box(p, (depth / 2, (y0 + y1) / 2, 0.42), (depth, y1 - y0, 0.84), T, bevel=0.004)
    box(p, (depth / 2 + 0.01, (y0 + y1) / 2, 0.86), (depth + 0.03, y1 - y0 + 0.02, 0.035), "RB_Oak", bevel=0.008)
    for i in range(2):
        yc = y0 + (y1 - y0) * (0.25 + 0.5 * i)
        box(p, (depth + 0.004, yc, 0.44), (0.012, (y1 - y0) / 2 - 0.03, 0.72), T, bevel=0.003)
        box(p, (depth + 0.012, yc, 0.44), (0.006, (y1 - y0) / 2 - 0.1, 0.62), T, bevel=0.002)
        lr.lathe(p, (depth + 0.025, yc + (0.18 if i == 0 else -0.18) * (1 if i == 0 else 1), 0.62), [(0.0, 0.0), (0.012, 0.004), (0.014, 0.015), (0.0, 0.025)], "RB_Brass", steps=10)
    shelves = [1.22, 1.56, 1.9, 2.24]
    for z in shelves:
        box(p, (0.14, (y0 + y1) / 2, z - 0.012), (0.28, y1 - y0, 0.025), T, bevel=0.004)
    done(p)
    bk = Part("D_Books")
    for zi, z in enumerate([0.875] + shelves[:3]):
        y = y0 + 0.02
        top = (shelves[zi] if zi < len(shelves) else 2.5) - 0.03 if zi > 0 else 1.2
        while y < y1 - 0.06:
            if rnd.random() < 0.08:
                y += 0.05 + rnd.random() * 0.1
                continue
            if rnd.random() < 0.07 and zi > 0:
                # 平放的一疊。
                for j in range(rnd.randint(2, 4)):
                    th = 0.025 + rnd.random() * 0.02
                    box(bk, (0.13, y + 0.1, z + 0.005 + j * th + th / 2), (0.2, 0.15 + rnd.random() * 0.04, th - 0.002), "RB_Books", bevel=0.002, rot=(0, 0, rnd.uniform(-8, 8)))
                y += 0.24
                continue
            th = 0.02 + rnd.random() * 0.035
            hh = min(0.18 + rnd.random() * 0.13, (shelves[zi] - z - 0.04) if zi < 3 else 0.3)
            dd = 0.15 + rnd.random() * 0.08
            lean = 0.0
            n0 = len(bk.bm.faces)
            box(bk, (0.04 + dd / 2 + rnd.random() * 0.02, y + th / 2, z + hh / 2 + 0.001), (dd, th - 0.002, hh), "RB_Books", bevel=0.0025, rot=(lean, 0, 0))
            fs = faces_since(bk, n0)
            ci = rnd.randint(0, 7)
            cj = rnd.randint(0, 3)
            set_uv_rect(bk, fs, ci / 8, cj / 4, (ci + 1) / 8, (cj + 1) / 4, axis_u=1, axis_v=2)
            y += th
    done(bk)


def furniture(cy, xf, mh):
    # ---- 三人座沙發（捲邊扶手、分開的坐墊與靠墊、車削木腳），面向壁爐，背靠房間中央偏走廊側。
    s = Part("F_Sofa")
    V = "RB_Velvet"
    sx, sy, sw, sd = 2.62, cy, 2.05, 0.92
    # 底座與背框（沙發面向 −x）。
    lr.rounded_box(s, (sx, sy, 0.27), (sd - 0.06, sw - 0.06, 0.26), 0.05, V, cuts=5)
    lr.rounded_box(s, (sx + sd / 2 - 0.13, sy, 0.62), (0.2, sw - 0.3, 0.64), 0.07, V, cuts=6, puff=0.01)
    for side in (-1, 1):
        y = sy + side * (sw / 2 - 0.1)
        lr.rounded_box(s, (sx, y, 0.42), (sd, 0.2, 0.46), 0.06, V, cuts=6)
        lr.rounded_box(s, (sx - 0.01, y + side * 0.012, 0.66), (sd + 0.02, 0.26, 0.16), 0.08, V, cuts=7, puff=0.012)
        lr.rounded_box(s, (sx - sd / 2 + 0.005, y + side * 0.01, 0.48), (0.05, 0.25, 0.42), 0.022, V, cuts=4)
    inner = sw - 0.4
    for i in range(3):
        y = sy - inner / 2 + inner / 3 * (i + 0.5)
        lr.rounded_box(s, (sx - 0.08, y, 0.47), (sd - 0.3, inner / 3 - 0.015, 0.16), 0.06, V, cuts=7, puff=0.025, sag=0.03 if i == 1 else 0.018)
        lr.rounded_box(s, (sx + sd / 2 - 0.27, y, 0.75), (0.2, inner / 3 - 0.03, 0.44), 0.08, V, cuts=7, puff=0.035, rot=(0, -12, 0))
    for dx in (-1, 1):
        for dy in (-1, 1):
            lr.lathe(s, (sx + dx * (sd / 2 - 0.09), sy + dy * (sw / 2 - 0.09), 0.0), [(0.024, 0.0), (0.03, 0.03), (0.022, 0.07), (0.035, 0.1), (0.03, 0.14)], "RB_Mantel", steps=12)
    done(s)
    # 沙發上的抱枕（被靠過、歪的）與搭在扶手上的格紋毯。
    cu = Part("D_Cushions")
    # 抱枕斜靠在靠墊前（沙發面向 −x，靠墊在 +x 側）。
    lr.rounded_box(cu, (sx + 0.08, sy - inner / 2 + 0.24, 0.72), (0.13, 0.4, 0.38), 0.06, "RB_Cushion", cuts=7, puff=0.04, sag=0.02, rot=(10, -22, 6))
    lr.rounded_box(cu, (sx + 0.06, sy + inner / 2 - 0.26, 0.7), (0.12, 0.38, 0.36), 0.06, "RB_Linen", cuts=7, puff=0.035, sag=0.02, rot=(-8, -26, -10))
    done(cu)
    th = Part("D_Throw")

    def fn(u, v):
        y = sy + sw / 2 - 0.02 - u * 0.55
        path = [(sx + 0.42, 0.74), (sx + 0.2, 0.76), (sx, 0.76), (sx - 0.25, 0.74), (sx - 0.44, 0.62), (sx - 0.47, 0.38)]
        t = v * (len(path) - 1)
        i = min(int(t), len(path) - 2)
        f = t - i
        x = path[i][0] * (1 - f) + path[i + 1][0] * f
        z = path[i][1] * (1 - f) + path[i + 1][1] * f
        fold = math.sin(u * math.pi * 4 + v * 3) * 0.012
        return (x, y + fold, z + 0.02)

    lr.grid_sheet(th, 0.55, 1.2, 16, 26, fn, "RB_Plaid", thickness=0.008)
    done(th, 70)
    # ---- 翼背扶手椅（右側凹室前、斜對壁爐）。
    a = Part("F_Armchair")
    Tw = "RB_Tweed"
    ax, ay = 1.0, CB_Y1 + 0.78
    lr.rounded_box(a, (0, 0, 0.27), (0.74, 0.74, 0.26), 0.05, Tw, cuts=5)
    lr.rounded_box(a, (0.3, 0, 0.75), (0.16, 0.7, 0.86), 0.06, Tw, cuts=6, puff=0.02)
    for side in (-1, 1):
        lr.rounded_box(a, (0.0, side * 0.33, 0.48), (0.7, 0.13, 0.32), 0.05, Tw, cuts=5)
        lr.rounded_box(a, (0.18, side * 0.34, 0.92), (0.3, 0.1, 0.5), 0.05, Tw, cuts=5, rot=(0, 0, side * 12))
    lr.rounded_box(a, (-0.05, 0, 0.45), (0.56, 0.52, 0.13), 0.05, Tw, cuts=6, puff=0.02, sag=0.02)
    for dx in (-1, 1):
        for dy in (-1, 1):
            lr.lathe(a, (dx * 0.3, dy * 0.3, 0.0), [(0.02, 0.0), (0.026, 0.04), (0.02, 0.1), (0.03, 0.14)], "RB_Mantel", steps=12)
    ob = done(a)
    ob.location = (ax, ay, 0)
    ob.rotation_euler = (0, 0, math.radians(125))
    # ---- 茶几（橡木、車削腳、下層擱板）＋報紙、馬克杯、眼鏡盒。
    t = Part("F_CoffeeTable")
    tx, ty = 1.62, cy
    lr.rounded_box(t, (tx, ty, 0.43), (0.55, 1.0, 0.035), 0.012, "RB_Oak", cuts=3)
    box(t, (tx, ty, 0.395), (0.47, 0.92, 0.05), "RB_Oak", bevel=0.006)
    box(t, (tx, ty, 0.12), (0.45, 0.9, 0.02), "RB_Oak", bevel=0.004)
    for dx in (-1, 1):
        for dy in (-1, 1):
            lr.turned_leg(t, tx + dx * 0.22, ty + dy * 0.44, 0.0, 0.41, "RB_Oak", r=0.022)
    done(t)
    dd = Part("D_TableThings")
    lr.lathe(dd, (tx - 0.1, ty + 0.25, 0.4475), [(0.0, 0.0), (0.036, 0.0), (0.04, 0.008), (0.041, 0.09), (0.036, 0.09), (0.035, 0.012), (0.0, 0.012)], "RB_Ceramic", steps=24)
    box(dd, (tx + 0.08, ty - 0.18, 0.452), (0.3, 0.42, 0.008), "RB_Paper", rot=(0, 0, 9))
    box(dd, (tx + 0.1, ty - 0.17, 0.46), (0.28, 0.4, 0.006), "RB_Paper", rot=(0, 0, 4))
    lr.rounded_box(dd, (tx - 0.15, ty - 0.3, 0.465), (0.06, 0.16, 0.035), 0.015, "RB_Plastic", cuts=2)
    box(dd, (tx + 0.02, ty - 0.2, 0.13 + 0.02), (0.3, 0.26, 0.02), "RB_Books", bevel=0.003)
    done(dd)
    # ---- 邊桌＋陶瓷檯燈（右凹室，扶手椅旁）。
    st = Part("F_SideTable")
    stx, sty = 0.45, CB_Y1 + 0.35
    lr.lathe(st, (stx, sty, 0.0), [(0.0, 0.01), (0.2, 0.01), (0.2, 0.03), (0.04, 0.05), (0.03, 0.3), (0.045, 0.45), (0.03, 0.55), (0.04, 0.58)], "RB_Mantel", steps=24)
    lr.lathe(st, (stx, sty, 0.58), [(0.0, 0.0), (0.24, 0.0), (0.25, 0.012), (0.24, 0.025), (0.0, 0.025)], "RB_Mantel", steps=36)
    done(st)
    lamp = Part("D_TableLamp")
    lr.lathe(lamp, (stx, sty, 0.605), [(0.0, 0.0), (0.06, 0.0), (0.075, 0.03), (0.09, 0.12), (0.07, 0.24), (0.025, 0.28), (0.02, 0.33)], "RB_Ceramic", steps=28)
    lr.lathe(lamp, (stx, sty, 0.92), [(0.01, 0.0), (0.012, 0.06)], "RB_Brass", steps=10)
    shade_faces = lr.lathe(lamp, (stx, sty, 0.9), [(0.19, 0.0), (0.12, 0.24), (0.115, 0.243), (0.185, 0.003)], "RB_Shade", steps=40, cap_top=False, cap_bottom=False)
    for f in shade_faces:
        for v in f.verts:
            ang = math.atan2(v.co.y - sty, v.co.x - stx)
            k = 1.0 + 0.025 * math.cos(ang * 20)
            v.co.x = stx + (v.co.x - stx) * k
            v.co.y = sty + (v.co.y - sty) * k
    done(lamp)
    empty("E_TableLamp", (stx, sty, 1.0))
    # ---- 落地燈（沙發後、窗邊角落）。
    fl = Part("D_FloorLamp")
    fx, fy = 3.3, 0.3
    lr.lathe(fl, (fx, fy, 0.0), [(0.0, 0.0), (0.16, 0.0), (0.165, 0.015), (0.12, 0.03), (0.02, 0.05), (0.014, 1.45), (0.02, 1.47)], "RB_Brass", steps=24)
    sf = lr.lathe(fl, (fx, fy, 1.42), [(0.24, 0.0), (0.16, 0.28), (0.155, 0.283), (0.235, 0.003)], "RB_Shade", steps=44, cap_top=False, cap_bottom=False)
    done(fl)
    empty("E_FloorLamp", (fx, fy, 1.5))
    # ---- 天花吊燈（布燈罩，預設關）。
    pl = Part("D_Pendant")
    lr.lathe(pl, (W / 2, D / 2 - 0.1, H - 0.5), [(0.004, 0.0), (0.004, 0.45)], "RB_Plastic", steps=8)
    lr.lathe(pl, (W / 2, D / 2 - 0.1, H - 0.72), [(0.26, 0.0), (0.2, 0.22), (0.195, 0.222), (0.255, 0.002)], "RB_Shade", steps=44, cap_top=False, cap_bottom=False)
    done(pl)
    empty("E_Pendant", (W / 2, D / 2 - 0.1, H - 0.62))
    # ---- 電視（靠走廊牆的矮櫃上，薄機身，面向 −x）。
    tv = Part("F_TvCabinet")
    tvx, tvy = 3.33, 0.8
    box(tv, (tvx, tvy, 0.25), (0.5, 0.5, 0.5), "RB_Oak", bevel=0.008)
    box(tv, (tvx - 0.25, tvy, 0.25), (0.01, 0.42, 0.38), "RB_Oak", bevel=0.003)
    for s in (-1, 1):
        lr.lathe(tv, (tvx - 0.262, tvy + s * 0.08, 0.38), [(0.0, 0.0), (0.008, 0.002), (0.01, 0.01), (0.0, 0.016)], "RB_Brass", steps=10)
    done(tv)
    sc = Part("D_TV")
    box(sc, (tvx, tvy, 0.51), (0.16, 0.24, 0.015), "RB_Plastic", bevel=0.004)
    box(sc, (tvx, tvy, 0.56), (0.04, 0.06, 0.1), "RB_Plastic")
    box(sc, (tvx - 0.01, tvy, 0.82), (0.035, 0.82, 0.5), "RB_Plastic", bevel=0.006)
    box(sc, (tvx - 0.029, tvy, 0.83), (0.002, 0.78, 0.44), "RB_Screen")
    done(sc)
    # ---- 地毯（帶流蘇）。
    rg = Part("D_Rug")
    rx, ry, rw, rd = 1.95, cy, 2.0, 2.7

    def rfn(u, v):
        return (rx - rw / 2 + u * rw, ry - rd / 2 + v * rd, 0.004 + 0.002 * math.sin(u * 7) * math.sin(v * 5))

    bm = rg.bm
    nx, ny = 10, 14
    verts = [[bm.verts.new(rfn(i / nx, j / ny)) for i in range(nx + 1)] for j in range(ny + 1)]
    faces = []
    for j in range(ny):
        for i in range(nx):
            f = bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
            faces.append(f)
            for l, (uu, vv) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                l[rg.uv].uv = (uu / nx, vv / ny)
            f[rg.fix] = 1
    lr._set_faces(rg, faces, "RB_Rug", True)
    for i in range(60):
        for yy in (ry - rd / 2 - 0.03, ry + rd / 2 + 0.03):
            box(rg, (rx - rw / 2 + 0.02 + i * (rw - 0.04) / 59, yy, 0.003), (0.008, 0.06, 0.004), "RB_Linen")
    done(rg, 80)
    # ---- 牆上的畫（掛在掛畫線上，有掛繩）。
    for name, tex, (x, y, rot), (pw, ph) in (("D_Painting1", "RB_Painting1", (W - 0.02, 1.55, 90), (0.62, 0.46)), ("D_Painting2", "RB_Painting2", (1.8, D - 0.02, 180), (0.5, 0.62))):
        pp = Part(name)
        z = 1.55
        for (dx, dz, sw2, sh2) in ((0, ph / 2, pw + 0.12, 0.06), (0, -ph / 2, pw + 0.12, 0.06), (pw / 2, 0, 0.06, ph), (-pw / 2, 0, 0.06, ph)):
            box(pp, (dx, 0.0, dz), (sw2, 0.045, sh2), "RB_Gilt", bevel=0.012)
        n0 = len(pp.bm.faces)
        box(pp, (0, 0.012, 0), (pw - 0.02, 0.006, ph - 0.02), tex)
        set_uv_rect(pp, faces_since(pp, n0), 0, 0, 1, 1, axis_u=0, axis_v=2)
        for s in (-1, 1):
            box(pp, (s * pw * 0.3, -0.01, ph / 2 + 0.25), (0.003, 0.003, 0.5), "RB_Brass", rot=(0, s * -14, 0))
        ob = done(pp)
        ob.location = (x, y, z)
        ob.rotation_euler = (0, 0, math.radians(rot))
    # ---- 壁爐架上：座鐘、燭台、小花瓶、家庭合照（相框）。
    mp = Part("D_MantelThings")
    top = mh + 0.06
    box(mp, (xf + 0.12, cy + 0.02, top + 0.11), (0.1, 0.22, 0.22), "RB_Mantel", bevel=0.01)
    lr.lathe(mp, (xf + 0.17, cy + 0.02, top + 0.13), [(0.0, 0.0), (0.06, 0.0), (0.06, 0.006), (0.0, 0.006)], "RB_Ceramic", steps=24)
    for s in (-1, 1):
        lr.lathe(mp, (xf + 0.12, cy + s * 0.5, top), [(0.0, 0.0), (0.045, 0.0), (0.03, 0.02), (0.012, 0.05), (0.02, 0.16), (0.012, 0.2), (0.025, 0.21)], "RB_Brass", steps=16)
        lr.lathe(mp, (xf + 0.12, cy + s * 0.5, top + 0.21), [(0.01, 0.0), (0.01, 0.12), (0.0, 0.125)], "RB_Ceramic", steps=10)
    lr.lathe(mp, (xf + 0.1, cy - 0.32, top), [(0.0, 0.0), (0.035, 0.0), (0.05, 0.05), (0.03, 0.12), (0.022, 0.15), (0.03, 0.17)], "RB_Ceramic", steps=20)
    done(mp)
    fr = Part("D_PhotoFrame")
    fw, fh = 0.21, 0.165
    fz = top + fh / 2 + 0.01
    fyc = cy + 0.28
    for (dy, dz, sy, sz) in ((0, fh / 2, fw + 0.04, 0.025), (0, -fh / 2, fw + 0.04, 0.025), (fw / 2, 0, 0.025, fh), (-fw / 2, 0, 0.025, fh)):
        box(fr, (xf + 0.13, fyc + dy, fz + dz), (0.02, sy, sz), "RB_Mantel", bevel=0.005)
    box(fr, (xf + 0.11, fyc, fz), (0.006, fw, fh), "RB_Mantel")
    box(fr, (xf + 0.08, fyc, fz - 0.03), (0.06, 0.012, fh * 0.8), "RB_Mantel", rot=(0, 20, 0))
    ob = done(fr)
    ob.rotation_euler = (0, 0, 0)
    pr = Part("D_PhotoPrint")
    n0 = len(pr.bm.faces)
    box(pr, (xf + 0.122, fyc, fz), (0.002, fw - 0.012, fh - 0.012), "RB_Photo")
    set_uv_rect(pr, faces_since(pr, n0), 0, 0, 1, 1, axis_u=1, axis_v=2)
    done(pr)
    empty("E_Photo", (xf + 0.13, fyc, fz))
    # ---- 扶手椅旁的毛線籃。
    kb = Part("D_KnittingBasket")
    lr.lathe(kb, (ax + 0.45, ay - 0.45, 0.0), [(0.0, 0.0), (0.16, 0.0), (0.18, 0.08), (0.19, 0.2), (0.18, 0.21), (0.17, 0.02), (0.0, 0.02)], "RB_Oak", steps=28)
    for i, (dx, dy, c) in enumerate(((0.05, 0.03, "RB_Cushion"), (-0.06, 0.02, "RB_Linen"), (0.0, -0.06, "RB_Plaid"))):
        lr.rounded_box(kb, (ax + 0.45 + dx, ay - 0.45 + dy, 0.2), (0.11, 0.11, 0.1), 0.05, c, cuts=4)
    done(kb)
    # ---- 窗邊的盆栽（陶盆＋一叢葉片）。
    pt = Part("D_Plant")
    px, py = 2.62, 0.32
    lr.lathe(pt, (px, py, 0.0), [(0.0, 0.0), (0.1, 0.0), (0.13, 0.22), (0.14, 0.24), (0.0, 0.24)], "RB_Ceramic", steps=24)
    import random
    rnd = random.Random(5)
    for i in range(26):
        a = rnd.random() * math.pi * 2
        tilt = 20 + rnd.random() * 45
        L = 0.25 + rnd.random() * 0.2
        lr.rounded_box(pt, (px + math.cos(a) * L * 0.4, py + math.sin(a) * L * 0.4, 0.24 + L * 0.55), (0.06, 0.012, L), 0.006, "RB_Leaf", cuts=2, rot=(math.cos(a) * tilt, math.sin(a) * -tilt, math.degrees(a)))
    done(pt)


def lights_and_marks():
    empty("E_Spawn", (W - 0.45, (DOOR_Y0 + DOOR_Y1) / 2, 0.0), (0, 0, 180))
    empty("E_StreetLamp", (1.8, -2.5, 2.6))
    empty("E_Moon", (1.8, -6.0, 6.0))
    empty("E_HallLamp", (W + T_INNER + HALL / 2, 2.0, H - 0.3))
    # 截圖機位：門口看進客廳、窗邊看壁爐、壁爐旁看門。
    empty("E_CamDoor", (W - 0.35, 3.45, 1.58), (0, 0, 0))
    empty("E_CamWindow", (2.9, 0.6, 1.58))
    empty("E_CamFire", (1.2, 0.9, 1.58))
    # 機位的注視點（Unity 端直接讀，不必換算座標系）。
    empty("E_TgtDoor", (0.6, 2.1, 1.2))
    empty("E_TgtFire", (3.4, 3.6, 1.1))
    empty("E_TgtDark", (0.5, 0.7, 1.1))


def build(out_dir):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    walls()
    window()
    door()
    cy, xf, mh = fireplace()
    alcove_shelves(cy, xf)
    furniture(cy, xf, mh)
    lights_and_marks()
    os.makedirs(out_dir, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in OBJS:
        o.select_set(True)
    path = os.path.join(out_dir, "LivingRoom.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH', 'EMPTY'}, add_leaf_bones=False, path_mode='STRIP',
                             mesh_smooth_type='OFF', use_mesh_modifiers=False)
    print("[Case01Rebuild] LivingRoom.fbx", len(OBJS), "objects")
