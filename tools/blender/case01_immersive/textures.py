"""Case01 沉浸式住宅 QA 的貼圖（numpy 生成，在 Blender 內存 PNG）。沿用 case01_living_room/textures.py 的雜訊與存檔工具。"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
import importlib.util  # noqa: E402

_spec = importlib.util.spec_from_file_location("lr_textures", os.path.join(HERE, "..", "case01_living_room", "textures.py"))
lt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lt)
fbm, noise, blur, ramp, save, fabric = lt.fbm, lt.noise, lt.blur, lt.ramp, lt.save, lt.fabric


def paper(n, k, base=(0.9, 0.87, 0.8)):
    f = fbm(n, n, k)
    fib = blur(lt._rng(k).random((n, n)), 1)
    rgb = np.ones((n, n, 3)) * np.asarray(base)
    rgb *= (0.94 + 0.06 * f)[..., None]
    rgb *= (0.97 + 0.05 * fib)[..., None]
    return rgb


def kraft(n, k):
    return paper(n, k, (0.62, 0.48, 0.32)) * (0.95 + 0.08 * noise(n, n, 64, 64, k + 3))[..., None]


def rubber(n, k):
    f = fbm(n, n, k)
    return np.ones((n, n, 3)) * 0.07 + (f * 0.025)[..., None]


def suede(n, k):
    f = fbm(n, n, k, ((8, 8, 0.4), (32, 32, 0.35), (128, 128, 0.25)))
    return ramp(f, [(0, (0.3, 0.2, 0.13)), (0.6, (0.42, 0.29, 0.19)), (1, (0.5, 0.36, 0.24))])


def umbrella(n, k):
    base = fabric(n, k, (0.08, 0.11, 0.18), (0.05, 0.07, 0.12), weave=128)
    return base


def newsprint(n, k):
    rgb = paper(n, k, (0.82, 0.8, 0.74))
    # 欄位與「字行」：灰色細條（不寫真實文字、不放商標）。
    for c in range(4):
        x0 = int(n * (0.05 + c * 0.235))
        x1 = x0 + int(n * 0.2)
        for r in range(0, int(n * 0.88), 9):
            y = int(n * 0.1) + r
            if (r // 9) % 11 == 0:
                continue
            w = x1 - int((r * 37 % 17) * n / 1200)
            rgb[y:y + 3, x0:w] *= 0.62
    rgb[int(n * 0.03):int(n * 0.075), int(n * 0.05):int(n * 0.95)] *= 0.35
    return rgb


def runner(w, h, k):
    """樓梯長毯：深紅底、兩側滾邊、中間重複的菱形花紋（舊、磨損）。"""
    y, x = np.mgrid[0:h, 0:w] / np.array([h, w])[:, None, None]
    base = np.ones((h, w, 3)) * np.asarray((0.36, 0.09, 0.08))
    edge = (x < 0.08) | (x > 0.92)
    base[edge] = (0.12, 0.08, 0.06)
    stripe = ((x > 0.1) & (x < 0.12)) | ((x > 0.88) & (x < 0.9))
    base[stripe] = (0.62, 0.5, 0.28)
    u = (x - 0.5) * 4.0
    v = (y * 4.0) % 1.0 - 0.5
    d = np.abs(u) * 0.5 + np.abs(v)
    motif = (d > 0.28) & (d < 0.34) & ~edge
    base[motif] = (0.6, 0.48, 0.26)
    inner = (d < 0.12) & ~edge
    base[inner] = (0.16, 0.18, 0.24)
    wear = fbm(h, w, k)
    worn = np.clip((np.abs(x - 0.5) < 0.25) * (wear - 0.35) * 1.6, 0, 1)
    base = base * (1 - 0.35 * worn[..., None]) + 0.35 * worn[..., None] * np.asarray((0.42, 0.3, 0.24))
    base *= (0.9 + 0.1 * fabric(w, k + 1, (1, 1, 1), (0.8, 0.8, 0.8), weave=160)[..., 0:1]) if w == h else 1.0
    return base


def cookie(n):
    """手電筒光斑：中心較亮、外圈柔邊，帶一圈反光碗造成的淡環（避免完美圓盤的假感）。"""
    y, x = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1
    r = np.sqrt(x * x + y * y)
    core = np.clip(1 - r / 0.97, 0, 1) ** 0.65
    ring = np.exp(-((r - 0.62) / 0.09) ** 2) * 0.07
    hot = np.exp(-(r / 0.25) ** 2) * 0.25
    a = np.clip(core * 0.82 + ring + hot, 0, 1)
    a[r > 0.98] = 0
    return np.repeat(a[..., None], 3, axis=2)


def photo_back(w, h, k):
    """相紙背面：米白紙、細纖維、極淡的斜向印刷紋（不使用任何品牌字樣）。"""
    rgb = paper(max(w, h), k, (0.93, 0.92, 0.88))[:h, :w]
    y, x = np.mgrid[0:h, 0:w]
    lines = ((x + y) % 64 < 2).astype(float) * 0.025
    rgb -= lines[..., None]
    return rgb


def build_all(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    jobs = {
        "IH_Paper": lambda: paper(512, 1), "IH_Kraft": lambda: kraft(512, 2), "IH_Rubber": lambda: rubber(256, 3), "IH_Suede": lambda: suede(512, 4),
        "IH_Umbrella": lambda: umbrella(512, 5), "IH_Newsprint": lambda: newsprint(512, 6), "IH_Runner": lambda: runner(512, 512, 7),
        "IH_PhotoBack": lambda: photo_back(1024, 768, 8),
    }
    for name, fn in jobs.items():
        save(fn(), os.path.join(out_dir, name + ".png"))
        print("[Case01Immersive] texture", name)
    c = cookie(256)
    save(c, os.path.join(out_dir, "IH_FlashlightCookie.png"), alpha=c[..., 0])
