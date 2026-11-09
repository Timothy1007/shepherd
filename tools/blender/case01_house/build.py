"""Case01 Demo Phase 1（v0.7）：住宅其他房間、事務所、街區的模型與貼圖（沿用 v0.6 客廳的建模工具與畫風）。

用法：blender -b --factory-startup --python tools/blender/case01_house/build.py -- [輸出資料夾] [all|tex|models]
預設輸出 unity/Assets/Exploration/Case01Art/Phase1/Resources/Case01Phase1。固定亂數種子、沒有外部素材。
慣例同客廳：公尺、原點在地面中心（掛牆物在背面中心）、正面朝 Blender +Y（Unity +Z）。
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LIVING = os.path.join(HERE, "..", "case01_living_room")
sys.path.insert(0, LIVING)
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import models as M  # noqa: E402  （客廳的建模工具）
import textures as T  # noqa: E402

M.TEX_METERS.update({
    "P1_Tile": 1.2, "P1_Pavement": 2.4, "P1_Brick": 1.6, "P1_Road": 3.0, "P1_Slate": 1.5, "P1_Render": 1.5, "P1_WoodPaint": 0.8,
    "P1_KitchenUnit": 0.8, "P1_Worktop": 1.0, "P1_Steel": 0.4, "P1_Enamel": 0.6, "P1_Leather": 0.5, "P1_Bedding": 0.8, "P1_Mattress": 0.8,
    "P1_DoorGreen": 0.8, "P1_GlassDark": 1.0, "P1_Metal": 0.5, "P1_Stone": 1.2, "P1_Coat": 0.6, "P1_CoatRed": 0.6,
})


# ================================================================ 貼圖

def tiles_checker(n, k):
    """廚房地磚：米白／灰綠棋盤格（30 cm 一格），有細灰縫與輕微舊化；不再是帶紫色紋路的異常中庭石材。"""
    y, x = np.mgrid[0:n, 0:n] / n
    cells = 4
    cx, cy = np.floor(x * cells), np.floor(y * cells)
    checker = ((cx + cy) % 2)[..., None]
    a = np.array([0.78, 0.75, 0.68])
    b = np.array([0.42, 0.47, 0.44])
    rgb = a * (1 - checker) + b * checker
    grout = np.minimum((x * cells) % 1, (y * cells) % 1) < 0.02
    rgb[grout] = (0.3, 0.29, 0.27)
    rgb *= (0.88 + 0.2 * T.fbm(n, n, k))[..., None]
    return T.posterize(rgb, 20, 0.3)


def pavement(n, k):
    """人行道：大塊灰色石板（60×90 cm，錯縫），接縫較暗、表面有雨後的深淺。"""
    y, x = np.mgrid[0:n, 0:n] / n
    rows = 4
    row = np.floor(y * rows)
    xs = x * 2.6 + (row % 2) * 0.5
    seam_x = (xs % 1) < 0.015
    seam_y = (y * rows % 1) < 0.015
    rng = T._rng(k)
    tint = np.ones((n, n))
    for r in range(rows):
        for c in range(4):
            m = (row == r) & (np.floor(xs) % 4 == c)
            tint[m] = 0.85 + 0.25 * rng.random()
    base = np.array([0.46, 0.46, 0.47])
    rgb = base * tint[..., None] * (0.85 + 0.25 * T.fbm(n, n, k + 1))[..., None]
    rgb[seam_x | seam_y] *= 0.45
    return T.posterize(rgb, 20, 0.3)


def brick(n, k, base=(0.48, 0.25, 0.17)):
    """英式磚牆（順砌、灰色灰縫）：每塊磚色調不同，筆觸概括。1 張＝1.6 m。"""
    y, x = np.mgrid[0:n, 0:n] / n
    rows = 22
    row = np.floor(y * rows)
    bx = x * 7.2 + (row % 2) * 0.5
    col = np.floor(bx)
    mortar = ((y * rows) % 1 < 0.14) | ((bx % 1) < 0.05)
    rng = T._rng(k)
    lut = 0.75 + 0.4 * rng.random((rows + 1, 16))
    tint = lut[row.astype(int) % (rows + 1), col.astype(int) % 16]
    rgb = np.asarray(base) * tint[..., None]
    rgb *= (0.85 + 0.25 * T.fbm(n, n, k + 1))[..., None]
    rgb[mortar] = (0.48, 0.46, 0.42)
    return T.posterize(rgb, 18, 0.35)


def build_textures(out):
    S = lambda name, rgb, a=None: T.save(rgb, os.path.join(out, name + ".png"), a)
    S("P1_Tile", tiles_checker(512, 31))
    S("P1_Pavement", pavement(512, 32))
    S("P1_Brick", brick(512, 33))
    S("P1_Road", T.plaster(256, 34, (0.12, 0.12, 0.13)))
    S("P1_Slate", T.wood(256, 35, (0.1, 0.11, 0.13), (0.2, 0.21, 0.24), grain=14.0))
    S("P1_Render", T.plaster(256, 36, (0.72, 0.7, 0.64)))
    S("P1_WoodPaint", T.plaster(256, 37, (0.84, 0.81, 0.74)))
    S("P1_KitchenUnit", T.plaster(256, 38, (0.62, 0.68, 0.62)))
    S("P1_Worktop", T.wood(256, 39, (0.3, 0.2, 0.12), (0.5, 0.36, 0.22), grain=6.0))
    S("P1_Steel", T.plaster(256, 40, (0.6, 0.62, 0.65)))
    S("P1_Enamel", T.plaster(256, 41, (0.86, 0.84, 0.78)))
    S("P1_Leather", T.fabric(256, 42, (0.36, 0.18, 0.11), (0.18, 0.08, 0.05), 40, 0.3))
    S("P1_Bedding", T.fabric(256, 43, (0.62, 0.66, 0.72), (0.4, 0.44, 0.52), 70, 0.2))
    S("P1_Mattress", T.fabric(256, 44, (0.86, 0.84, 0.78), (0.66, 0.64, 0.6), 90, 0.15))
    S("P1_DoorGreen", T.plaster(256, 45, (0.1, 0.24, 0.19)))
    S("P1_Metal", T.iron(256, 46))
    S("P1_Stone", T.plaster(256, 47, (0.62, 0.6, 0.56)))
    S("P1_Coat", T.fabric(256, 48, (0.24, 0.26, 0.32), (0.12, 0.13, 0.17), 60, 0.25))
    S("P1_CoatRed", T.fabric(256, 49, (0.48, 0.16, 0.12), (0.26, 0.07, 0.05), 60, 0.25))


# ================================================================ 模型

def panel_door(W=0.82, H=1.98, name="PanelDoor", mat="P1_WoodPaint", glass=False):
    """四片嵌板門（或上方兩片玻璃）：門框外圍、凸起的嵌板、黃銅把手。原點在門底中心，正面 +Y。"""
    p = M.Part(name)
    M.box(p, (0, 0, H / 2), (W, 0.04, H), mat, bevel=0.006)
    pw, ph = W * 0.32, H * 0.3
    for sx in (-1, 1):
        for zc, hh in ((H * 0.72, ph), (H * 0.3, ph)):
            if glass and zc > H * 0.5:
                M.box(p, (sx * W * 0.21, 0.022, zc), (pw, 0.006, hh), "P1_GlassDark")
            else:
                M.box(p, (sx * W * 0.21, 0.024, zc), (pw, 0.012, hh), mat, bevel=0.01, segments=2)
    M.rounded_box(p, (W * 0.38, 0.045, H * 0.48), (0.05, 0.03, 0.05), 0.02, "LR_Brass", cuts=3)
    M.box(p, (W * 0.38, 0.03, H * 0.48), (0.03, 0.02, 0.1), "LR_Brass", bevel=0.004)
    return M.finish(p)


def front_door():
    """十七號的前門（室內側）：深綠漆、四片嵌板、上方窗格、黃銅門環／信箱口。"""
    o = panel_door(1.02, 2.12, "FrontDoorArt", "P1_DoorGreen", glass=True)
    return o


def hall_bench():
    p = M.Part("HallBench")
    W = "LR_WoodMid"
    M.box(p, (0, 0, 0.45), (1.1, 0.4, 0.04), W, bevel=0.008, segments=2)
    M.rounded_box(p, (0, 0.0, 0.5), (1.04, 0.36, 0.06), 0.025, "LR_SofaFabric", cuts=4, puff=0.01)
    M.box(p, (0, 0, 0.18), (1.02, 0.36, 0.025), W, bevel=0.005)
    for sx in (-1, 1):
        for sy in (-1, 1):
            M.turned_leg(p, sx * 0.5, sy * 0.15, 0, 0.43, W, top_block=0.05, r=0.02)
    # 架上的兩雙鞋。
    for i, x in enumerate((-0.28, 0.22)):
        M.rounded_box(p, (x, 0.02, 0.24), (0.11, 0.27, 0.08), 0.035, "LR_Iron" if i == 0 else "P1_Leather", cuts=3)
        M.rounded_box(p, (x + 0.13, 0.04, 0.24), (0.11, 0.27, 0.08), 0.035, "LR_Iron" if i == 0 else "P1_Leather", cuts=3)
    return M.finish(p)


def coat_rack(W=1.0):
    """掛衣架：木條＋黃銅鉤、掛著兩件大衣與一條圍巾（簡化的布片垂墜）。原點在背面中心、木條高度。"""
    p = M.Part("CoatRack")
    M.box(p, (0, 0.012, 0), (W, 0.024, 0.11), "LR_WoodMid", bevel=0.006, segments=2)
    for i in range(4):
        x = -W * 0.36 + i * W * 0.24
        M.lathe(p, (x, 0.03, -0.02), [(0.008, 0), (0.008, 0.06), (0.016, 0.07)], "LR_Brass", steps=8)
    for i, (x, mat) in enumerate(((-W * 0.36, "P1_Coat"), (W * 0.12, "P1_CoatRed"))):
        def fn(u, v, x=x):
            a = (u - 0.5) * 2
            yy = 0.07 + 0.09 * math.cos(a * 1.4) + 0.02 * math.sin(u * 18)
            return (x + a * (0.17 + 0.07 * v), yy, -0.03 - v * 1.0)
        M.grid_sheet(p, 0.4, 1.0, 10, 8, fn, mat, thickness=0.02)
        M.rounded_box(p, (x, 0.07, -0.06), (0.22, 0.09, 0.09), 0.04, mat, cuts=3)
    return M.finish(p, sharp_angle=80)


def shoe_cabinet(W=0.9, H=0.9, D=0.36, name="ShoeCabinetArt"):
    p = M.Part(name)
    Wd = "LR_WoodMid"
    M.box(p, (0, 0, H / 2 + 0.04), (W, D, H - 0.08), Wd, bevel=0.006)
    M.box(p, (0, 0.01, H), (W + 0.03, D + 0.03, 0.03), Wd, bevel=0.008, segments=2)
    M.box(p, (0, 0, 0.03), (W - 0.02, D - 0.02, 0.06), "LR_WoodDark", bevel=0.004)
    for sx in (-1, 1):
        M.box(p, (sx * W / 4, D / 2 + 0.006, H / 2 + 0.04), (W / 2 - 0.03, 0.012, H - 0.16), Wd, bevel=0.004)
        M.box(p, (sx * W / 4, D / 2 + 0.014, H / 2 + 0.04), (W / 2 - 0.14, 0.006, H - 0.3), "LR_WoodDark", bevel=0.003)
        M.rounded_box(p, (sx * 0.04, D / 2 + 0.025, H * 0.62), (0.02, 0.02, 0.05), 0.009, "LR_Brass", cuts=2)
    return M.finish(p)


def umbrella_stand():
    p = M.Part("UmbrellaStandArt")
    M.lathe(p, (0, 0, 0), [(0.0, 0), (0.11, 0), (0.12, 0.02), (0.1, 0.25), (0.115, 0.48), (0.125, 0.5), (0.11, 0.5)], "LR_Ceramic", steps=20, cap_top=False)
    for i, (a, c) in enumerate(((0.3, "LR_CushionRed"), (2.4, "P1_Coat"))):
        x, y = math.cos(a) * 0.04, math.sin(a) * 0.04
        M.lathe(p, (x, y, 0.05), [(0.006, 0), (0.006, 0.6), (0.035, 0.62), (0.03, 0.85), (0.004, 0.92)], c, steps=10)
        M.lathe(p, (x, y, 0.92), [(0.01, 0), (0.01, 0.08)], "LR_WoodDark", steps=8)
    return M.finish(p)


def kitchen_base(W=0.6, name="KitchenBase"):
    """一節下櫃（60 cm）：Shaker 門板、門框線、不鏽鋼把手、踢腳。檯面由 Unity 依整排長度另外放。原點在地面中心、正面 +Y。"""
    p = M.Part(name)
    K = "P1_KitchenUnit"
    M.box(p, (0, 0, 0.47), (W, 0.58, 0.78), K, bevel=0.004)
    M.box(p, (0, 0.24, 0.04), (W, 0.06, 0.08), "LR_WoodDark")
    M.box(p, (0, 0.293, 0.47), (W - 0.02, 0.014, 0.74), K, bevel=0.004)
    M.box(p, (0, 0.302, 0.45), (W - 0.12, 0.006, 0.6), K, bevel=0.008, segments=2)
    M.box(p, (0, 0.302, 0.82), (W - 0.06, 0.006, 0.1), K, bevel=0.004)  # 抽屜
    M.box(p, (0, 0.315, 0.82), (0.14, 0.012, 0.012), "P1_Steel", bevel=0.003)
    M.box(p, (W * 0.38, 0.315, 0.62), (0.012, 0.012, 0.12), "P1_Steel", bevel=0.003)
    return M.finish(p)


def wall_cabinet(W=0.9):
    p = M.Part("WallCabinet")
    K = "P1_KitchenUnit"
    M.box(p, (0, 0, 0), (W, 0.34, 0.7), K, bevel=0.004)
    M.box(p, (0, 0.012, 0.36), (W + 0.03, 0.37, 0.03), K, bevel=0.006)
    for sx in (-1, 1):
        M.box(p, (sx * W / 4, 0.172, 0), (W / 2 - 0.02, 0.012, 0.66), K, bevel=0.004)
        M.box(p, (sx * W / 4, 0.18, 0), (W / 2 - 0.12, 0.006, 0.52), K, bevel=0.008, segments=2)
        M.box(p, (sx * 0.05, 0.19, -0.24), (0.012, 0.012, 0.1), "P1_Steel", bevel=0.003)
    return M.finish(p)


def range_cooker():
    """爐台：琺瑯爐身、兩個烤箱門＋玻璃窗、旋鈕列、四口爐架、上方抽油煙機（煙罩＋煙囪）。原點在地面中心、正面 +Y。"""
    p = M.Part("RangeCooker")
    E = "P1_Enamel"
    M.box(p, (0, 0, 0.45), (0.62, 0.6, 0.86), E, bevel=0.01, segments=2)
    M.box(p, (0, 0.302, 0.42), (0.52, 0.012, 0.42), "P1_Steel", bevel=0.008)
    M.box(p, (0, 0.31, 0.44), (0.36, 0.006, 0.2), "P1_GlassDark", bevel=0.004)
    M.box(p, (0, 0.33, 0.66), (0.4, 0.02, 0.02), "P1_Steel", bevel=0.006)
    for i in range(5):
        M.rounded_box(p, (-0.2 + i * 0.1, 0.31, 0.8), (0.035, 0.03, 0.035), 0.015, "LR_Iron", cuts=2)
    M.box(p, (0, 0, 0.885), (0.62, 0.6, 0.012), "LR_Iron")
    for i in range(4):
        cx, cy = (-0.14 if i % 2 == 0 else 0.14), (-0.12 if i < 2 else 0.12)
        M.lathe(p, (cx, cy, 0.89), [(0.0, 0.0), (0.08, 0.0), (0.08, 0.012), (0.06, 0.016), (0.0, 0.016)], "LR_Iron", steps=16)
    M.lathe(p, (0.14, 0.12, 0.906), [(0.0, 0), (0.1, 0), (0.105, 0.14), (0.11, 0.15), (0.0, 0.15)], "P1_Steel", steps=18)
    # 抽油煙機。
    M.box(p, (0, -0.12, 1.88), (0.66, 0.38, 0.1), "P1_Steel", bevel=0.01, segments=2)
    M.box(p, (0, -0.2, 2.34), (0.26, 0.22, 0.86), "P1_Steel", bevel=0.008)
    return M.finish(p)


def butler_sink():
    """陶瓷水槽（嵌在檯面）＋鵝頸龍頭與兩個把手。原點在檯面高度的水槽中心。"""
    p = M.Part("ButlerSink")
    M.box(p, (0, 0.04, 0.0), (0.64, 0.46, 0.02), "P1_Enamel", bevel=0.01, segments=2)
    M.box(p, (0, 0.05, -0.002), (0.5, 0.34, 0.006), "LR_Iron")
    M.lathe(p, (0, -0.17, 0.0), [(0.03, 0), (0.025, 0.02), (0.018, 0.04), (0.016, 0.24)], "P1_Steel", steps=12)
    for i in range(10):
        a = math.pi * i / 9
        M.rounded_box(p, (0, -0.17 + 0.09 * (1 - math.cos(a)), 0.24 + 0.09 * math.sin(a)), (0.03, 0.03, 0.03), 0.014, "P1_Steel", cuts=2)
    for sx in (-1, 1):
        M.lathe(p, (sx * 0.1, -0.18, 0.0), [(0.02, 0), (0.02, 0.05), (0.03, 0.06), (0.0, 0.065)], "P1_Steel", steps=10)
    return M.finish(p)


def fridge():
    p = M.Part("FridgeArt")
    E = "P1_Enamel"
    M.rounded_box(p, (0, 0, 0.92), (0.66, 0.64, 1.84), 0.06, E, cuts=6)
    M.box(p, (0, 0.325, 1.25), (0.62, 0.012, 0.012), "LR_Iron")
    for z, h in ((1.55, 0.36), (0.85, 0.42)):
        M.rounded_box(p, (-0.26, 0.345, z), (0.03, 0.04, h), 0.012, "P1_Steel", cuts=3)
    return M.finish(p)


def kettle_teapot():
    out = []
    k = M.Part("Kettle")
    M.lathe(k, (0, 0, 0), [(0.0, 0), (0.08, 0), (0.09, 0.03), (0.085, 0.14), (0.05, 0.18), (0.015, 0.2), (0.0, 0.21)], "P1_Steel", steps=18)
    M.box(k, (0.0, 0, 0.23), (0.12, 0.02, 0.02), "LR_Iron", bevel=0.006)
    out.append(M.finish(k))
    return out


def dining_chair(name="DiningChairArt"):
    """餐椅：座板＋坐墊、車削前腳、彎背橫條與直條（不是方塊）。原點在地面中心、正面 +Y（坐下時面向）。"""
    p = M.Part(name)
    W = "LR_WoodDark"
    M.box(p, (0, 0, 0.45), (0.44, 0.44, 0.035), W, bevel=0.008, segments=2)
    M.rounded_box(p, (0, 0.01, 0.48), (0.4, 0.38, 0.04), 0.018, "LR_CushionRed", cuts=4, puff=0.006)
    for sx in (-1, 1):
        M.turned_leg(p, sx * 0.19, 0.18, 0, 0.44, W, top_block=0.04, r=0.018)
        M.box(p, (sx * 0.19, -0.19, 0.47), (0.035, 0.035, 0.94), W, bevel=0.006)
    M.box(p, (0, -0.19, 0.88), (0.42, 0.04, 0.08), W, bevel=0.012, segments=2)
    M.box(p, (0, -0.19, 0.66), (0.38, 0.03, 0.04), W, bevel=0.008)
    for i in range(3):
        M.box(p, (-0.1 + i * 0.1, -0.19, 0.77), (0.022, 0.022, 0.2), W, bevel=0.005)
    M.box(p, (0, 0.0, 0.14), (0.36, 0.025, 0.025), W)
    return M.finish(p)


def dining_table(L=1.6, D=1.2):
    p = M.Part("DiningTableArt")
    W = "LR_WoodMid"
    M.box(p, (0, 0, 0.73), (L, D, 0.045), W, bevel=0.014, segments=3)
    M.box(p, (0, 0, 0.66), (L - 0.16, D - 0.16, 0.1), W, bevel=0.005)
    for sx in (-1, 1):
        for sy in (-1, 1):
            M.turned_leg(p, sx * (L / 2 - 0.1), sy * (D / 2 - 0.1), 0, 0.71, "LR_WoodDark", top_block=0.1, r=0.03)
    return M.finish(p)


def sideboard(W=1.6, D=0.46):
    p = M.Part("SideboardArt")
    Wd = "LR_WoodDark"
    M.box(p, (0, 0, 0.47), (W, D, 0.78), Wd, bevel=0.006)
    M.box(p, (0, 0.012, 0.885), (W + 0.06, D + 0.04, 0.04), "LR_WoodMid", bevel=0.012, segments=3)
    for i in range(3):
        x = -W / 3 + i * W / 3
        M.box(p, (x, D / 2 + 0.006, 0.42), (W / 3 - 0.03, 0.012, 0.6), Wd, bevel=0.004)
        M.box(p, (x, D / 2 + 0.014, 0.42), (W / 3 - 0.14, 0.006, 0.46), "LR_WoodMid", bevel=0.006, segments=2)
        M.rounded_box(p, (x, D / 2 + 0.024, 0.78), (0.1, 0.02, 0.02), 0.009, "LR_Brass", cuts=2)
    for sx in (-1, 1):
        M.lathe(p, (sx * (W / 2 - 0.06), 0, 0), [(0.03, 0), (0.035, 0.04), (0.025, 0.08)], Wd, steps=10)
    return M.finish(p)


def desk(W=1.9, D=0.8, name="DeskArt"):
    """雙抽屜櫃書桌：皮革桌墊、兩側抽屜櫃（三層抽屜、黃銅把手）、中間抽屜。原點在地面中心，正面（坐的那一側）＋Y。"""
    p = M.Part(name)
    Wd = "LR_WoodDark"
    M.box(p, (0, 0, 0.745), (W, D, 0.04), Wd, bevel=0.012, segments=3)
    M.box(p, (0, 0.0, 0.768), (W - 0.2, D - 0.2, 0.006), "P1_Leather")
    for sx in (-1, 1):
        x = sx * (W / 2 - 0.25)
        M.box(p, (x, 0, 0.37), (0.48, D - 0.06, 0.7), Wd, bevel=0.006)
        for k in range(3):
            z = 0.14 + k * 0.21
            M.box(p, (x, D / 2 - 0.025, z), (0.42, 0.012, 0.18), "LR_WoodMid", bevel=0.005)
            M.rounded_box(p, (x, D / 2 - 0.012, z + 0.03), (0.1, 0.02, 0.02), 0.009, "LR_Brass", cuts=2)
    M.box(p, (0, D / 2 - 0.025, 0.68), (W - 1.08, 0.012, 0.09), "LR_WoodMid", bevel=0.004)
    M.box(p, (0, -D / 2 + 0.03, 0.4), (W - 1.0, 0.02, 0.6), Wd)
    return M.finish(p)


def office_chair(name="DeskChairArt"):
    p = M.Part(name)
    M.rounded_box(p, (0, 0, 0.47), (0.5, 0.48, 0.1), 0.04, "P1_Leather", cuts=4, puff=0.01)
    M.rounded_box(p, (0, -0.22, 0.82), (0.48, 0.1, 0.6), 0.05, "P1_Leather", cuts=5, puff=0.015, rot=(-8, 0, 0))
    for sx in (-1, 1):
        M.rounded_box(p, (sx * 0.27, -0.02, 0.62), (0.06, 0.42, 0.05), 0.02, "LR_WoodDark", cuts=3)
        M.box(p, (sx * 0.27, 0.12, 0.54), (0.035, 0.035, 0.14), "LR_WoodDark")
    M.lathe(p, (0, 0, 0.06), [(0.03, 0), (0.03, 0.36)], "LR_Iron", steps=10)
    for i in range(5):
        a = 2 * math.pi * i / 5
        M.box(p, (math.cos(a) * 0.15, math.sin(a) * 0.15, 0.05), (0.3, 0.04, 0.03), "LR_Iron", rot=(0, 0, math.degrees(a)))
        M.rounded_box(p, (math.cos(a) * 0.29, math.sin(a) * 0.29, 0.025), (0.04, 0.04, 0.04), 0.018, "LR_Iron", cuts=2)
    return M.finish(p)


def bankers_lamp():
    p = M.Part("BankersLamp")
    M.lathe(p, (0, 0, 0), [(0.0, 0), (0.08, 0), (0.08, 0.02), (0.02, 0.03), (0.015, 0.3)], "LR_Brass", steps=16)
    M.rounded_box(p, (0, 0.04, 0.33), (0.3, 0.12, 0.06), 0.05, "P1_GlassDark", cuts=4)
    return M.finish(p)


def bed(W=1.6, L=2.0):
    """雙人床：床頭板（框＋嵌板）、床尾板、床架、床墊、枕頭、半掀的被子。原點在地面中心，床頭在 -Y（靠牆）。"""
    p = M.Part("BedArt")
    Wd = "LR_WoodMid"
    M.box(p, (0, 0, 0.27), (W, L, 0.18), Wd, bevel=0.01, segments=2)
    M.box(p, (0, -L / 2 + 0.03, 0.58), (W + 0.08, 0.06, 1.0), Wd, bevel=0.012, segments=2)
    M.box(p, (0, -L / 2 + 0.065, 0.62), (W - 0.24, 0.012, 0.6), "LR_WoodDark", bevel=0.006)
    M.box(p, (0, L / 2 - 0.03, 0.34), (W + 0.08, 0.06, 0.56), Wd, bevel=0.012, segments=2)
    for sx in (-1, 1):
        for sy in (-1, 1):
            M.lathe(p, (sx * (W / 2 + 0.01), sy * (L / 2 - 0.03), 0), [(0.035, 0), (0.035, 0.18)], Wd, steps=10)
    M.rounded_box(p, (0, 0.02, 0.43), (W - 0.06, L - 0.12, 0.18), 0.05, "P1_Mattress", cuts=6, puff=0.01)
    for sx in (-1, 1):
        M.rounded_box(p, (sx * W * 0.24, -L / 2 + 0.3, 0.58), (W * 0.42, 0.34, 0.12), 0.05, "P1_Mattress", cuts=6, puff=0.03)
    def duvet(u, v):
        x = (u - 0.5) * (W + 0.06)
        y = -L / 2 + 0.62 + v * (L - 0.7)
        edge = abs(u - 0.5) * 2
        z = 0.54 + 0.02 * math.sin(v * 9 + u * 4) - 0.2 * max(0.0, edge - 0.86) / 0.14
        return (x, y, z)
    M.grid_sheet(p, W, L, 16, 14, duvet, "P1_Bedding", thickness=0.04)
    return M.finish(p, sharp_angle=70)


def wardrobe(W=1.2, H=2.0, D=0.6):
    p = M.Part("WardrobeArt")
    Wd = "LR_WoodMid"
    M.box(p, (0, 0, H / 2 + 0.04), (W, D, H - 0.08), Wd, bevel=0.006)
    M.box(p, (0, 0.012, H + 0.02), (W + 0.06, D + 0.04, 0.05), Wd, bevel=0.012, segments=3)
    M.box(p, (0, 0, 0.04), (W - 0.02, D - 0.02, 0.08), "LR_WoodDark", bevel=0.005)
    for sx in (-1, 1):
        x = sx * W / 4
        M.box(p, (x, D / 2 + 0.006, H / 2 + 0.04), (W / 2 - 0.02, 0.012, H - 0.16), Wd, bevel=0.004)
        for zc, hh in ((H * 0.68, H * 0.45), (H * 0.22, H * 0.25)):
            M.box(p, (x, D / 2 + 0.014, zc), (W / 2 - 0.14, 0.006, hh), "LR_WoodDark", bevel=0.006, segments=2)
        M.rounded_box(p, (sx * 0.04, D / 2 + 0.03, H * 0.5), (0.02, 0.025, 0.12), 0.009, "LR_Brass", cuts=2)
    return M.finish(p)


def nightstand():
    p = M.Part("NightstandArt")
    Wd = "LR_WoodMid"
    M.box(p, (0, 0, 0.33), (0.48, 0.42, 0.5), Wd, bevel=0.006)
    M.box(p, (0, 0.01, 0.6), (0.52, 0.46, 0.03), Wd, bevel=0.01, segments=2)
    M.box(p, (0, 0.215, 0.45), (0.42, 0.012, 0.16), Wd, bevel=0.004)
    M.rounded_box(p, (0, 0.23, 0.45), (0.06, 0.02, 0.02), 0.009, "LR_Brass", cuts=2)
    for sx in (-1, 1):
        for sy in (-1, 1):
            M.lathe(p, (sx * 0.2, sy * 0.17, 0), [(0.02, 0), (0.025, 0.04), (0.02, 0.08)], Wd, steps=10)
    return M.finish(p)


def filing_cabinet():
    p = M.Part("FilingCabinetArt")
    M.box(p, (0, 0, 0.66), (0.48, 0.6, 1.32), "P1_Metal", bevel=0.01, segments=2)
    for k in range(4):
        z = 0.17 + k * 0.32
        M.box(p, (0, 0.302, z), (0.42, 0.012, 0.28), "P1_Metal", bevel=0.006)
        M.box(p, (0, 0.315, z + 0.08), (0.12, 0.02, 0.02), "LR_Brass", bevel=0.004)
        M.box(p, (0, 0.311, z + 0.02), (0.08, 0.004, 0.04), "LR_Linen")
    return M.finish(p)


def terrace_front(W, door_x, windows, name, upper=True, bay=False):
    """英式聯排住宅的立面模組：磚牆（由 Unity 立面方塊提供）之外的建築細節——
    一樓窗框／石窗台／過梁、凸窗（可選）、凹入式門洞＋門楣＋台階、二樓窗、二樓腰線、屋簷線腳、煙囪帽。
    原點在立面底部中心（立面平面 y=0，細節往 +Y 凸出＝街道側）。"""
    p = M.Part(name)
    S, Wp = "P1_Stone", "P1_WoodPaint"
    # door_x／windows 用 Unity 立面的本地 x（立面朝 -Z）。Unity 以 yaw 180 擺放模組（細節 +Y→朝街），會把 x 鏡射，所以這裡先取負號。
    door_x = -door_x
    windows = [-w for w in windows]
    # 一樓窗（或凸窗）。
    for wx in windows:
        if bay:
            M.box(p, (wx, 0.25, 1.3), (1.8, 0.5, 1.9), "P1_Brick", bevel=0.01)
            M.box(p, (wx, 0.52, 1.3), (1.5, 0.04, 1.4), "P1_GlassDark")
            for sx in (-1, 0, 1):
                M.box(p, (wx + sx * 0.5, 0.54, 1.3), (0.06, 0.04, 1.44), Wp, bevel=0.006)
            M.box(p, (wx, 0.3, 2.3), (1.95, 0.62, 0.12), S, bevel=0.012, segments=2)
            M.box(p, (wx, 0.3, 0.32), (1.95, 0.62, 0.1), S, bevel=0.01)
        else:
            M.box(p, (wx, 0.03, 1.5), (1.0, 0.06, 1.2), Wp, bevel=0.006)
            M.box(p, (wx, 0.065, 1.5), (0.88, 0.01, 1.08), "P1_GlassDark")
            M.box(p, (wx, 0.075, 1.5), (0.04, 0.02, 1.08), Wp)
            M.box(p, (wx, 0.075, 1.52), (0.88, 0.02, 0.04), Wp)
            M.box(p, (wx, 0.09, 0.86), (1.2, 0.18, 0.07), S, bevel=0.01, segments=2)
            M.box(p, (wx, 0.04, 2.17), (1.2, 0.08, 0.14), S, bevel=0.008)
    # 門：凹入的門洞（深色門洞面＋石門框）、門楣、兩級台階。
    M.box(p, (door_x, 0.02, 1.1), (1.3, 0.04, 2.35), S, bevel=0.01)
    M.box(p, (door_x, 0.045, 2.42), (1.5, 0.12, 0.14), S, bevel=0.012, segments=2)
    M.box(p, (door_x, 0.22, 0.06), (1.4, 0.44, 0.12), S, bevel=0.008)
    M.box(p, (door_x, 0.45, 0.03), (1.6, 0.3, 0.06), S, bevel=0.006)
    if upper:
        ux = list(windows) + [door_x]
        for wx in ux:
            M.box(p, (wx, 0.03, 4.4), (0.95, 0.06, 1.25), Wp, bevel=0.006)
            M.box(p, (wx, 0.065, 4.4), (0.83, 0.01, 1.13), "P1_GlassDark")
            M.box(p, (wx, 0.075, 4.4), (0.04, 0.02, 1.13), Wp)
            M.box(p, (wx, 0.08, 3.74), (1.1, 0.16, 0.06), S, bevel=0.008)
            M.box(p, (wx, 0.04, 5.1), (1.1, 0.08, 0.12), S, bevel=0.008)
    M.box(p, (0, 0.04, 3.05), (W, 0.08, 0.1), S, bevel=0.006)        # 腰線
    M.box(p, (0, 0.1, 6.15), (W + 0.1, 0.2, 0.18), S, bevel=0.015, segments=3)  # 屋簷線腳
    return M.finish(p)


def lamp_post():
    """維多利亞式路燈：鑄鐵燈柱（基座、凹槽柱身、橫臂）＋燈籠（自發光玻璃由 Unity 指定）。原點在地面，燈臂朝 -Y（街道側）。"""
    p = M.Part("LampPost")
    I = "LR_Iron"
    M.lathe(p, (0, 0, 0), [(0.0, 0), (0.16, 0), (0.16, 0.12), (0.11, 0.2), (0.11, 0.5), (0.07, 0.62), (0.055, 0.7), (0.05, 3.6), (0.07, 3.7), (0.04, 3.8)], I, steps=14)
    M.box(p, (0, -0.32, 3.78), (0.05, 0.64, 0.05), I, bevel=0.01)
    M.lathe(p, (0, -0.62, 3.5), [(0.06, 0), (0.12, 0.05), (0.15, 0.3), (0.08, 0.38), (0.02, 0.42)], "P1_Lantern", steps=8)
    M.lathe(p, (0, -0.62, 3.88), [(0.0, 0.0), (0.17, 0.0), (0.02, 0.12), (0.0, 0.14)], I, steps=8)
    return M.finish(p)


def railing(L=2.0):
    p = M.Part("Railing")
    I = "LR_Iron"
    M.box(p, (0, 0, 0.82), (L, 0.04, 0.04), I, bevel=0.008)
    M.box(p, (0, 0, 0.1), (L, 0.03, 0.03), I)
    n = int(L / 0.12)
    for i in range(n + 1):
        x = -L / 2 + i * L / n
        M.box(p, (x, 0, 0.46), (0.018, 0.018, 0.72), I)
        M.lathe(p, (x, 0, 0.84), [(0.014, 0), (0.025, 0.02), (0.0, 0.07)], I, steps=6)
    return M.finish(p)


def build_models(out):
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    E = lambda objs, n: M.export(objs if isinstance(objs, list) else [objs], os.path.join(out, n + ".fbx"))
    E(front_door(), "FrontDoorArt")
    E(panel_door(), "PanelDoor")
    E(hall_bench(), "HallBench")
    E(coat_rack(), "CoatRack")
    E(shoe_cabinet(), "ShoeCabinetArt")
    E(umbrella_stand(), "UmbrellaStandArt")
    E(kitchen_base(), "KitchenBase")
    E(wall_cabinet(), "WallCabinet")
    E(range_cooker(), "RangeCooker")
    E(butler_sink(), "ButlerSink")
    E(fridge(), "FridgeArt")
    E(kettle_teapot(), "Kettle")
    E(dining_chair(), "DiningChairArt")
    E(dining_table(), "DiningTableArt")
    E(sideboard(), "SideboardArt")
    E(desk(), "DeskArt")
    E(desk(1.5, 0.7, "OfficeDeskArt"), "OfficeDeskArt")
    E(office_chair(), "DeskChairArt")
    E(bankers_lamp(), "BankersLamp")
    E(bed(), "BedArt")
    E(wardrobe(), "WardrobeArt")
    E(nightstand(), "NightstandArt")
    E(filing_cabinet(), "FilingCabinetArt")
    E(terrace_front(6.2, -6.2 * 0.28, [6.2 * 0.18], "TerraceFront"), "TerraceFront")
    E(terrace_front(6.2, -6.2 * 0.28, [6.2 * 0.18], "TerraceFrontBay", bay=True), "TerraceFrontBay")
    E(terrace_front(10.0, 2.1, [-3.4, -1.2], "Front17A", upper=False), "Front17A")
    E(terrace_front(10.0, 2.1, [-2.5], "Front17O", upper=False), "Front17O")
    E(lamp_post(), "LampPost")
    E(railing(), "Railing")


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
repo = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
out = os.path.abspath(argv[0]) if argv else os.path.join(repo, "unity", "Assets", "Exploration", "Case01Art", "Phase1", "Resources", "Case01Phase1")
which = argv[1] if len(argv) > 1 else "all"
os.makedirs(out, exist_ok=True)
if which in ("all", "tex"):
    build_textures(out)
    print("[Case01Phase1] textures ->", out)
if which in ("all", "models"):
    build_models(out)
    print("[Case01Phase1] models ->", out)
