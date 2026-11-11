"""Case01 全屋重製：PBR 貼圖（numpy 生成，在 Blender 內存檔）。

每個材質輸出三張：
  <名稱>_A.jpg   albedo（sRGB）
  <名稱>_N.png   法線（OpenGL／Unity 慣例，由高度圖推導）
  <名稱>_MS.png  R＝金屬度、A＝光滑度（Unity Standard 的 Metallic/Smoothness 貼圖）
全部原創紋樣（不複製任何真實品牌或既有壁紙設計），可商用。
"""
import math
import os

import numpy as np

SEED = 2611


def rng(k):
    return np.random.default_rng(SEED + k)


def blur(a, r):
    if r <= 0:
        return a
    out = a
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r, r)
        p = np.pad(out, pad, mode="wrap")
        c = np.cumsum(p, axis=axis)
        c = np.concatenate([np.zeros_like(np.take(c, [0], axis=axis)), c], axis=axis)
        n = out.shape[axis]
        hi = np.take(c, np.arange(n) + 2 * r + 1, axis=axis)
        lo = np.take(c, np.arange(n), axis=axis)
        out = (hi - lo) / (2 * r + 1)
    return out


def tile_noise(n, cells, k, smooth=2):
    """可無縫平鋪的值雜訊（格點雜訊放大＋環狀模糊）。"""
    g = rng(k).random((cells, cells))
    blk = -(-n // cells)
    a = np.kron(g, np.ones((blk, blk)))[:n, :n]
    for _ in range(smooth):
        a = blur(a, max(1, n // cells // 2))
    return (a - a.min()) / max(1e-6, a.max() - a.min())


def fbm(n, k, octaves=((4, 0.5), (16, 0.3), (64, 0.2))):
    a = np.zeros((n, n))
    for i, (c, amp) in enumerate(octaves):
        a += tile_noise(n, c, k * 13 + i) * amp
    return a / sum(o[1] for o in octaves)


def aniso(n, cells_along, cells_across, k, smooth=1, along_axis=0):
    """拉長的雜訊（木紋纖維）：沿 along_axis 少格、橫向多格。along_axis=0＝沿 y（列方向）延伸。"""
    g = rng(k).random((cells_along, cells_across) if along_axis == 0 else (cells_across, cells_along))
    by, bx = -(-n // g.shape[0]), -(-n // g.shape[1])
    a = np.kron(g, np.ones((by, bx)))[:n, :n]
    # 沿纖維方向大幅平滑（消除格子接縫）、橫向只輕微平滑。
    along_r = (by if along_axis == 0 else bx) // 2
    across_r = max(1, (bx if along_axis == 0 else by) // 3) if smooth else 0
    for _ in range(2):
        a = blur1d(a, along_r, along_axis)
    if across_r:
        a = blur1d(a, across_r, 1 - along_axis)
    return (a - a.min()) / max(1e-6, a.max() - a.min())


def blur1d(a, r, axis):
    if r <= 0:
        return a
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r, r)
    p = np.pad(a, pad, mode="wrap")
    c = np.cumsum(p, axis=axis)
    c = np.concatenate([np.zeros_like(np.take(c, [0], axis=axis)), c], axis=axis)
    n = a.shape[axis]
    return (np.take(c, np.arange(n) + 2 * r + 1, axis=axis) - np.take(c, np.arange(n), axis=axis)) / (2 * r + 1)


def ramp(t, stops):
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops[:-1], stops[1:]):
        m = (t >= p0) & (t <= p1)
        u = ((t - p0) / max(1e-6, p1 - p0))[m]
        out[m] = np.asarray(c0) * (1 - u[:, None]) + np.asarray(c1) * u[:, None]
    return out


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([nx / l, ny / l, nz / l], axis=2) * 0.5 + 0.5


def save_img(rgb, path, alpha=None, jpeg=False):
    import bpy
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=alpha is not None)
    a = np.ones((h, w)) if alpha is None else alpha
    px = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)[..., None]], axis=2)
    img.pixels.foreach_set(px[::-1].astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'JPEG' if jpeg else 'PNG'
    if jpeg:
        bpy.context.scene.render.image_settings.quality = 92
    img.save()
    bpy.data.images.remove(img)


def save_set(out, name, albedo, height, rough, metal=None, nstrength=4.0):
    n = albedo.shape[0]
    save_img(albedo, os.path.join(out, name + "_A.jpg"), jpeg=True)
    save_img(normal_from_height(height, nstrength), os.path.join(out, name + "_N.png"))
    m = np.zeros((rough.shape[0], rough.shape[1])) if metal is None else metal
    ms = np.stack([m, m, m], axis=2)
    save_img(ms, os.path.join(out, name + "_MS.png"), alpha=1.0 - rough)
    print("[Case01Rebuild] pbr", name)


# ---------------------------------------------------------------- 材質

def floorboards(n, k):
    """舊松木地板（一張貼圖＝ 1 m × 1 m：7 條板寬約 14 cm、錯開的接縫、年輪、磨亮的走道、縫裡的灰塵）。"""
    r = rng(k)
    y, x = np.mgrid[0:n, 0:n] / n
    rows = 7
    row = np.floor(y * rows).astype(int)
    fy = (y * rows) % 1.0
    offs = r.random(rows)
    seg = ((x + offs[row]) * 1.0) % 1.0
    joint = (seg < 0.006) | (seg > 0.994)
    gap = (fy < 0.025) | (fy > 0.975)
    tone = r.random(rows * 4)
    boardid = row * 4 + np.floor((x + offs[row]) * 1.0).astype(int) % 4
    grain = aniso(n, 96, 4, k + 1, smooth=1, along_axis=1) * 0.6 + aniso(n, 384, 8, k + 4, smooth=0, along_axis=1) * 0.4
    rings = np.sin((fy * 2.5 + aniso(n, 6, 2, k + 2, smooth=2, along_axis=1) * 0.8 + boardid * 0.37) * math.pi * 2) * 0.5 + 0.5
    rings = rings ** 3
    t = 0.35 + 0.25 * tone[boardid] + 0.15 * grain + 0.1 * rings
    alb = ramp(t, [(0, (0.24, 0.13, 0.07)), (0.5, (0.42, 0.25, 0.13)), (1, (0.6, 0.4, 0.22))])
    wear = fbm(n, k + 3)
    alb *= (0.9 + 0.15 * wear)[..., None]
    alb[gap | joint] *= 0.25
    h = 0.6 + 0.1 * grain + 0.05 * rings
    h[gap | joint] = 0.0
    h = blur(h, 1)
    rough = 0.55 - 0.18 * wear + 0.1 * (gap | joint)
    return alb, h, rough


def wallpaper(n, k):
    """原創壁紙：暗綠底、細直條與小葉形印花（凸版壓紋），舊紙的斑駁與淡褪。一張＝ 0.53 m 寬。"""
    y, x = np.mgrid[0:n, 0:n] / n
    base = np.asarray((0.2, 0.27, 0.22))
    alb = np.ones((n, n, 3)) * base
    stripe = (np.abs(((x * 8) % 1.0) - 0.5) < 0.03)
    alb[stripe] = (0.27, 0.33, 0.26)
    # 葉形：每張 4×4 格、交錯排列的小枝（三片葉＋一顆小花苞），葉脈細線。
    motif = np.zeros((n, n))
    rep = 4
    gx, gy = (x * rep) % 1.0, (y * rep) % 1.0
    cell = (np.floor(y * rep).astype(int) % 2)
    gx = (gx + cell * 0.5) % 1.0
    for (cx, cy, a) in ((0.5, 0.35, 0.5), (0.36, 0.55, 2.4), (0.64, 0.58, 0.8)):
        dx, dy = gx - cx, gy - cy
        u = dx * math.cos(a) + dy * math.sin(a)
        v = -dx * math.sin(a) + dy * math.cos(a)
        leaf = (u / 0.16) ** 2 + (v / 0.06) ** 2 < 1
        motif = np.maximum(motif, leaf.astype(float))
        motif[(np.abs(v) < 0.006) & (np.abs(u) < 0.15)] = 0.55
    stem = (np.abs(gx - 0.5) < 0.008) & (gy > 0.35) & (gy < 0.75)
    motif[stem] = 0.8
    bud = (gx - 0.5) ** 2 + (gy - 0.22) ** 2 < 0.04 ** 2
    motif[bud] = 1.0
    motif = blur(motif, 1)
    alb = alb * (1 - motif[..., None]) + motif[..., None] * np.asarray((0.62, 0.55, 0.36))
    age = fbm(n, k + 1)
    alb *= (0.88 + 0.16 * age)[..., None]
    alb = alb * 0.92 + 0.08 * np.asarray((0.5, 0.45, 0.35))
    h = motif * 0.6 + fbm(n, k + 2, ((64, 0.6), (128, 0.4))) * 0.15
    rough = 0.82 - motif * 0.12
    return alb, h, rough


def paint(n, k, color, rough_base=0.7):
    """舊漆／灰泥：微小的刷痕與起伏、輕微泛黃。"""
    f = fbm(n, k)
    brush = blur(rng(k).random((n, n)), 2)
    alb = np.ones((n, n, 3)) * np.asarray(color) * (0.95 + 0.06 * f)[..., None]
    h = f * 0.3 + brush * 0.25
    rough = rough_base + 0.1 * (f - 0.5)
    return alb, h, rough


def trim_paint(n, k):
    return paint(n, k, (0.86, 0.83, 0.74), 0.38)


def stained_wood(n, k, dark=(0.12, 0.06, 0.03), light=(0.36, 0.2, 0.1)):
    """直紋木材：沿 y 的長纖維＋少量平行的年輪帶，沒有大理石般的漩渦。"""
    y, x = np.mgrid[0:n, 0:n] / n
    fib = aniso(n, 3, 96, k, smooth=1, along_axis=0)
    fine = aniso(n, 8, 384, k + 1, smooth=0, along_axis=0)
    wob = aniso(n, 2, 6, k + 2, smooth=2, along_axis=0)
    rings = np.sin((x * 9 + wob * 0.6) * math.pi * 2) * 0.5 + 0.5
    t = 0.45 * fib + 0.2 * fine + 0.35 * rings ** 3
    alb = ramp(t, [(0, dark), (1, light)])
    h = fine * 0.3 + fib * 0.2
    rough = 0.45 - 0.08 * fib
    return alb, h, rough

def oak(n, k):
    return stained_wood(n, k, (0.3, 0.18, 0.09), (0.58, 0.4, 0.22))


def cast_iron(n, k, color=(0.07, 0.07, 0.075)):
    f = fbm(n, k)
    pits = (blur(rng(k).random((n, n)), 1) > 0.7).astype(float)
    alb = np.ones((n, n, 3)) * np.asarray(color) * (0.85 + 0.3 * f)[..., None]
    h = f * 0.3 - pits * 0.2
    rough = 0.55 + 0.2 * f
    metal = np.ones((n, n)) * 0.6
    return alb, h, rough, metal


def brass(n, k):
    f = fbm(n, k, ((16, 0.4), (64, 0.35), (128, 0.25)))
    tarnish = np.clip((f - 0.55) * 2.0, 0, 1) * 0.6
    alb = np.ones((n, n, 3)) * np.asarray((0.78, 0.6, 0.3))
    alb = alb * (1 - tarnish[..., None] * 0.55) + tarnish[..., None] * 0.55 * np.asarray((0.3, 0.25, 0.14))
    rough = 0.25 + 0.35 * tarnish
    metal = np.ones((n, n)) * 0.95 - tarnish * 0.3
    return alb, f * 0.1, rough, metal


def velvet(n, k, base=(0.12, 0.22, 0.16)):
    """天鵝絨：方向性的光澤斑塊（被坐過、被手摸過的倒毛）、細密絨面。"""
    nap = fbm(n, k, ((3, 0.6), (8, 0.4)))
    fine = blur(rng(k).random((n, n)), 1)
    alb = np.ones((n, n, 3)) * np.asarray(base) * (0.75 + 0.5 * nap)[..., None] * (0.95 + 0.1 * fine)[..., None]
    h = fine * 0.4
    rough = 0.6 - 0.25 * nap
    return alb, h, rough


def weave(n, k, base, shade, pitch=96):
    y, x = np.mgrid[0:n, 0:n]
    wx = np.sin(x * 2 * math.pi * pitch / n) * 0.5 + 0.5
    wy = np.sin(y * 2 * math.pi * pitch / n) * 0.5 + 0.5
    over = ((x * pitch // n + y * pitch // n) % 2).astype(float)
    t = over * wx + (1 - over) * wy
    f = fbm(n, k)
    alb = ramp(t * 0.6 + f * 0.4, [(0, shade), (1, base)])
    h = t * 0.6 + f * 0.2
    rough = 0.85 - 0.05 * t
    return alb, h, rough


def tweed(n, k):
    """扶手椅：灰褐人字紋粗呢。"""
    y, x = np.mgrid[0:n, 0:n]
    band = (x * 16 // n) % 2
    diag = np.where(band == 0, (x + y), (x - y)) * 24 / n
    t = (np.sin(diag * 2 * math.pi) * 0.5 + 0.5)
    fleck = (rng(k).random((n, n)) > 0.985).astype(float)
    f = fbm(n, k)
    alb = ramp(t * 0.5 + f * 0.5, [(0, (0.22, 0.19, 0.16)), (1, (0.46, 0.4, 0.33))])
    alb = alb * (1 - fleck[..., None]) + fleck[..., None] * np.asarray((0.62, 0.34, 0.2))
    h = t * 0.5 + f * 0.2
    return alb, h, 0.88 - 0.04 * t


def rug(n, k):
    """原創地毯紋樣：深酒紅地、藏青與駝色的邊框、中央菱形徽章；絨毛起伏與磨薄的區域。"""
    y, x = np.mgrid[0:n, 0:n] / n
    alb = np.ones((n, n, 3)) * np.asarray((0.36, 0.08, 0.08))
    d = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    alb[d < 0.1] = (0.1, 0.12, 0.22)
    alb[(d > 0.06) & (d < 0.075)] = (0.66, 0.52, 0.32)
    alb[(d > 0.115) & (d < 0.125)] = (0.66, 0.52, 0.32)
    cx, cy = np.abs(x - 0.5), np.abs(y - 0.5)
    m = cx + cy * 1.3
    alb[(m > 0.2) & (m < 0.23)] = (0.66, 0.52, 0.32)
    alb[m < 0.12] = (0.1, 0.12, 0.22)
    alb[(m < 0.06)] = (0.66, 0.52, 0.32)
    # 邊框裡的小鉤紋。
    hook = (np.sin(x * 2 * math.pi * 20) * np.sin(y * 2 * math.pi * 20) > 0.6) & (d < 0.055) & (d > 0.015)
    alb[hook] = (0.62, 0.5, 0.3)
    pile = blur(rng(k).random((n, n)), 1)
    worn = np.clip((fbm(n, k + 1) - 0.5) * 2, 0, 1) * (d > 0.13)
    alb *= (0.85 + 0.2 * pile)[..., None]
    alb = alb * (1 - 0.3 * worn[..., None]) + 0.3 * worn[..., None] * np.asarray((0.4, 0.3, 0.22))
    return alb, pile * 0.5 - worn * 0.2, 0.92 + 0 * pile


def tiles(n, k, base=(0.16, 0.3, 0.3), motif=(0.62, 0.5, 0.28)):
    """壁爐側邊的維多利亞式釉面磚（原創花形）：一張＝兩塊 15 cm 磚。"""
    y, x = np.mgrid[0:n, 0:n] / n
    tx, ty = (x * 1) % 1.0, (y * 2) % 1.0
    alb = np.ones((n, n, 3)) * np.asarray(base)
    r = np.sqrt((tx - 0.5) ** 2 + ((ty - 0.5) * 1.0) ** 2)
    petal = np.cos(np.arctan2(ty - 0.5, tx - 0.5) * 5) * 0.08 + 0.22
    flower = r < petal
    alb[flower] = motif
    alb[r < 0.07] = (0.55, 0.18, 0.12)
    grout = (tx < 0.02) | (tx > 0.98) | (ty < 0.02) | (ty > 0.98)
    alb[grout] = (0.55, 0.52, 0.46)
    craze = (blur(rng(k).random((n, n)), 1) > 0.78) * 0.06
    alb -= craze[..., None]
    h = np.where(grout, 0.0, 0.7) + flower * 0.05
    return alb, blur(h, 1), np.where(grout, 0.9, 0.12)


def books_atlas(n, k):
    """書脊圖集：8 欄 × 4 列，每格一本書（布面／皮面、燙金線、褪色），UV 依格選取。"""
    r = rng(k)
    alb = np.zeros((n, n, 3))
    h = np.zeros((n, n))
    cols, rows = 8, 4
    cw, ch = n // cols, n // rows
    palette = [(0.32, 0.08, 0.07), (0.1, 0.16, 0.26), (0.13, 0.22, 0.14), (0.5, 0.36, 0.2), (0.6, 0.55, 0.45), (0.18, 0.14, 0.12), (0.42, 0.12, 0.1), (0.25, 0.3, 0.32)]
    for i in range(cols):
        for j in range(rows):
            c = np.asarray(palette[r.integers(0, len(palette))]) * (0.75 + 0.4 * r.random())
            block = np.ones((ch, cw, 3)) * c
            yy = np.arange(ch)[:, None] / ch
            for band in r.choice([0.1, 0.16, 0.8, 0.86], size=r.integers(1, 4), replace=False):
                block[(np.abs(yy - band) < 0.008).repeat(cw, axis=1)] = (0.72, 0.58, 0.3)
            label = (yy > 0.3) & (yy < 0.36 + 0.06 * r.random())
            block[label.repeat(cw, axis=1)] = block[label.repeat(cw, axis=1)] * 0.6 + 0.4 * np.asarray((0.8, 0.75, 0.62))
            fade = np.linspace(0.85, 1.05, cw)[None, :, None]
            alb[j * ch:(j + 1) * ch, i * cw:(i + 1) * cw] = block * fade
            hh = np.ones((ch, cw)) * 0.5
            hh[:, :2] = 0.2
            hh[:, -2:] = 0.2
            h[j * ch:(j + 1) * ch, i * cw:(i + 1) * cw] = hh
    alb *= (0.9 + 0.15 * fbm(n, k))[..., None]
    return alb, h, 0.7 + 0 * h


def linen(n, k, base=(0.82, 0.76, 0.64)):
    return weave(n, k, base, tuple(c * 0.8 for c in base), pitch=128)


def plaid(n, k):
    y, x = np.mgrid[0:n, 0:n] / n
    alb = np.ones((n, n, 3)) * np.asarray((0.5, 0.42, 0.3))
    for c, w, col in [(0.2, 0.08, (0.18, 0.22, 0.32)), (0.6, 0.12, (0.42, 0.12, 0.1)), (0.85, 0.02, (0.8, 0.72, 0.5))]:
        mx = np.abs(((x - c) % 1.0)) < w
        my = np.abs(((y - c) % 1.0)) < w
        alb[mx] = alb[mx] * 0.5 + 0.5 * np.asarray(col)
        alb[my] = alb[my] * 0.5 + 0.5 * np.asarray(col)
    wv, hh, rr = weave(n, k, (1, 1, 1), (0.8, 0.8, 0.8), pitch=128)
    return alb * wv, hh, rr


def curtain(n, k):
    alb, h, r = weave(n, k, (0.55, 0.42, 0.24), (0.42, 0.3, 0.16), pitch=160)
    return alb, h, r


def shade(n, k):
    return weave(n, k, (0.86, 0.78, 0.6), (0.72, 0.62, 0.45), pitch=140)


def ceramic(n, k, base=(0.86, 0.84, 0.78)):
    f = fbm(n, k)
    craze = (blur(rng(k).random((n, n)), 1) > 0.82) * 0.04
    alb = np.ones((n, n, 3)) * np.asarray(base) * (0.97 + 0.04 * f)[..., None] - craze[..., None]
    return alb, f * 0.05, 0.12 + 0 * f


def painting(n, k, sky, land):
    """原創油畫（風景，筆觸）：地平線、遠山、近處的草與水。"""
    y, x = np.mgrid[0:n, 0:n] / n
    hor = 0.55 + 0.05 * np.sin(x * 7 + k)
    alb = np.where((y < hor)[..., None], ramp(y / hor, [(0, land[0]), (1, land[1])]), ramp((y - hor) / (1 - hor), [(0, sky[0]), (1, sky[1])]))
    hills = (y > hor) & (y < hor + 0.08 + 0.05 * np.sin(x * 11 + k * 2))
    alb[hills] = alb[hills] * 0.6 + 0.4 * np.asarray(land[1]) * 0.7
    stroke = blur(rng(k).random((n, n)), 2)
    streak = blur(np.roll(rng(k + 1).random((n, n)), 3, axis=1), 1)
    alb *= (0.85 + 0.3 * stroke)[..., None]
    alb = alb * 0.9 + 0.1 * streak[..., None]
    craq = (blur(rng(k + 2).random((n, n)), 1) > 0.8) * 0.05
    alb -= craq[..., None]
    alb = alb * np.asarray((1.05, 0.98, 0.85))
    return alb, stroke * 0.5, 0.45 + 0 * stroke


def gilt(n, k):
    alb, h, r, m = brass(n, k + 9)
    return alb * np.asarray((1.05, 0.92, 0.65)), h, r * 0.9 + 0.1, m


def leaf(n, k):
    f = fbm(n, k)
    vein = (np.abs(((np.mgrid[0:n, 0:n][1] / n) - 0.5)) < 0.01)
    alb = ramp(f, [(0, (0.08, 0.18, 0.07)), (1, (0.2, 0.36, 0.14))])
    alb[vein] *= 1.5
    return alb, f * 0.2, 0.55 + 0 * f


def build_all(out):
    os.makedirs(out, exist_ok=True)
    jobs = [
        ("RB_Floor", 1024, lambda n: floorboards(n, 1), 6.0),
        ("RB_Wallpaper", 1024, lambda n: wallpaper(n, 2), 5.0),
        ("RB_Paint", 512, lambda n: paint(n, 3, (0.84, 0.8, 0.7)), 2.0),
        ("RB_Ceiling", 512, lambda n: paint(n, 4, (0.88, 0.86, 0.8), 0.85), 1.5),
        ("RB_Trim", 512, lambda n: trim_paint(n, 5), 2.0),
        ("RB_Mantel", 512, lambda n: stained_wood(n, 6), 3.0),
        ("RB_Oak", 512, lambda n: oak(n, 7), 3.0),
        ("RB_Velvet", 1024, lambda n: velvet(n, 8), 2.0),
        ("RB_Tweed", 512, lambda n: tweed(n, 9), 3.0),
        ("RB_Rug", 1024, lambda n: rug(n, 10), 3.0),
        ("RB_Tile", 512, lambda n: tiles(n, 11), 4.0),
        ("RB_HearthTile", 512, lambda n: tiles(n, 12, (0.28, 0.12, 0.08), (0.2, 0.16, 0.12)), 4.0),
        ("RB_Books", 1024, lambda n: books_atlas(n, 13), 3.0),
        ("RB_Linen", 512, lambda n: linen(n, 14), 2.0),
        ("RB_Plaid", 512, lambda n: plaid(n, 15), 2.0),
        ("RB_Curtain", 512, lambda n: curtain(n, 16), 2.0),
        ("RB_Shade", 512, lambda n: shade(n, 17), 1.5),
        ("RB_Ceramic", 256, lambda n: ceramic(n, 18), 1.0),
        ("RB_Painting1", 512, lambda n: painting(n, 19, ((0.75, 0.72, 0.6), (0.45, 0.55, 0.62)), ((0.22, 0.26, 0.12), (0.4, 0.42, 0.24))), 2.0),
        ("RB_Painting2", 512, lambda n: painting(n, 20, ((0.85, 0.6, 0.4), (0.4, 0.42, 0.55)), ((0.2, 0.18, 0.2), (0.35, 0.3, 0.28))), 2.0),
        ("RB_Leaf", 256, lambda n: leaf(n, 21), 1.0),
        ("RB_Cushion", 512, lambda n: linen(n, 22, (0.62, 0.3, 0.22)), 2.0),
    ]
    for name, n, fn, s in jobs:
        alb, h, r = fn(n)
        save_set(out, name, alb, h, r, nstrength=s)
    for name, n, fn in [("RB_CastIron", 512, lambda n: cast_iron(n, 30)), ("RB_Brass", 256, lambda n: brass(n, 31)), ("RB_Gilt", 256, lambda n: gilt(n, 32)),
                        ("RB_Radiator", 256, lambda n: cast_iron(n, 33, (0.78, 0.76, 0.7)))]:
        alb, h, r, m = fn(n)
        if name == "RB_Radiator":
            m = m * 0.0
            r = r * 0.6 + 0.1
        save_set(out, name, alb, h, r, m, 2.0)
