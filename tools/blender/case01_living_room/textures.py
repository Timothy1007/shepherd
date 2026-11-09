"""Case01 v0.6 客廳美術標準間：程序產生的繪畫感貼圖（numpy，於 Blender 內執行以存成 PNG）。

風格：stylized semi-realistic，大面筆觸／色塊概括、低高頻；沒有照片素材、沒有外部貼圖來源。
所有貼圖都由固定亂數種子產生，可重建。
"""
import math
import os

import numpy as np

RNG_SEED = 1717


def _rng(k):
    return np.random.default_rng(RNG_SEED + k)


def blur(a, r):
    """沿兩軸的盒狀模糊（可重複疊加近似高斯；邊界環繞，貼圖可無縫）。"""
    if r <= 0:
        return a
    out = a
    for axis in (0, 1):
        c = np.cumsum(np.concatenate([out.take(range(-r, 0), axis=axis), out, out.take(range(0, r + 1), axis=axis)], axis=axis), axis=axis)
        n = out.shape[axis]
        idx = np.arange(n)
        hi = np.take(c, idx + 2 * r + 1, axis=axis)
        lo = np.take(c, idx, axis=axis)
        out = (hi - lo) / (2 * r + 1)
    return out


def noise(h, w, cells_y, cells_x, k, smooth=2):
    """可無縫的值雜訊：低解析亂數 → 最近鄰放大 → 模糊。"""
    g = _rng(k).random((cells_y, cells_x))
    a = np.kron(g, np.ones((h // cells_y, w // cells_x)))
    for _ in range(smooth):
        a = blur(a, max(1, min(h // cells_y, w // cells_x) // 2))
    a = (a - a.min()) / max(1e-6, a.max() - a.min())
    return a


def fbm(h, w, k, octaves=((4, 4, 0.5), (16, 16, 0.3), (64, 64, 0.2))):
    a = np.zeros((h, w))
    for i, (cy, cx, amp) in enumerate(octaves):
        a += noise(h, w, cy, cx, k * 31 + i) * amp
    return a / sum(o[2] for o in octaves)


def lerp(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return np.asarray(a) * (1 - t) + np.asarray(b) * t


def ramp(t, stops):
    """t（0～1）依色標內插。stops：[(pos, (r,g,b)), ...]"""
    t = np.clip(t, 0, 1)
    out = np.zeros(t.shape + (3,))
    for (p0, c0), (p1, c1) in zip(stops[:-1], stops[1:]):
        m = (t >= p0) & (t <= p1)
        u = ((t - p0) / max(1e-6, p1 - p0))[m]
        out[m] = np.asarray(c0) * (1 - u[:, None]) + np.asarray(c1) * u[:, None]
    return out


def posterize(rgb, levels=18, mix=0.35):
    """輕度色階化：讓色面像筆觸一樣成塊，但不至於變成卡通。"""
    q = np.round(rgb * levels) / levels
    return rgb * (1 - mix) + q * mix


def save(rgb, path, alpha=None):
    import bpy
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=alpha is not None)
    a = np.ones((h, w)) if alpha is None else alpha
    px = np.concatenate([np.clip(rgb, 0, 1), a[..., None]], axis=2)
    img.pixels.foreach_set(px[::-1].astype(np.float32).ravel())  # Blender 影像原點在左下
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


# ---------------------------------------------------------------- 木材

def wood(n, k, dark, light, grain=9.0):
    """木紋沿 U（水平）方向：低頻彎曲的年輪帶＋拉長的筆觸。"""
    y, x = np.mgrid[0:n, 0:n] / n
    warp = noise(n, n, 4, 2, k) * 1.6 + noise(n, n, 16, 4, k + 1) * 0.35
    rings = 0.5 + 0.5 * np.sin((y + warp * 0.08) * grain * 2 * math.pi)
    rings = rings ** 1.3
    streak = noise(n, n, 64, 4, k + 2, smooth=1)
    t = np.clip(rings * 0.28 + streak * 0.42 + fbm(n, n, k + 3) * 0.3, 0, 1)
    rgb = lerp(dark, light, t)
    rgb *= (0.9 + 0.12 * noise(n, n, 8, 8, k + 4))[..., None]
    return posterize(rgb, 20, 0.3)


def floorboards(n, k):
    """1024 px＝1.2 m 見方：寬約 15 cm 的長條木地板，每條色調不同、接縫錯開、縫隙略暗。"""
    rgb = np.zeros((n, n, 3))
    board_w = n // 8
    rng = _rng(k)
    base = wood(n, k + 10, (0.20, 0.12, 0.075), (0.42, 0.27, 0.16), grain=30)
    for i in range(8):
        y0, y1 = i * board_w, (i + 1) * board_w
        x = int(rng.integers(0, n))
        lengths = rng.integers(n // 3, n, size=4)
        seg_start = x
        for L in lengths:
            tint = 0.8 + 0.35 * rng.random()
            hue = np.array([1.0, 0.95 + 0.08 * rng.random(), 0.9 + 0.1 * rng.random()])
            cols = (np.arange(seg_start, seg_start + L) % n)
            shift = int(rng.integers(0, n))
            rgb[y0:y1, cols] = np.roll(base, shift, axis=1)[y0:y1][:, cols] * tint * hue
            rgb[y0:y1, cols[:3]] *= 0.55  # 接縫
            seg_start += L
        rgb[y0:y0 + 3, :] *= 0.5  # 長邊縫隙
    rgb *= (0.88 + 0.18 * fbm(n, n, k + 20))[..., None]
    return posterize(rgb, 22, 0.25)


# ---------------------------------------------------------------- 布料

def fabric(n, k, base, shade, weave=96, mottle=0.25):
    y, x = np.mgrid[0:n, 0:n]
    w = (np.sin(x / n * weave * 2 * math.pi) * np.sin(y / n * weave * 2 * math.pi)) * 0.5 + 0.5
    t = 0.55 + (fbm(n, n, k) - 0.5) * mottle * 2 + (w - 0.5) * 0.12
    t += (noise(n, n, 128, 16, k + 5, smooth=1) - 0.5) * 0.08
    rgb = lerp(shade, base, np.clip(t, 0, 1))
    return posterize(rgb, 24, 0.25)


def plaid(n, k):
    y, x = np.mgrid[0:n, 0:n] / n
    def bands(c):
        v = np.zeros_like(c)
        for p, wdt, s in ((0.12, 0.06, 1.0), (0.37, 0.025, 0.6), (0.62, 0.08, 0.9), (0.86, 0.02, 0.5)):
            v += s * np.clip(1 - np.abs(((c - p + 0.5) % 1) - 0.5) / wdt, 0, 1)
        return np.clip(v, 0, 1)
    bx, by = bands(x * 2 % 1), bands(y * 2 % 1)
    base = np.array([0.55, 0.47, 0.38])
    red = np.array([0.42, 0.12, 0.12])
    cream = np.array([0.82, 0.76, 0.62])
    rgb = base + (red - base) * np.maximum(bx, by)[..., None] * 0.85
    rgb = rgb + (cream - rgb) * (bx * by)[..., None] * 0.6
    rgb *= (0.88 + 0.2 * fabric(n, k, (1, 1, 1), (0.6, 0.6, 0.6), 160, 0.15)[..., 0])[..., None]
    return posterize(rgb, 20, 0.3)


def damask(n, k, bg, fg, contrast=0.35):
    """低對比的大花紋（窗簾、壁紙）：由正弦組合出的對稱花型，邊緣柔化，避免高頻。"""
    y, x = np.mgrid[0:n, 0:n] / n
    u = (x * 2) % 1 - 0.5
    v = (y * 2) % 1 - 0.5
    u2 = np.abs(u)
    petal = np.cos(u2 * 7.0) * np.cos(v * 6.0) + 0.6 * np.cos(np.hypot(u2 - 0.2, v + 0.15) * 14)
    motif = np.clip((petal - 0.35) * 2.2, 0, 1)
    motif = blur(motif, 2)
    t = motif * contrast + (fbm(n, n, k) - 0.5) * 0.25
    rgb = lerp(bg, fg, np.clip(t + 0.1, 0, 1))
    return posterize(rgb, 22, 0.3)


def rug(n, k):
    """深紅＋赭黃＋藏青的英式地毯：外框兩道邊、中央花心、場地低頻菱格；舊化磨損。"""
    y, x = np.mgrid[0:n, 0:n] / n
    cx, cy = x - 0.5, y - 0.5
    border = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    red = np.array([0.36, 0.08, 0.08])
    deep = np.array([0.2, 0.05, 0.06])
    ochre = np.array([0.62, 0.42, 0.2])
    navy = np.array([0.12, 0.13, 0.22])
    cream = np.array([0.72, 0.62, 0.46])
    diamond = (np.abs(((cx * 6) % 1) - 0.5) + np.abs(((cy * 4) % 1) - 0.5))
    field = lerp(red, deep, np.clip(np.cos(diamond * 9) * 0.5 + 0.5, 0, 1) * 0.6)
    r = np.hypot(cx * 1.25, cy)
    med = np.clip(1 - r / 0.22, 0, 1)
    ang = np.arctan2(cy, cx * 1.25)
    petals = 0.5 + 0.5 * np.cos(ang * 8)
    field = field + (navy - field) * np.clip(med * 2.2, 0, 1)[..., None] * 0.85
    field = field + (ochre - field) * (np.clip((med - 0.25) * 4, 0, 1) * petals)[..., None] * 0.8
    field = field + (cream - field) * np.clip((med - 0.75) * 6, 0, 1)[..., None]
    out = field.copy()
    b1 = (border < 0.09) & (border > 0.06)
    b2 = border < 0.05
    out[b2] = (navy * 0.9 + ochre * 0.1)
    out[(border >= 0.05) & (border < 0.09)] = ochre * 0.85
    out[b1] = cream * 0.8
    motif = (np.abs(np.sin((x + y) * 40)) > 0.7) & b2
    out[motif] = red * 1.2
    wear = fbm(n, n, k + 3)
    out *= (0.82 + 0.25 * wear)[..., None]
    out = out * (1 - 0.15 * np.clip((0.45 - np.hypot(cx, cy)) * 2, 0, 1))[..., None] + 0.0
    return posterize(out, 18, 0.35)


# ---------------------------------------------------------------- 其他

def plaster(n, k, base):
    rgb = np.ones((n, n, 3)) * np.asarray(base)
    rgb *= (0.9 + 0.14 * fbm(n, n, k))[..., None]
    rgb *= (0.96 + 0.06 * noise(n, n, 32, 32, k + 3))[..., None]
    return posterize(rgb, 26, 0.25)


def brass(n, k):
    t = noise(n, n, 64, 2, k, smooth=1) * 0.5 + fbm(n, n, k + 1) * 0.5
    return posterize(ramp(t, [(0, (0.35, 0.24, 0.08)), (0.5, (0.7, 0.52, 0.22)), (1, (0.9, 0.76, 0.42))]), 18, 0.3)


def iron(n, k):
    t = fbm(n, n, k)
    return ramp(t, [(0, (0.04, 0.04, 0.045)), (0.6, (0.1, 0.095, 0.1)), (1, (0.18, 0.15, 0.13))])


def hearth_tiles(n, k):
    y, x = np.mgrid[0:n, 0:n] / n
    cells = 8
    cx = np.floor(x * cells)
    cy = np.floor(y * cells)
    checker = ((cx + cy) % 2)[..., None]
    a = np.array([0.42, 0.16, 0.1])
    b = np.array([0.55, 0.45, 0.3])
    rgb = a * checker + b * (1 - checker)
    grout = (np.minimum((x * cells) % 1, (y * cells) % 1) < 0.05)
    rgb[grout] = (0.12, 0.11, 0.1)
    rgb *= (0.85 + 0.25 * fbm(n, n, k))[..., None]
    return posterize(rgb, 18, 0.3)


def books_atlas(n, k):
    """8 欄書背（每欄一種顏色、燙金線與書名塊），右側 1/8 欄是書頁顏色。UV 依欄位選色。"""
    rgb = np.zeros((n, n, 3))
    cols = [(0.35, 0.1, 0.09), (0.12, 0.22, 0.17), (0.16, 0.17, 0.3), (0.45, 0.32, 0.16), (0.3, 0.12, 0.22), (0.2, 0.2, 0.2), (0.55, 0.45, 0.3)]
    w = n // 8
    rng = _rng(k)
    for i, c in enumerate(cols):
        col = np.ones((n, w, 3)) * np.asarray(c)
        col *= (0.85 + 0.25 * noise(n, w, 32, 4, k + i))[..., None]
        for band in (0.12, 0.16, 0.84, 0.88):
            yb = int(band * n)
            col[yb:yb + max(2, n // 128)] = (0.75, 0.6, 0.28)
        ty = int((0.3 + 0.1 * rng.random()) * n)
        col[ty:ty + n // 8, w // 5:w - w // 5] = col[ty:ty + n // 8, w // 5:w - w // 5] * 0.6 + np.array([0.7, 0.58, 0.3]) * 0.4
        rgb[:, i * w:(i + 1) * w] = col
    pages = np.ones((n, w, 3)) * np.array([0.82, 0.76, 0.62])
    pages *= (0.92 + 0.1 * noise(n, w, 128, 2, k + 9, smooth=0))[..., None]
    rgb[:, 7 * w:] = pages
    return posterize(rgb, 22, 0.25)


def leaf(n, k):
    """常春藤葉卡（RGB＋alpha 剪影）：一張 4×4 的葉子散佈。"""
    rgb = np.zeros((n, n, 3))
    alpha = np.zeros((n, n))
    rng = _rng(k)
    y, x = np.mgrid[0:n, 0:n] / n
    for i in range(22):
        px, py = rng.random(), rng.random()
        s = 0.07 + 0.05 * rng.random()
        ang = rng.random() * math.pi * 2
        dx, dy = x - px, y - py
        u = dx * math.cos(ang) + dy * math.sin(ang)
        v = -dx * math.sin(ang) + dy * math.cos(ang)
        shape = (np.abs(u) / s) ** 1.6 + (np.abs(v) / (s * 0.7)) ** 1.6 < 1
        tone = 0.6 + 0.4 * rng.random()
        rgb[shape] = np.array([0.16, 0.32, 0.12]) * tone + np.array([0.05, 0.08, 0.02]) * (u[shape] / s)[:, None]
        alpha[shape] = 1
    return rgb, alpha


def painting(w, h, k):
    """壁爐上方的風景畫：灰藍天空、遠山、河流與一座塔的剪影（不是任何真實畫作）。"""
    y, x = np.mgrid[0:h, 0:w]
    u, v = x / w, y / h
    sky = ramp(v, [(0, (0.45, 0.5, 0.6)), (0.55, (0.66, 0.66, 0.62)), (1, (0.66, 0.66, 0.62))])
    rgb = sky.copy()
    hill = 0.55 + 0.06 * np.sin(u * 7 + 1) + 0.03 * np.sin(u * 19)
    rgb[v > hill] = np.array([0.24, 0.27, 0.24])
    river = (v > 0.72) & (np.abs(v - 0.8 - 0.05 * np.sin(u * 5)) < 0.04)
    rgb[river] = np.array([0.5, 0.56, 0.6])
    tower = (np.abs(u - 0.7) < 0.03) & (v > 0.35) & (v < hill + 0.01)
    rgb[tower] = np.array([0.2, 0.2, 0.24])
    rgb *= (0.85 + 0.25 * fbm(h, w, k))[..., None]
    rgb = blur(rgb.reshape(h, w * 3), 0).reshape(h, w, 3)
    return posterize(rgb, 14, 0.5)


def family_photo(w, h, k):
    """合照的照片面（兩人並肩站在海邊；臉部只是概括的色塊，不是實拍人像）。"""
    y, x = np.mgrid[0:h, 0:w]
    u, v = x / w, y / h
    rgb = ramp(v, [(0, (0.58, 0.68, 0.76)), (0.55, (0.72, 0.76, 0.76)), (0.6, (0.42, 0.5, 0.55)), (0.72, (0.74, 0.66, 0.5)), (1, (0.7, 0.62, 0.48))])
    rgb *= (0.9 + 0.15 * fbm(h, w, k))[..., None]
    return posterize(rgb, 16, 0.4)


def figure(n, k):
    """合照裡的人影（白色剪影＋alpha；頭、肩、軀幹，邊緣柔和，Unity 端依材質著色）。"""
    y, x = np.mgrid[0:n, 0:n] / n
    head = np.hypot((x - 0.5) / 0.16, (y - 0.82) / 0.13) < 1
    body = (np.abs(x - 0.5) < 0.3 - 0.12 * np.clip((y - 0.3) / 0.4, 0, 1)) & (y < 0.66) & (y > 0.0)
    shoulders = np.hypot((x - 0.5) / 0.32, (y - 0.6) / 0.1) < 1
    a = (head | body | shoulders).astype(float)
    a = blur(a, 2)
    rgb = np.ones((n, n, 3)) * (0.9 + 0.1 * fbm(n, n, k))[..., None]
    # 陣列第 0 列＝影像上緣（save 會上下翻轉成 Blender 的左下原點）：頭要在上方，所以翻轉。
    return rgb[::-1], np.clip(a * 1.4, 0, 1)[::-1]


def build_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    S = lambda name, rgb, a=None: save(rgb, os.path.join(out_dir, name + ".png"), a)
    S("LR_WoodMid", wood(512, 1, (0.26, 0.15, 0.08), (0.5, 0.32, 0.17)))
    S("LR_WoodDark", wood(512, 2, (0.11, 0.065, 0.045), (0.28, 0.17, 0.1)))
    S("LR_Floor", floorboards(1024, 3))
    S("LR_SofaFabric", fabric(512, 4, (0.36, 0.38, 0.27), (0.17, 0.19, 0.13)))
    S("LR_CushionCream", fabric(256, 5, (0.8, 0.74, 0.62), (0.55, 0.5, 0.42), 64, 0.2))
    S("LR_CushionRed", fabric(256, 6, (0.5, 0.13, 0.15), (0.26, 0.06, 0.08), 64, 0.2))
    S("LR_Plaid", plaid(512, 7))
    S("LR_Curtain", damask(512, 8, (0.26, 0.08, 0.08), (0.46, 0.24, 0.16), 0.5))
    S("LR_Wallpaper", damask(512, 9, (0.52, 0.47, 0.4), (0.62, 0.55, 0.45), 0.4))
    S("LR_Ceiling", plaster(256, 10, (0.72, 0.68, 0.6)))
    S("LR_Paint", plaster(256, 11, (0.78, 0.74, 0.65)))
    S("LR_Rug", rug(1024, 12))
    S("LR_Brass", brass(256, 13))
    S("LR_Ceramic", plaster(256, 14, (0.68, 0.62, 0.5)))
    S("LR_Shade", fabric(256, 15, (0.92, 0.82, 0.62), (0.72, 0.62, 0.45), 80, 0.15))
    S("LR_Iron", iron(256, 16))
    S("LR_HearthTile", hearth_tiles(256, 17))
    S("LR_Books", books_atlas(512, 18))
    S("LR_Linen", fabric(256, 19, (0.84, 0.8, 0.72), (0.62, 0.58, 0.5), 90, 0.15))
    rgb, a = leaf(256, 20)
    S("LR_Leaf", rgb, a)
    S("LR_Painting", painting(512, 384, 21))
    S("LR_FamilyPhoto", family_photo(256, 192, 22))
    rgb, a = figure(128, 23)
    S("LR_Figure", rgb, a)
