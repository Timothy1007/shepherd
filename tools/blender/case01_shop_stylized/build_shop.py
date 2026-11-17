# 《牧羊人》Case01 改衣店：正式美術重做（和美術風格 QA 的縫紉機桌同一套風格，2026-11-17）。
# 在 Blender 裡執行：
#   OUT_UNITY = r"...\unity\Assets\Exploration\StyleShop"
#   ONLY = ["v3_shop_counter", ...]   # 可選：只做這幾件
#   exec(open(r"...\tools\blender\case01_shop_stylized\build_shop.py", encoding="utf-8").read())
# 規則：
#   ・沿用 tools/blender/stylized_qa/build.py 的建模與「手繪底色烘焙」工具（bmesh 立體、平面著色、Emission 烘焙）。
#   ・每一件獨立的東西都是自己的物件、自己的 FBX（使用者：不要和其他東西綁在一起，之後改位置比較好弄）。
#   ・尺寸、樞紐點（底部中央）和朝向都和原本的 v2_ 模型一樣：正面朝 Blender -Y（Unity +Z），牆上的東西背面貼 Y=0。
#     Unity 的 X 是 Blender 的 -X（左右鏡射），所以「Unity +X 那一側」寫成 Blender 的 -X。
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix

_HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else None
try:
    SQA_BUILD
except NameError:
    SQA_BUILD = os.path.join(os.path.dirname(_HERE) if _HERE else ".", "stylized_qa", "build.py")
try:
    OUT_UNITY
except NameError:
    OUT_UNITY = os.path.join(".", "StyleShop")
try:
    ONLY
except NameError:
    ONLY = None

# 借用美術風格 QA 的工具（不執行它的 run）
# 2026-11-17 使用者：模型有點粗糙、邊緣不要白線 → 深色同色系的線、兩段倒角（轉角比較圓）、圓的東西邊數 ×1.6。
EDGE_STYLE = globals().get("EDGE_STYLE", "dark")
BEVEL_SEGS = globals().get("BEVEL_SEGS", 2)   # 兩段倒角有時烘焙很慢（燙衣板 38 分鐘），小東西可以設成 1
SEG_MUL = globals().get("SEG_MUL", 1.6)
SQA_LIBRARY_ONLY = True
_keep_out = OUT_UNITY
exec(open(SQA_BUILD, encoding="utf-8").read())
OUT_UNITY = _keep_out
MODELS = os.path.join(OUT_UNITY, "Models")
TEXTURES = os.path.join(OUT_UNITY, "Textures")
os.makedirs(MODELS, exist_ok=True)
os.makedirs(TEXTURES, exist_ok=True)
rng = random.Random(23)

# 改衣店用到的額外顏色（固有色, 凹處暗色, 筆觸尺度, 筆觸強度, 邊緣提亮）
PALETTE.update({
    "WoodDark":     ("#5B3A26", "#26170F", 7.0, 0.07, 0.28),
    "WoodTop":      ("#9A643C", "#47291D", 6.0, 0.06, 0.32),
    "WoodWarm":     ("#A0683F", "#4C2C1E", 8.0, 0.07, 0.30),
    "Paint":        ("#7E8B7A", "#3C4440", 6.0, 0.06, 0.26),
    "Paper":        ("#E9DFC8", "#9C907A", 9.0, 0.05, 0.25),
    "Label":        ("#D8C9A2", "#8A7B5E", 12.0, 0.05, 0.25),
    "Felt":         ("#3E5E4A", "#1D2C24", 8.0, 0.07, 0.18),
    "Leather":      ("#7A3B2A", "#341912", 8.0, 0.07, 0.30),
    "Iron":         ("#6E7176", "#2E3034", 12.0, 0.06, 0.55),
    "Mirror":       ("#B9C6C8", "#6E7C80", 2.0, 0.03, 0.15),
    "Rubber":       ("#2A2826", "#100F0E", 8.0, 0.05, 0.20),
    "ClothSlate":   ("#5F6C79", "#2B3039", 6.0, 0.08, 0.25),
    "ClothMustard": ("#C29245", "#5E431F", 6.0, 0.08, 0.25),
    "ClothOlive":   ("#6E7346", "#33361F", 6.0, 0.08, 0.25),
    "ClothNavy":    ("#33415E", "#161C2B", 6.0, 0.08, 0.25),
    "ClothBrick":   ("#8F4B3A", "#43211B", 6.0, 0.08, 0.25),
    "ClothSage":    ("#8FA08A", "#46513F", 6.0, 0.07, 0.25),
    "Curtain":      ("#9A4A3F", "#46201C", 5.0, 0.08, 0.22),
})


def J(a=0.6):
    return rng.uniform(-a, a)


# ================================================================== 第一批：大家具

def build_counter(col):
    """櫃台本體（不含上面的東西）：長 2.68、桌面頂 0.95、深 0.686。客人那一面（-Y）三片鑲板＋柱子＋黃銅踢腳板；店主那一面（+Y）一排抽屜＋開放層板。"""
    A = Asset("v3_shop_counter")
    W, D = 2.68, 0.68
    A.cube((W + 0.02, D + 0.02, 0.05), (0, 0, 0.925), "WoodTop", bevel=0.014)              # 桌面
    A.cube((W - 0.02, D - 0.06, 0.035), (0, 0, 0.885), "WoodDark", bevel=0.006)            # 桌面下的線板
    A.cube((W - 0.06, D - 0.1, 0.1), (0, 0, 0.05), "WoodDark", bevel=0.01, taper=0.99)     # 底座
    A.cube((W - 0.1, D - 0.14, 0.77), (0, 0, 0.485), "Wood", bevel=0.006)                  # 箱體
    fy = -(D - 0.14) / 2
    # 客人那一面：三片鑲板（框＋凸起的板）與四根柱子
    pw = (W - 0.1) / 3
    for i in range(3):
        cx = -(W - 0.1) / 2 + pw * (i + 0.5)
        A.cube((pw - 0.09, 0.025, 0.62), (cx, fy - 0.012, 0.47), "WoodWarm", bevel=0.01)
        A.cube((pw - 0.19, 0.02, 0.5), (cx, fy - 0.028, 0.47), "Wood", bevel=0.012, taper=0.97)
    for i in range(4):
        x = -(W - 0.1) / 2 + pw * i
        A.cube((0.07, 0.05, 0.8), (x, fy - 0.022, 0.48), "WoodDark", bevel=0.008)
        A.cube((0.09, 0.06, 0.035), (x, fy - 0.025, 0.86), "WoodTop", bevel=0.006)
    A.cube((W - 0.12, 0.012, 0.07), (0, fy - 0.05, 0.055), "Brass", bevel=0.004)            # 踢腳板
    # 店主那一面：三個抽屜（黃銅把手）＋下面的開放層板
    by = (D - 0.14) / 2
    for i in range(3):
        cx = -(W - 0.1) / 2 + pw * (i + 0.5)
        A.cube((pw - 0.06, 0.03, 0.16), (cx, by + 0.012, 0.77), "WoodWarm", rot=(0, 0, J(0.3)), bevel=0.008)
        A.cyl(0.018, 0.03, (cx, by + 0.035, 0.77), "Brass", rot=(90, 0, 0), segs=10, r2=0.012)
    A.cube((W - 0.16, 0.02, 0.02), (0, by + 0.005, 0.66), "WoodDark", bevel=0.004)
    A.cube((W - 0.2, D - 0.24, 0.025), (0, 0.02, 0.32), "WoodDark", bevel=0.004)            # 層板
    return A.build(col)


def build_button_cabinet(col):
    """鈕扣抽屜櫃：0.84 寬、1.03 高、0.39 深；5 欄 × 6 列小抽屜，每個抽屜有黃銅拉環與紙標籤。"""
    A = Asset("v3_button_cabinet")
    W, H, D = 0.82, 0.98, 0.37
    A.cube((W, D, H), (0, 0, H / 2 + 0.02), "WoodDark", bevel=0.008)
    A.cube((W + 0.02, D + 0.02, 0.035), (0, 0, H + 0.035), "WoodTop", bevel=0.01)          # 頂板
    A.cube((W + 0.01, D + 0.01, 0.04), (0, 0, 0.02), "WoodDark", bevel=0.008, taper=0.97)  # 底座
    cols, rows = 5, 6
    dw, dh = (W - 0.06) / cols, (H - 0.08) / rows
    fy = -D / 2
    for r in range(rows):
        for c in range(cols):
            x = -(W - 0.06) / 2 + dw * (c + 0.5)
            z = 0.06 + dh * (r + 0.5)
            A.cube((dw - 0.012, 0.02, dh - 0.012), (x, fy - 0.006, z), "WoodWarm", rot=(0, 0, J(0.6)), bevel=0.004)
            A.cube((dw * 0.45, 0.004, dh * 0.22), (x, fy - 0.018, z + dh * 0.18), "Label", bevel=0.0)
            A.cyl(0.009, 0.012, (x, fy - 0.022, z - dh * 0.12), "Brass", rot=(90, 0, 0), segs=8)
    return A.build(col)


def build_clothes_rack(col):
    """移動式吊衣架（只有架子）：寬 1.44、高 1.63、深 0.55；兩端 H 形底座＋輪子、兩根立柱、上面橫桿。"""
    A = Asset("v3_clothes_rack")
    W, H, D = 1.40, 1.60, 0.52
    for sx in (-1, 1):
        x = sx * W / 2
        A.cube((0.05, D, 0.04), (x, 0, 0.09), "Iron", bevel=0.008)                          # 底座橫條
        A.cyl(0.016, H - 0.11, (x, 0, 0.11 + (H - 0.11) / 2), "Iron", segs=10)              # 立柱
        for sy in (-1, 1):
            A.cyl(0.035, 0.03, (x, sy * (D / 2 - 0.03), 0.04), "Rubber", rot=(0, 90, 0), segs=10)   # 輪子
            A.cube((0.03, 0.03, 0.04), (x, sy * (D / 2 - 0.03), 0.075), "Iron", bevel=0.004)
    A.cyl(0.018, W + 0.06, (0, 0, H), "Steel", rot=(0, 90, 0), segs=10)                      # 上橫桿
    for sx in (-1, 1):
        A.lathe([(0.0, -0.02), (0.026, -0.015), (0.026, 0.012), (0.0, 0.02)], (sx * (W / 2 + 0.035), 0, H), "Iron", rot=(0, 90, 0), segs=10)
    A.cyl(0.012, W, (0, 0, 0.14), "Iron", rot=(0, 90, 0), segs=8)                             # 下橫撐
    return A.build(col)


def garment(A, x, mat, kind, seed, turn=0.0):
    """掛在橫桿上的一件衣服（衣架＋衣身＋袖子）。衣身沿 Y 方向攤開（正面朝 -Y 不重要，衣服是側掛）。"""
    r = random.Random(seed)
    top = 1.6
    # 衣架：鉤子＋三角
    A.lathe([(0.0, 0.0), (0.004, 0.0), (0.004, 0.05), (0.0, 0.055)], (x, 0, top - 0.04), "Steel", segs=6)
    A.cube((0.012, 0.42, 0.02), (x, 0, top - 0.07), "WoodPale", rot=(0, 0, 0), bevel=0.004, taper=1.0)
    L = 1.0 if kind == "coat" else (0.72 if kind == "shirt" else 0.9)
    w0, w1 = 0.42, (0.46 if kind == "coat" else 0.4)
    # 衣身：上窄下寬的厚片，微微擺動
    rings = []
    n = 5
    for i in range(n):
        f = i / (n - 1)
        z = top - 0.08 - L * f
        rings.append(((x + r.uniform(-0.01, 0.01), r.uniform(-0.01, 0.01), z), (0, 0, -1), (1, 0, 0), w0 + (w1 - w0) * f, 0.05 + 0.02 * f))
    A.loft(rings, mat, n=8, expo=5.0)
    # 領子
    A.cube((0.05, 0.16, 0.05), (x - 0.0, 0, top - 0.1), mat, bevel=0.01)
    # 兩隻袖子（垂在兩側）
    for sy in (-1, 1):
        A.loft([((x, sy * 0.2, top - 0.1), (0, sy * 0.3, -1), (1, 0, 0), 0.06, 0.09),
                ((x + r.uniform(-0.01, 0.01), sy * 0.235, top - 0.1 - L * 0.62), (0, sy * 0.05, -1), (1, 0, 0), 0.055, 0.075)], mat, n=8, expo=3.0)


def build_rack_clothes(col):
    """吊衣架上的衣服（獨立物件，和架子同一個樞紐點）：六件，顏色各異。"""
    A = Asset("v3_rack_clothes")
    specs = [("ClothNavy", "coat"), ("ClothCream", "shirt"), ("ClothBrick", "dress"), ("ClothCharcoal", "coat"), ("ClothSage", "shirt"), ("ClothMustard", "dress")]
    for i, (mat, kind) in enumerate(specs):
        x = -0.55 + i * 0.22 + J(0.02)
        before = set(A.bm.verts)
        garment(A, 0.0, mat, kind, 100 + i)
        new = [v for v in A.bm.verts if v not in before]
        # 每件衣服繞著衣架鉤轉 30～40 度（左右交錯），從正面看得到衣服的正面，不是一條邊。
        ang = math.radians((35 if i % 2 == 0 else -32) + J(5))
        M = Matrix.Translation(Vector((x, 0, 0))) @ Matrix.Rotation(ang, 4, "Z")
        bmesh.ops.transform(A.bm, matrix=M, verts=new)
    return A.build(col)


def build_ironing_board(col):
    """燙衣板（不含熨斗）：板長 1.2＋尖端、寬 0.36、板面高 0.88；X 形腳。熨斗那一端在 Unity -X（Blender +X）。"""
    A = Asset("v3_ironing_board")
    # 板面：方形段＋尖端（用 loft 做漸窄）
    rings = []
    for i in range(7):
        f = i / 6
        x = 0.42 - f * 1.3            # Blender +X＝熨斗那一端（Unity -X）
        w = 0.36 if f < 0.62 else 0.36 * (1 - (f - 0.62) / 0.38 * 0.72)
        rings.append(((x, 0, 0.88), (-1, 0, 0), (0, 0, 1), w, 0.03))
    A.loft(rings, "ClothSage", n=10, expo=6.0)
    A.loft([((r[0][0], 0, 0.862), r[1], r[2], r[3] + 0.012, 0.012) for r in rings], "Iron", n=10, expo=6.0)  # 金屬邊
    # 熨斗架（熨斗那一端）
    A.cube((0.14, 0.26, 0.008), (0.5, 0, 0.87), "Iron", bevel=0.003)
    # X 形腳
    for (x0, x1) in ((0.25, -0.55), (-0.55, 0.25)):
        for sy in (-1, 1):
            a, b = Vector((x0, sy * 0.13, 0.02)), Vector((x1, sy * 0.13, 0.85))
            t = b - a
            A.loft([(a, t, (0, 1, 0), 0.025, 0.018), (b, t, (0, 1, 0), 0.025, 0.018)], "Iron", n=8, expo=3.0, cap=True)
        A.cyl(0.03, 0.3, (x0, 0, 0.02), "Rubber", rot=(90, 0, 0), segs=8)
    return A.build(col)


def build_iron(col):
    """熨斗（獨立物件）：底板＋機身＋把手。樞紐點在底板中央。"""
    A = Asset("v3_iron")
    rings = []
    for i in range(5):
        f = i / 4
        x = 0.11 - f * 0.22
        w = 0.11 * (1 - max(0, f - 0.55) / 0.45 * 0.75) if f > 0.55 else 0.11
        rings.append(((x, 0, 0.012), (-1, 0, 0), (0, 0, 1), w, 0.02))
    A.loft(rings, "Steel", n=10, expo=5.0)
    A.loft([((r[0][0] * 0.92, 0, 0.045), r[1], r[2], r[3] * 0.85, 0.05) for r in rings], "Lacquer", n=10, expo=3.0)
    A.loft([((0.07, 0, 0.07), (-1, 0, 0.6), (0, 0, 1), 0.03, 0.03), ((0.0, 0, 0.115), (-1, 0, 0), (0, 0, 1), 0.03, 0.028),
            ((-0.06, 0, 0.09), (-1, 0, -0.6), (0, 0, 1), 0.03, 0.03)], "WoodDark", n=8, expo=3.0)
    A.cyl(0.012, 0.01, (0.0, -0.045, 0.06), "Brass", rot=(90, 0, 0), segs=8)                # 溫度旋鈕
    return A.build(col)


def build_fabric_shelf(col):
    """布料架（只有架子）：寬 1.88、高 2.51、深 0.46；背板、兩側板、五層層板、頂上的線板。"""
    A = Asset("v3_fabric_shelf")
    W, H, D = 1.84, 2.42, 0.44
    A.cube((W, 0.02, H - 0.1), (0, D / 2 - 0.02, (H - 0.1) / 2 + 0.06), "WoodDark", bevel=0.0)   # 背板（+Y＝靠牆）
    for sx in (-1, 1):
        A.cube((0.04, D, H), (sx * (W / 2 - 0.02), 0, H / 2), "Wood", bevel=0.008)
    for k in range(6):
        z = 0.08 + k * 0.45
        A.cube((W - 0.06, D - 0.03, 0.03), (0, 0.005, z), "WoodTop", rot=(0, J(0.15), 0), bevel=0.006)
    A.cube((W + 0.06, D + 0.05, 0.06), (0, 0, H + 0.03), "WoodDark", bevel=0.012)              # 頂線板
    A.cube((W + 0.02, D + 0.02, 0.08), (0, 0, 0.04), "WoodDark", bevel=0.008, taper=0.98)
    return A.build(col)


def build_shelf_fabrics(col):
    """布料架上的布（獨立物件，和架子同一個樞紐點）：每層幾疊摺好的布、幾捲布。"""
    A = Asset("v3_shelf_fabrics")
    mats = ["ClothNavy", "ClothCream", "ClothBrick", "ClothSage", "ClothMustard", "ClothSlate", "ClothRose", "ClothOlive", "ClothCharcoal"]
    for k in range(5):
        z = 0.095 + k * 0.45
        x = -0.82
        while x < 0.78:
            w = rng.uniform(0.26, 0.34)
            if x + w > 0.84:
                break
            if rng.random() < 0.2:      # 一捲布（橫放）
                A.cyl(0.07, w - 0.02, (x + w / 2, -0.02, z + 0.07), rng.choice(mats), rot=(0, 90, 0), segs=12, bevel=0.0)
            else:                        # 一疊摺好的布
                h = 0.0
                for j in range(rng.randint(2, 5)):
                    t = rng.uniform(0.035, 0.05)
                    A.cube((w - 0.03 - J(0.01), 0.32, t), (x + w / 2 + J(0.01), -0.03 + J(0.01), z + h + t / 2), rng.choice(mats),
                           rot=(0, 0, J(3)), bevel=0.01, taper=0.98)
                    h += t
            x += w + 0.02
    return A.build(col)


def build_mirror(col):
    """立鏡：寬 0.73、高 1.815、深 0.5；兩根柱子、腳、可以擺動的鏡框（微微後仰）。"""
    A = Asset("v3_cheval_mirror")
    for sx in (-1, 1):
        x = sx * 0.33
        A.cube((0.045, 0.045, 1.62), (x, 0, 0.81), "WoodDark", bevel=0.008, taper=0.85)
        A.cube((0.06, 0.5, 0.05), (x, 0, 0.025), "WoodDark", bevel=0.01, taper=0.9)                 # 腳
        A.lathe([(0.0, 0.0), (0.03, 0.0), (0.035, 0.03), (0.02, 0.06), (0.0, 0.08)], (x, 0, 1.62), "WoodTop", segs=10)  # 頂飾
        A.cyl(0.022, 0.02, (x - sx * 0.025, 0, 0.95), "Brass", rot=(0, 90, 0), segs=10)               # 轉軸
    # 鏡框（後仰 6°）：外框＋鏡面
    A.cube((0.58, 0.05, 1.41), (0, 0, 0.95), "WoodWarm", rot=(-6, 0, 0), bevel=0.014)
    A.cube((0.5, 0.012, 1.33), (0, -0.024, 0.95), "Mirror", rot=(-6, 0, 0), bevel=0.0)
    A.cube((0.6, 0.06, 0.04), (0, 0, 1.66), "WoodTop", rot=(-6, 0, 0), bevel=0.01)
    return A.build(col)


def build_cut_table(col):
    """裁剪桌（原本是灰盒）：桌面 1.6 × 0.85、高 0.91；下層放布捲的層板。原始尺寸，Unity 會再乘家具比例。"""
    A = Asset("v3_cut_table")
    W, D, H = 1.6, 0.85, 0.91
    A.cube((W, D, 0.05), (0, 0, H - 0.025), "WoodTop", bevel=0.012)
    A.cube((W - 0.02, D - 0.02, 0.006), (0, 0, H + 0.001), "ClothSage", bevel=0.0)          # 裁剪墊（綠色格子墊）
    for i in range(1, 8):
        A.cube((0.004, D - 0.06, 0.002), (-W / 2 + i * W / 8, 0, H + 0.005), "ClothCream", bevel=0.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            A.cube((0.06, 0.06, H - 0.05), (sx * (W / 2 - 0.06), sy * (D / 2 - 0.06), (H - 0.05) / 2), "Wood", bevel=0.006, taper=0.9)
    A.cube((W - 0.1, D - 0.1, 0.03), (0, 0, 0.18), "WoodDark", bevel=0.006)                 # 下層
    A.cube((W - 0.1, 0.03, 0.08), (0, -D / 2 + 0.06, H - 0.09), "Wood", bevel=0.004)
    A.cube((W - 0.1, 0.03, 0.08), (0, D / 2 - 0.06, H - 0.09), "Wood", bevel=0.004)
    return A.build(col)


def build_fabric_roll(col):
    """一捲布（獨立物件，放在裁剪桌下層）：長 0.9。"""
    A = Asset("v3_fabric_roll")
    A.cyl(0.065, 0.9, (0, 0, 0.065), "ClothBrick", rot=(0, 90, 0), segs=14)
    A.cyl(0.018, 0.94, (0, 0, 0.065), "WoodPale", rot=(0, 90, 0), segs=8)
    return A.build(col)


def build_fitting_booth(col):
    """試衣間（原本是灰盒）：原始尺寸寬 1.07（Unity X）、深 1.17（Unity Z）、高 2.05；
    前後兩片側板（木框＋布）、靠房間那一側（Unity -X＝Blender +X）兩根柱子＋簾桿＋拉了三分之二的布簾、裡面的掛鉤。"""
    A = Asset("v3_fitting_booth")
    W, D, H = 1.07, 1.17, 2.05
    for sy in (-1, 1):
        y = sy * (D / 2 - 0.02)
        A.cube((W, 0.04, 0.06), (0, y, H - 0.03), "WoodDark", bevel=0.008)
        A.cube((W, 0.04, 0.12), (0, y, 0.06), "WoodDark", bevel=0.008)
        A.cube((W - 0.06, 0.02, H - 0.2), (0, y, H / 2), "Paint", bevel=0.004)              # 側板
        A.cube((0.05, 0.05, H), (W / 2 - 0.025, y, H / 2), "WoodDark", bevel=0.008)          # 柱子（房間那一側）
    A.cyl(0.015, D, (W / 2 - 0.025, 0, H - 0.05), "Brass", rot=(90, 0, 0), segs=10)          # 簾桿
    # 布簾：從 -Y 拉到約三分之二，有波浪摺
    n = 12
    cw = D * 0.66
    pts = []
    for i in range(n + 1):
        f = i / n
        y = -D / 2 + 0.03 + cw * f
        x = W / 2 - 0.03 + math.sin(f * math.pi * 7) * 0.035
        pts.append((x, y))
    grid = []
    for z in (H - 0.07, H * 0.55, 0.1):
        grid.append([Vector((x + (0.01 if z < 1 else 0), y, z)) for (x, y) in pts])
    A.sheet(grid, "Curtain", thickness=0.012)
    for k in range(n // 2):
        y = -D / 2 + 0.03 + cw * (2 * k) / n
        A.lathe([(0.0, -0.015), (0.022, -0.012), (0.022, 0.012), (0.0, 0.015)], (W / 2 - 0.025, y, H - 0.05), "Brass", rot=(90, 0, 0), segs=8)
    A.cube((0.05, 0.25, 0.04), (-W / 2 + 0.06, 0, 1.6), "Brass", bevel=0.006)               # 掛鉤
    return A.build(col)


def build_booth_stool(col):
    """試衣間裡的小凳（獨立物件）：高 0.45。"""
    A = Asset("v3_booth_stool")
    A.cube((0.35, 0.35, 0.04), (0, 0, 0.43), "WoodWarm", bevel=0.01)
    for sx in (-1, 1):
        for sy in (-1, 1):
            A.cube((0.035, 0.035, 0.41), (sx * 0.14, sy * 0.14, 0.205), "Wood", rot=(sy * 3, sx * -3, 0), bevel=0.005)
    return A.build(col)


def build_customer_chair(col):
    """客人坐的椅子（換掉原本的 Poly Haven 椅子）：沿用美術風格 QA 那張椅子的造型。"""
    ob = build_chair(col)
    ob.name = "v3_customer_chair"
    ob.data.name = "v3_customer_chair"
    return ob


# ================================================================== 第二批：小東西（每一件都是自己的物件）

def build_cash_register(col):
    """收銀機：寬 0.44、高 0.42、深 0.34。黑漆底座＋錢箱抽屜、黃銅機身、三排按鍵（客人那一面 -Y）、頂上的數字窗、右側搖把（Unity -X＝Blender +X）。
    按鍵、搖把、數字牌刻意做大一點，第一人稱才認得出來。"""
    A = Asset("v3_cash_register")
    A.cube((0.44, 0.34, 0.1), (0, 0, 0.05), "Lacquer", bevel=0.01, taper=0.98)                  # 底座
    A.cube((0.36, 0.02, 0.06), (0, -0.172, 0.05), "WoodWarm", bevel=0.006)                      # 錢箱抽屜
    A.cyl(0.014, 0.02, (0, -0.19, 0.05), "Brass", rot=(90, 0, 0), segs=10)
    A.cube((0.38, 0.27, 0.2), (0, 0.02, 0.2), "Brass", bevel=0.012, taper=0.9, shear=(0, 0.03))  # 機身（往後收）
    for r in range(3):
        for c in range(5):
            x = -0.13 + c * 0.065
            y = -0.1 + r * 0.035
            z = 0.13 + r * 0.04
            A.cyl(0.017, 0.03, (x, y, z), "Paper", rot=(-35, 0, 0), segs=10)
            A.cyl(0.004, 0.03, (x, y + 0.012, z - 0.015), "Steel", rot=(-35, 0, 0), segs=6)
    A.cube((0.3, 0.12, 0.12), (0, 0.06, 0.36), "Brass", bevel=0.012)                             # 頂上的數字窗
    for k in range(4):
        A.cube((0.05, 0.01, 0.06), (-0.09 + k * 0.06, -0.002, 0.37), "Paper", bevel=0.004)        # 數字牌
    A.lathe([(0.0, 0.0), (0.03, 0.0), (0.03, 0.02), (0.0, 0.025)], (0.215, 0.0, 0.2), "Brass", rot=(0, 90, 0), segs=10)
    A.cube((0.012, 0.012, 0.11), (0.235, 0.0, 0.15), "Steel", bevel=0.003)                       # 搖把
    A.lathe([(0.0, -0.025), (0.015, -0.02), (0.015, 0.02), (0.0, 0.025)], (0.235, -0.025, 0.1), "WoodDark", rot=(90, 0, 0), segs=8)
    return A.build(col)


def build_button_display(col):
    """鈕扣展示盒：0.8 × 0.56、高 0.11；綠色絨布、黃銅框、上面一層玻璃，裡面散著鈕扣。"""
    A = Asset("v3_button_display")
    A.cube((0.8, 0.56, 0.03), (0, 0, 0.015), "WoodDark", bevel=0.006)
    A.cube((0.76, 0.52, 0.01), (0, 0, 0.034), "Felt", bevel=0.0)
    for (sx, sy, l, a) in [(0, -0.28, 0.82, 0), (0, 0.28, 0.82, 0), (-0.4, 0, 0.58, 90), (0.4, 0, 0.58, 90)]:
        A.cube((l, 0.02, 0.1), (sx, sy, 0.06), "Brass", rot=(0, 0, a), bevel=0.004)
    A.cube((0.78, 0.54, 0.006), (0, 0, 0.108), "Glass", bevel=0.0)
    mats = ["Brass", "Paper", "ClothSlate", "ClothBrick", "Lacquer", "Leather", "ClothCream"]
    for i in range(34):
        x, y = rng.uniform(-0.34, 0.34), rng.uniform(-0.22, 0.22)
        r = rng.uniform(0.008, 0.016)
        A.cyl(r, 0.005, (x, y, 0.042), rng.choice(mats), segs=8)
    return A.build(col)


def build_button_jar(col):
    """裝鈕扣的玻璃罐（一個；擺三個）：直徑 0.09、高 0.12；黃銅蓋。"""
    A = Asset("v3_button_jar")
    A.lathe([(0.0, 0.0), (0.042, 0.0), (0.045, 0.01), (0.045, 0.085), (0.038, 0.1), (0.038, 0.105), (0.0, 0.105)], (0, 0, 0), "Glass", segs=14)
    A.cyl(0.04, 0.02, (0, 0, 0.115), "Brass", segs=14, bevel=0.003)
    for i in range(14):
        a = rng.uniform(0, math.tau); rr = rng.uniform(0, 0.032)
        A.cyl(0.011, 0.006, (math.cos(a) * rr, math.sin(a) * rr, 0.012 + i * 0.004), rng.choice(["Paper", "ClothBrick", "ClothSlate", "Brass"]), rot=(J(30), J(30), 0), segs=8)
    return A.build(col)


def build_ledger(col):
    """皮面帳本（紅）：0.37 × 0.3、厚 0.04。"""
    A = Asset("v3_ledger")
    A.cube((0.37, 0.3, 0.012), (0, 0, 0.006), "Leather", bevel=0.004)
    A.cube((0.355, 0.285, 0.022), (0.005, 0, 0.023), "Paper", bevel=0.003)
    A.cube((0.37, 0.3, 0.012), (0, 0, 0.04), "Leather", bevel=0.004)
    A.cube((0.03, 0.3, 0.046), (-0.17, 0, 0.023), "Leather", bevel=0.008)       # 書背
    return A.build(col)


def build_receipt_book(col):
    """收據本（綠）：0.17 × 0.24、厚 0.03，比帳本小，顏色分得開。"""
    A = Asset("v3_receipt_book")
    A.cube((0.17, 0.24, 0.01), (0, 0, 0.005), "ClothOlive", bevel=0.003)
    A.cube((0.16, 0.23, 0.016), (0.003, 0, 0.018), "Paper", bevel=0.002)
    A.cube((0.012, 0.24, 0.028), (-0.08, 0, 0.014), "ClothOlive", bevel=0.004)
    return A.build(col)


def build_tape_measure(col):
    """軟尺：一捲黃色布尺＋拉出來的一段。"""
    A = Asset("v3_tape_measure")
    A.lathe([(0.02, 0.0), (0.065, 0.0), (0.065, 0.03), (0.02, 0.03)], (0, 0, 0), "ClothMustard", segs=16)
    A.cube((0.26, 0.022, 0.002), (0.19, -0.04, 0.001), "ClothMustard", rot=(0, 0, -12), bevel=0.0)
    return A.build(col)


def build_scissors(col):
    """裁縫剪刀：放大 1.4 倍（17 cm → 約 24 cm），黑色把手比較好認。"""
    A = Asset("v3_scissors")
    s = 1.4
    for k, a in ((0, 8), (1, -8)):
        A.cube((0.15 * s, 0.012 * s, 0.004), (0.06 * s, 0, 0.004 + k * 0.004), "Steel", rot=(0, 0, a), bevel=0.0, taper=0.4)
        A.lathe([(0.012 * s, 0.0), (0.022 * s, 0.0), (0.022 * s, 0.006), (0.012 * s, 0.006)], (-0.04 * s, (0.02 if k else -0.02) * s, 0.002 + k * 0.004), "Rubber", segs=12)
    A.cyl(0.006, 0.012, (0.0, 0.0, 0.006), "Brass", segs=8)
    return A.build(col)


def build_pencil(col):
    """鉛筆（放大 1.4 倍）＋一塊藍色劃粉。"""
    A = Asset("v3_pencil")
    A.cyl(0.006, 0.17 * 1.4, (0, 0, 0.006), "ClothMustard", rot=(0, 90, 0), segs=6)
    A.cyl(0.006, 0.02, (0.13, 0, 0.006), "WoodPale", rot=(0, 90, 0), segs=6, r2=0.001)
    return A.build(col)


def build_chalk(col):
    A = Asset("v3_chalk")
    A.cube((0.08, 0.08, 0.012), (0, 0, 0.006), "ClothSlate", rot=(0, 0, 45), bevel=0.003, taper=0.6)
    return A.build(col)


def build_teacup(col):
    """一杯茶：杯盤＋杯子＋把手＋茶。"""
    A = Asset("v3_teacup")
    A.lathe([(0.0, 0.0), (0.06, 0.0), (0.068, 0.008), (0.06, 0.01), (0.0, 0.01)], (0, 0, 0), "Paper", segs=16)
    A.lathe([(0.0, 0.01), (0.026, 0.01), (0.038, 0.035), (0.042, 0.07), (0.038, 0.07), (0.035, 0.04), (0.0, 0.04)], (0, 0, 0), "Paper", segs=16)
    A.cyl(0.036, 0.004, (0, 0, 0.06), "WoodDark", segs=16)
    A.lathe([(0.012, -0.004), (0.016, -0.004), (0.016, 0.004), (0.012, 0.004)], (0.05, 0, 0.045), "Paper", rot=(90, 0, 0), segs=10)
    return A.build(col)


def build_pin_cushion(col):
    """番茄針插＋幾根大頭針。"""
    A = Asset("v3_pin_cushion")
    A.lathe([(0.0, 0.0), (0.04, 0.005), (0.055, 0.03), (0.05, 0.055), (0.03, 0.068), (0.0, 0.07)], (0, 0, 0), "ClothBrick", segs=12)
    A.cyl(0.012, 0.012, (0, 0, 0.074), "ClothOlive", segs=6)
    for i in range(6):
        a = i * math.tau / 6 + 0.3
        p = (math.cos(a) * 0.03, math.sin(a) * 0.03, 0.06)
        A.cyl(0.0015, 0.05, p, "Steel", rot=(math.cos(a) * 25, math.sin(a) * 25, 0), segs=5)
        A.cyl(0.006, 0.008, (p[0] * 1.3, p[1] * 1.3, 0.085), rng.choice(["Brass", "ClothSlate", "Paper"]), segs=6)
    return A.build(col)


def build_counter_bell(col):
    """叫人鈴（放大 1.3 倍）。"""
    A = Asset("v3_counter_bell")
    s = 1.3
    A.cyl(0.05 * s, 0.012, (0, 0, 0.006), "WoodDark", segs=14, bevel=0.003)
    A.lathe([(0.0, 0.012), (0.045 * s, 0.014), (0.04 * s, 0.03), (0.025 * s, 0.05 * s), (0.0, 0.055 * s)], (0, 0, 0), "Brass", segs=16)
    A.cyl(0.008, 0.02, (0, 0, 0.055 * s + 0.01), "Brass", segs=8)
    return A.build(col)


def build_counter_lamp(col):
    """櫃台的小檯燈（換掉灰盒）：圓底座、彎曲的細桿、小燈罩；燈泡在原點上方約 0.18 m（和原本的燈光位置一樣）。"""
    A = Asset("v3_counter_lamp")
    A.lathe([(0.0, 0.0), (0.06, 0.0), (0.062, 0.012), (0.04, 0.025), (0.0, 0.028)], (0, 0, 0), "Brass", segs=16)
    A.loft([((0, 0, 0.025), (0, 0, 1), (1, 0, 0), 0.012, 0.012), ((0.0, 0.0, 0.12), (0, 0.2, 1), (1, 0, 0), 0.01, 0.01),
            ((0.0, -0.04, 0.2), (0, -1, 0.4), (1, 0, 0), 0.01, 0.01)], "Brass", n=8, expo=2.0)
    A.lathe([(0.012, 0.0), (0.03, -0.01), (0.06, -0.05), (0.058, -0.055), (0.025, -0.02), (0.01, -0.005)], (0.0, -0.06, 0.215), "Paint", segs=16)
    A.lathe([(0.0, -0.02), (0.018, -0.03), (0.016, -0.05), (0.0, -0.058)], (0.0, -0.06, 0.215), "Paper", segs=10)
    return A.build(col)


def build_spool(col):
    """一捲線（擺好幾個）。"""
    A = Asset("v3_spool")
    prof = [(0.0, 0.0), (0.024, 0.0), (0.024, 0.007), (0.016, 0.008), (0.019, 0.012), (0.019, 0.045), (0.016, 0.048), (0.024, 0.049), (0.024, 0.056), (0.0, 0.056)]
    A.lathe(prof, (0, 0, 0), "WoodPale", segs=12, mats=["WoodPale", "WoodPale", "WoodPale", "ThreadRed", "ThreadRed", "ThreadRed", "WoodPale", "WoodPale", "WoodPale"])
    return A.build(col)


def build_thread_rack(col):
    """牆上的線架：木板 0.62 × 0.78，掛在 1.06～1.84 m；6 × 6 根木釘，大部分插著線。背面貼牆（Y＝0），正面朝 -Y。"""
    A = Asset("v3_thread_rack")
    A.cube((0.62, 0.025, 0.78), (0, -0.0125, 1.45), "WoodWarm", bevel=0.008)
    A.cube((0.66, 0.03, 0.03), (0, -0.015, 1.06), "WoodDark", bevel=0.006)
    A.cube((0.66, 0.03, 0.03), (0, -0.015, 1.84), "WoodDark", bevel=0.006)
    threads = ["ThreadRed", "ThreadMustard", "ThreadSlate", "ClothCream", "ClothNavy", "ClothSage", "ClothBrick", "ClothOlive"]
    for r in range(6):
        for c in range(6):
            x = -0.25 + c * 0.1
            z = 1.14 + r * 0.12
            A.cyl(0.004, 0.06, (x, -0.055, z + 0.01), "WoodPale", rot=(80, 0, 0), segs=6)
            if rng.random() < 0.8:
                m = rng.choice(threads)
                A.lathe([(0.006, 0.0), (0.02, 0.0), (0.02, 0.005), (0.015, 0.006), (0.017, 0.009), (0.017, 0.04), (0.015, 0.043), (0.02, 0.044), (0.02, 0.05), (0.006, 0.05)],
                        (x, -0.035, z), "WoodPale", rot=(90, 0, 0), segs=10,
                        mats=["WoodPale", "WoodPale", "WoodPale", m, m, m, "WoodPale", "WoodPale", "WoodPale"])
    return A.build(col)


def build_wall_shelf(col):
    """牆上的層板（只有板子和托架）：寬 1.1、板面頂 1.43 m、深 0.26。背面貼牆（Y＝0）。"""
    A = Asset("v3_wall_shelf")
    A.cube((1.1, 0.26, 0.03), (0, -0.13, 1.415), "WoodTop", bevel=0.008)
    for sx in (-0.42, 0.42):
        A.loft([((sx, -0.015, 1.4), (0, 0, -1), (0, 1, 0), 0.025, 0.03), ((sx, -0.015, 1.3), (0, 0, -1), (0, 1, 0), 0.025, 0.03)], "Iron", n=6, expo=6)
        A.loft([((sx, -0.02, 1.32), (0, -1, 1.2), (1, 0, 0), 0.02, 0.02), ((sx, -0.2, 1.395), (0, -1, 0.2), (1, 0, 0), 0.02, 0.02)], "Iron", n=6, expo=4)
    return A.build(col)


def build_radio(col):
    """老收音機（獨立物件，放在牆上的層板）：0.26 × 0.12 × 0.17。"""
    A = Asset("v3_radio")
    A.cube((0.26, 0.12, 0.16), (0, 0, 0.08), "WoodWarm", bevel=0.02)
    A.cube((0.14, 0.006, 0.1), (-0.04, -0.061, 0.085), "ClothCream", bevel=0.004)          # 喇叭布
    for k in range(5):
        A.cube((0.13, 0.004, 0.006), (-0.04, -0.065, 0.045 + k * 0.02), "WoodDark", bevel=0.0)
    A.cyl(0.022, 0.012, (0.08, -0.062, 0.105), "Paper", rot=(90, 0, 0), segs=14)              # 刻度盤
    A.cyl(0.012, 0.02, (0.08, -0.065, 0.045), "Lacquer", rot=(90, 0, 0), segs=10)
    return A.build(col)


def build_folded_cloth(col):
    """層板上一疊摺好的布（獨立物件）。"""
    A = Asset("v3_folded_cloth")
    z = 0.0
    for m in ("ClothSage", "ClothCream", "ClothBrick"):
        t = 0.035
        A.cube((0.24, 0.2, t), (J(0.01), J(0.01), z + t / 2), m, rot=(0, 0, J(4)), bevel=0.01, taper=0.98)
        z += t
    return A.build(col)


def build_wall_clock(col):
    """掛鐘：直徑 0.32，中心在 2.0 m；背面貼牆（Y＝0）。"""
    A = Asset("v3_wall_clock")
    A.cyl(0.16, 0.04, (0, -0.02, 2.0), "WoodDark", rot=(90, 0, 0), segs=24, bevel=0.006)
    A.cyl(0.135, 0.008, (0, -0.042, 2.0), "Paper", rot=(90, 0, 0), segs=24)
    for k in range(12):
        a = k * math.tau / 12
        A.cube((0.008, 0.004, 0.022 if k % 3 else 0.035), (math.sin(a) * 0.115, -0.047, 2.0 + math.cos(a) * 0.115), "Lacquer", rot=(0, math.degrees(a), 0), bevel=0.0)
    A.cube((0.008, 0.004, 0.07), (0.02, -0.05, 2.03), "Lacquer", rot=(0, 30, 0), bevel=0.0)
    A.cube((0.006, 0.004, 0.1), (-0.035, -0.051, 2.04), "Lacquer", rot=(0, -40, 0), bevel=0.0)
    A.cyl(0.008, 0.01, (0, -0.052, 2.0), "Brass", rot=(90, 0, 0), segs=8)
    return A.build(col)


def build_fabric_bin(col):
    """放布捲的木箱（只有箱子）：0.7 × 0.39，前面低後面高。"""
    A = Asset("v3_fabric_bin")
    A.cube((0.7, 0.03, 0.3), (0, -0.18, 0.15), "WoodWarm", bevel=0.006)    # 前（-Y）
    A.cube((0.7, 0.03, 0.5), (0, 0.18, 0.25), "WoodWarm", bevel=0.006)     # 後
    for sx in (-1, 1):
        A.loft([((sx * 0.335, -0.195, 0.0), (0, 1, 0), (0, 0, 1), 0.03, 0.001), ((sx * 0.335, 0.195, 0.0), (0, 1, 0), (0, 0, 1), 0.03, 0.001)], "WoodWarm", n=4, expo=2, cap=True)
        A.cube((0.03, 0.39, 0.3), (sx * 0.335, 0, 0.15), "WoodWarm", bevel=0.006)
        A.cube((0.03, 0.2, 0.2), (sx * 0.335, 0.09, 0.4), "WoodWarm", bevel=0.006)
    A.cube((0.66, 0.36, 0.02), (0, 0, 0.03), "WoodDark", bevel=0.0)
    return A.build(col)


def build_fabric_bolts(col):
    """木箱裡站著的布捲（獨立物件）：8 捲、各種顏色、高度不一。"""
    A = Asset("v3_fabric_bolts")
    mats = ["ClothNavy", "ClothCream", "ClothBrick", "ClothSage", "ClothMustard", "ClothSlate", "ClothRose", "ClothOlive"]
    for i in range(8):
        x = -0.26 + (i % 4) * 0.17 + J(0.02)
        y = -0.08 + (i // 4) * 0.15 + J(0.02)
        h = rng.uniform(1.0, 1.35)
        A.cube((0.13, 0.06, h), (x, y, 0.04 + h / 2), mats[i], rot=(J(6), J(6), J(20)), bevel=0.02)
    return A.build(col)


def build_umbrella_stand(col):
    """傘架（只有桶子）：直徑 0.24、高 0.5。"""
    A = Asset("v3_umbrella_stand")
    A.lathe([(0.0, 0.0), (0.11, 0.0), (0.12, 0.03), (0.115, 0.46), (0.125, 0.5), (0.105, 0.5), (0.1, 0.05), (0.0, 0.05)], (0, 0, 0), "Paint", segs=16)
    A.cyl(0.118, 0.03, (0, 0, 0.25), "Brass", segs=16)
    return A.build(col)


def build_umbrellas(col):
    """兩把傘（獨立物件，插在傘架裡）。"""
    A = Asset("v3_umbrellas")
    for i, (x, m, tilt) in enumerate([(-0.03, "ClothNavy", 6), (0.04, "ClothBrick", -8)]):
        A.loft([((x, 0, 0.05), (0, 0, 1), (1, 0, 0), 0.02, 0.02), ((x, 0, 0.3), (0, 0, 1), (1, 0, 0), 0.07, 0.07),
                ((x + tilt * 0.002, 0, 0.75), (0, 0, 1), (1, 0, 0), 0.05, 0.05), ((x + tilt * 0.003, 0, 0.85), (0, 0, 1), (1, 0, 0), 0.02, 0.02)], m, n=8, expo=2.0)
        A.loft([((x + tilt * 0.003, 0, 0.85), (0, 0, 1), (1, 0, 0), 0.012, 0.012), ((x + tilt * 0.003, 0, 0.98), (0, 0, 1), (1, 0, 0), 0.012, 0.012),
                ((x + tilt * 0.003 + 0.05, 0, 1.02), (1, 0, -0.5), (0, 0, 1), 0.014, 0.014)], "WoodDark", n=6, expo=2.0)
    return A.build(col)


def build_box_fixed(col):
    """不能動的紙箱（電箱旁那一個）：0.55 × 0.42、高 0.36；正面貼著標籤。"""
    A = Asset("v3_box_fixed")
    A.cube((0.55, 0.42, 0.36), (0, 0, 0.18), "WoodPale", bevel=0.01)
    A.cube((0.551, 0.12, 0.004), (0, 0, 0.361), "ClothMustard", bevel=0.0)        # 封箱膠帶
    A.cube((0.18, 0.004, 0.1), (0, -0.212, 0.2), "Paper", bevel=0.0)              # 標籤（-Y＝正面）
    return A.build(col)


def build_box_movable(col):
    """可以搬的紙箱：0.49 × 0.41、高 0.28（Unity 會再縮到 0.85）；封箱膠帶。"""
    A = Asset("v3_box_movable")
    A.cube((0.49, 0.41, 0.28), (0, 0, 0.14), "WoodPale", rot=(0, 0, J(1)), bevel=0.01)
    A.cube((0.11, 0.411, 0.004), (0, 0, 0.281), "ClothMustard", bevel=0.0)
    A.cube((0.11, 0.004, 0.08), (0, -0.206, 0.24), "ClothMustard", bevel=0.0)
    return A.build(col)


def build_stool(col):
    """縫紉機的圓凳：直徑 0.34、高 0.5；四隻腳＋腳環＋圓座面。"""
    A = Asset("v3_stool")
    A.lathe([(0.0, 0.45), (0.165, 0.45), (0.17, 0.47), (0.16, 0.495), (0.0, 0.5)], (0, 0, 0), "WoodWarm", segs=16)
    for k in range(4):
        a = k * math.tau / 4 + math.pi / 4
        x, y = math.cos(a) * 0.12, math.sin(a) * 0.12
        A.cube((0.03, 0.03, 0.45), (x, y, 0.225), "Wood", rot=(math.sin(a) * -4, math.cos(a) * 4, 0), bevel=0.005, taper=1.15)
    A.lathe([(0.115, -0.008), (0.135, -0.008), (0.135, 0.008), (0.115, 0.008)], (0, 0, 0.18), "Wood", segs=16)
    return A.build(col)


def build_coat_pegboard(col):
    """掛外套的木掛鉤板（牆上）：0.62 寬，在 1.6 m；背面在 Y＝+0.255（貼牆），一根長木釘伸出來掛外套。"""
    A = Asset("v3_coat_pegboard")
    A.cube((0.62, 0.025, 0.1), (0, 0.255, 1.6), "WoodDark", bevel=0.008)
    for x in (-0.2, 0.2):
        A.cyl(0.012, 0.08, (x, 0.21, 1.6), "WoodPale", rot=(90, 0, 0), segs=8)
    A.cyl(0.014, 0.26, (0, 0.13, 1.6), "WoodPale", rot=(90, 0, 0), segs=8)              # 掛外套的長木釘
    A.cyl(0.02, 0.02, (0, 0.0, 1.6), "WoodPale", rot=(90, 0, 0), segs=8)
    return A.build(col)


def build_hanging_coat(col):
    """那件外套（掛在木釘上）：衣身、領子、兩隻袖子、鈕扣、衣架；吊牌是另一個物件 HC_CoatTag（S2 謎題用它的位置）。"""
    A = Asset("v3_hanging_coat")
    top = 1.52
    A.lathe([(0.0, 0.0), (0.004, 0.0), (0.004, 0.06), (0.0, 0.065)], (0, 0, top - 0.05), "Steel", segs=6)
    A.cube((0.44, 0.014, 0.02), (0, 0, top - 0.075), "WoodPale", bevel=0.004)
    rings = []
    for i in range(6):
        f = i / 5
        z = top - 0.09 - 1.06 * f
        rings.append(((0, 0.0 + 0.02 * f, z), (0, 0, -1), (0, 1, 0), 0.46 + 0.07 * f, 0.18 + 0.12 * min(f * 2, 1)))
    A.loft(rings, "ClothSlate", n=10, expo=3.5)
    A.cube((0.2, 0.06, 0.07), (0, -0.07, top - 0.12), "ClothSlate", rot=(20, 0, 0), bevel=0.012)            # 領子
    for sx in (-1, 1):
        A.loft([((sx * 0.24, 0.0, top - 0.12), (sx * 0.2, 0, -1), (0, 1, 0), 0.09, 0.11),
                ((sx * 0.27, 0.02, top - 0.12 - 0.62), (sx * 0.03, 0, -1), (0, 1, 0), 0.075, 0.09)], "ClothSlate", n=8, expo=3.0)
    for k in range(4):
        A.cyl(0.011, 0.01, (0.0, -0.155, top - 0.25 - k * 0.17), "Brass", rot=(90, 0, 0), segs=8)
    ob = A.build(col)
    # 吊牌：另一個物件（Unity 端以名稱找它的位置），掛在 Unity +X 那隻袖口（Blender -X）。
    T = Asset("HC_CoatTag")
    T.cube((0.05, 0.004, 0.08), (0, 0, 0), "Paper", bevel=0.0)
    T.cyl(0.0015, 0.04, (0, 0, 0.055), "ThreadRed", segs=4)
    old_tag = bpy.data.objects.get("HC_CoatTag")
    if old_tag:
        bpy.data.objects.remove(old_tag, do_unlink=True)
    tag = T.build(col)
    tag.name = "HC_CoatTag"
    tag.location = (-0.24, -0.05, 0.78)
    tag.parent = ob
    return ob


def build_power_box(col):
    """電箱（S1；拉桿另外是程式的物件）：0.36 × 0.12、高 0.4；樞紐點在箱子中央。正面朝 -Y，有警告標誌、小門的把手。"""
    A = Asset("v3_power_box")
    A.cube((0.36, 0.12, 0.4), (0, 0, 0), "Paint", bevel=0.01)
    A.cube((0.32, 0.01, 0.36), (0, -0.064, 0), "Paint", bevel=0.006)
    A.cube((0.07, 0.004, 0.06), (0.08, -0.071, 0.1), "ClothMustard", rot=(0, 45, 0), bevel=0.0)        # 警告標誌（黃色菱形）
    A.cube((0.012, 0.02, 0.05), (0.14, -0.075, 0), "Steel", bevel=0.003)
    A.cube((0.36, 0.13, 0.03), (0, 0.0, 0.215), "Paint", bevel=0.006)
    return A.build(col)


def build_picture_frame(col):
    """牆上的老照片相框（換掉 Poly Haven 的相框）：0.34 × 0.42；背面貼牆。"""
    A = Asset("v3_picture_frame")
    A.cube((0.34, 0.025, 0.42), (0, -0.0125, 0), "WoodDark", bevel=0.01)
    A.cube((0.26, 0.004, 0.34), (0, -0.027, 0), "Paper", bevel=0.0)
    A.cube((0.18, 0.003, 0.24), (0, -0.03, 0.01), "ClothSlate", bevel=0.0)
    return A.build(col)

# ================================================================== 第三批：牆、地板、天花板（可以無縫重複的手繪貼圖）、窗框

def _periodic_noise(np, n, freqs, seed):
    """可以無縫重複的雜訊：只用整數頻率的正弦波相加（左右、上下接得起來）。"""
    r = np.random.default_rng(seed)
    y, x = np.mgrid[0:n, 0:n] / n * 2 * np.pi
    out = np.zeros((n, n))
    for f in freqs:
        for _ in range(3):
            fx, fy = r.integers(-f, f + 1), r.integers(-f, f + 1)
            if fx == 0 and fy == 0:
                continue
            out += np.sin(fx * x + fy * y + r.uniform(0, 6.283)) / max(abs(fx) + abs(fy), 1)
    out -= out.min()
    return out / max(out.max(), 1e-6)


def _hex(np, h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def _save(np, name, rgb):
    n = rgb.shape[0]
    img = bpy.data.images.get(name) or bpy.data.images.new(name, n, n, alpha=False)
    if img.size[0] != n:
        bpy.data.images.remove(img)
        img = bpy.data.images.new(name, n, n, alpha=False)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((n, n, 1))], axis=2)
    img.pixels.foreach_set(rgba[::-1].astype(np.float32).ravel())   # Blender 的影像是由下往上
    os.makedirs(SURFACES, exist_ok=True)
    img.filepath_raw = os.path.join(SURFACES, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img.filepath_raw


def build_surfaces():
    """地板（木條，2.4 m 一格）、牆（灰泥，3 m 一格）、天花板（淺灰泥，3 m 一格）。
    色塊筆觸＝雜訊色階化（和家具的烘焙一樣），木條之間有深色的縫。sRGB 顏色，Unity 端用世界座標貼圖。"""
    import numpy as np
    n = 1024
    out = []
    # ---- 地板：16 條 × 每條幾段 ----
    base, dark = _hex(np, "#8A5E3B"), _hex(np, "#3A2418")
    rgb = np.zeros((n, n, 3))
    rr = np.random.default_rng(5)
    cols = 16
    cw = n // cols
    brush = _periodic_noise(np, n, [3, 6, 12], 11)
    grain = _periodic_noise(np, n, [24, 40], 12)
    for c in range(cols):
        y0 = int(rr.integers(0, n))
        segs = []
        pos = 0
        while pos < n:
            L = int(rr.integers(n // 4, n // 2))
            segs.append((pos, min(n, pos + L)))
            pos += L
        for (a, b) in segs:
            tone = rr.uniform(0.86, 1.1)
            for yy in range(a, b):
                row = (yy + y0) % n
                rgb[row, c * cw:(c + 1) * cw] = base * tone
            seam = (a + y0) % n
            rgb[seam:seam + 3, c * cw:(c + 1) * cw] = dark
        rgb[:, c * cw:c * cw + 3] = dark
    step = np.floor(brush * 4) / 4
    rgb *= (0.9 + step * 0.16)[..., None]
    rgb *= (0.95 + grain * 0.08)[..., None]
    out.append(_save(np, "v3_floor_planks", rgb))
    # ---- 牆：暖灰泥，大塊的深淺 ----
    base = _hex(np, "#B3A893")
    blot = _periodic_noise(np, n, [2, 4, 8], 21)
    fine = _periodic_noise(np, n, [16, 32], 22)
    step = np.floor(blot * 5) / 5
    rgb = np.ones((n, n, 3)) * base
    rgb *= (0.88 + step * 0.18)[..., None]
    rgb *= (0.97 + fine * 0.05)[..., None]
    rgb = rgb * np.array([1.0, 0.99, 0.97])
    out.append(_save(np, "v3_wall_plaster", rgb))
    # ---- 天花板：淺一點 ----
    base = _hex(np, "#D2CABA")
    blot = _periodic_noise(np, n, [2, 5], 31)
    step = np.floor(blot * 4) / 4
    rgb = np.ones((n, n, 3)) * base * (0.92 + step * 0.12)[..., None]
    out.append(_save(np, "v3_ceiling_plaster", rgb))
    return out


def build_shop_window(col):
    """店面的前窗（窗框、窗格、窗台、下半截的蕾絲窗簾與簾桿；玻璃和光束仍由程式做）。
    世界尺寸（Unity 端縮放設成 1）：窗洞寬 1.5、高 1.25、下緣 0.95 m；樞紐點在窗洞下方的地面、牆的內側面。正面朝 -Y（房間）。"""
    A = Asset("v3_shop_window")
    w, h, b, ft = 1.5, 1.25, 0.95, 0.07
    cz = b + h / 2
    for (sz, loc) in [((w + 2 * ft, 0.06, ft), (0, -0.03, b + h + ft / 2)), ((ft, 0.06, h), (w / 2 + ft / 2, -0.03, cz)),
                      ((ft, 0.06, h), (-w / 2 - ft / 2, -0.03, cz))]:
        A.cube(sz, loc, "WoodTrim", bevel=0.01)
    A.cube((w + 0.24, 0.16, 0.06), (0, -0.07, b - 0.03), "WoodTrim", bevel=0.012)            # 窗台
    A.cube((0.035, 0.04, h), (0, -0.02, cz), "WoodTrim", bevel=0.006)                         # 直的窗格
    A.cube((w, 0.04, 0.035), (0, -0.02, b + h * 0.7), "WoodTrim", bevel=0.006)                # 橫的窗格
    A.cyl(0.008, w + 0.04, (0, -0.07, b + 0.62), "Brass", rot=(0, 90, 0), segs=8)             # 簾桿
    grid = []
    for z in (b + 0.6, b + 0.3, b + 0.05):
        grid.append([Vector((x, -0.075 + math.sin(x * 40) * 0.008, z)) for x in [-w / 2 + w * i / 14 for i in range(15)]])
    A.sheet(grid, "ClothCream", thickness=0.004)
    return A.build(col)

# ================================================================== 2026-11-17 第二版：衣服、外套、布捲（使用者：外套形狀怪、厚度不規整；衣架上的衣服糊在一起；布捲應該是圓的）

# 衣身的斷面（由上往下）：(往下多少, 寬, 厚)。寬沿 X、正面朝 -Y；厚度是固定的一層衣服，不會忽厚忽薄。
GARMENT_KINDS = {
    "coat":  {"body": [(0.02, 0.18, 0.12), (0.06, 0.42, 0.17), (0.12, 0.46, 0.19), (0.30, 0.46, 0.19), (0.55, 0.43, 0.18), (0.80, 0.46, 0.18), (1.00, 0.50, 0.18)],
              "sleeve": 0.62, "sleeve_w": 0.11, "buttons": 4, "lapel": True, "pockets": True},
    "shirt": {"body": [(0.02, 0.16, 0.04), (0.06, 0.40, 0.05), (0.12, 0.42, 0.055), (0.35, 0.41, 0.055), (0.62, 0.42, 0.05), (0.70, 0.42, 0.05)],
              "sleeve": 0.58, "sleeve_w": 0.085, "buttons": 5, "lapel": False, "pockets": False},
    "dress": {"body": [(0.02, 0.15, 0.05), (0.06, 0.34, 0.07), (0.14, 0.36, 0.08), (0.36, 0.30, 0.07), (0.62, 0.44, 0.09), (0.92, 0.56, 0.1)],
              "sleeve": 0.16, "sleeve_w": 0.09, "buttons": 0, "lapel": False, "pockets": False},
}


def garment2(A, M, mat, kind, seed, hanger=True):
    """一件掛著的衣服（衣架＋衣身＋袖子＋領子＋鈕扣）。區域座標：衣架鉤在原點上方，衣身頂在 z＝0；寬沿 X、正面 -Y。M＝放到哪裡、朝哪個方向。"""
    r = random.Random(seed)
    k = GARMENT_KINDS[kind]
    before = set(A.bm.verts)
    rings = []
    for (dz, w, d) in k["body"]:
        rings.append(((0, 0, -dz), (0, 0, -1), (0, -1, 0), w, d))
    A.loft(rings, mat, n=14, expo=2.8)
    L = k["body"][-1][0]
    front = -max(d for (_, _, d) in k["body"]) / 2
    # 領子：一圈立起來的領口
    A.loft([((0, 0.0, -0.005), (0, 0, -1), (0, -1, 0), 0.2, 0.14), ((0, -0.005, -0.06), (0, 0, -1), (0, -1, 0), 0.22, 0.16)], mat, n=12, expo=2.4)
    # 袖子：從肩膀垂下，手肘微彎，袖口
    sw = k["sleeve_w"]
    for sx in (-1, 1):
        sh = k["body"][1][1] / 2 - 0.02
        x0 = sx * sh
        if k["sleeve"] < 0.3:      # 短袖
            A.loft([((x0, 0, -0.07), (sx * 0.6, 0, -1), (0, -1, 0), sw * 1.2, sw * 1.1), ((x0 + sx * 0.05, -0.005, -0.07 - k["sleeve"]), (sx * 0.3, 0, -1), (0, -1, 0), sw, sw)], mat, n=10, expo=2.4)
            continue
        el = -0.07 - k["sleeve"] * 0.5
        cf = -0.07 - k["sleeve"]
        A.loft([((x0, 0, -0.07), (sx * 0.25, 0, -1), (0, -1, 0), sw * 1.15, sw * 1.15),
                ((x0 + sx * 0.035, -0.015, el), (sx * 0.05, -0.05, -1), (0, -1, 0), sw, sw * 1.02),
                ((x0 + sx * 0.03, -0.035, cf), (0, -0.12, -1), (0, -1, 0), sw * 0.9, sw * 0.92)], mat, n=10, expo=2.4)
        A.loft([((x0 + sx * 0.03, -0.035, cf + 0.03), (0, -0.12, -1), (0, -1, 0), sw * 0.98, sw), ((x0 + sx * 0.03, -0.037, cf), (0, -0.12, -1), (0, -1, 0), sw * 0.98, sw)],
               "ClothCharcoal" if kind == "coat" else mat, n=10, expo=2.4)
    # 西裝領（外套）：兩片翻領，形成 V 字
    if k["lapel"]:
        for sx in (-1, 1):
            A.cube((0.075, 0.014, 0.24), (sx * 0.055, front - 0.004, -0.17), mat, rot=(8, 0, -sx * 14), bevel=0.006, taper=0.55)
    # 鈕扣
    for i in range(k["buttons"]):
        z = -0.22 - i * (0.5 if kind == "coat" else 0.55) / max(k["buttons"], 1)
        A.cyl(0.011 if kind == "coat" else 0.006, 0.008, (0.0 if kind != "coat" else 0.02, front - 0.006, z), "Brass" if kind == "coat" else "Paper", rot=(90, 0, 0), segs=8)
    # 口袋蓋（外套）
    if k["pockets"]:
        for sx in (-1, 1):
            A.cube((0.13, 0.012, 0.035), (sx * 0.12, front - 0.004, -0.62), mat, rot=(0, 0, J(3)), bevel=0.004)
    # 衣架
    if hanger:
        A.loft([((-0.2, 0, -0.05), (1, 0, 0.25), (0, 0, 1), 0.02, 0.012), ((0, 0, -0.01), (1, 0, 0), (0, 0, 1), 0.022, 0.012),
                ((0.2, 0, -0.05), (1, 0, -0.25), (0, 0, 1), 0.02, 0.012)], "WoodPale", n=8, expo=3.0)
        A.cyl(0.0035, 0.06, (0, 0, 0.02), "Steel", segs=6)
        A.loft([((0, 0, 0.05), (0, 0, 1), (1, 0, 0), 0.007, 0.007), ((0, -0.02, 0.075), (0, -1, 0.2), (1, 0, 0), 0.007, 0.007),
                ((0, -0.035, 0.06), (0, -0.2, -1), (1, 0, 0), 0.007, 0.007)], "Steel", n=6, expo=2.0)
    new = [v for v in A.bm.verts if v not in before]
    bmesh.ops.transform(A.bm, matrix=M, verts=new)
    return L


def build_rack_clothes(col):
    """吊衣架上的衣服（第二版）：五件、間距拉開（不糊在一起），掛在橫桿上、衣身和橫桿垂直，稍微轉一點讓正面看得到。"""
    A = Asset("v3_rack_clothes")
    specs = [("ClothNavy", "coat"), ("ClothCream", "shirt"), ("ClothBrick", "dress"), ("ClothSage", "shirt"), ("ClothCharcoal", "coat")]
    for i, (mat, kind) in enumerate(specs):
        x = -0.54 + i * 0.27
        ang = math.radians(90 + (14 if i % 2 == 0 else -14))
        M = Matrix.Translation(Vector((x, 0, 1.55))) @ Matrix.Rotation(ang, 4, "Z")
        garment2(A, M, mat, kind, 200 + i)
    return A.build(col)


def build_hanging_coat(col):
    """那件外套（第二版）：掛在牆上的長木釘；正面朝 -Y（房間）。衣身厚度一致、肩線、翻領、四顆鈕扣、口袋蓋、袖口；
    吊牌是另一個物件 HC_CoatTag，掛在 Unity +X 那隻袖口（Blender -X）。"""
    A = Asset("v3_hanging_coat")
    garment2(A, Matrix.Translation(Vector((0, 0, 1.47))), "ClothSlate", "coat", 7)
    ob = A.build(col)
    T = Asset("HC_CoatTag")
    T.cube((0.05, 0.004, 0.08), (0, 0, 0), "Paper", bevel=0.0)
    T.cyl(0.0015, 0.04, (0, 0, 0.055), "ThreadRed", segs=4)
    old_tag = bpy.data.objects.get("HC_CoatTag")
    if old_tag:
        bpy.data.objects.remove(old_tag, do_unlink=True)
    tag = T.build(col)
    tag.name = "HC_CoatTag"
    tag.location = (-0.245, -0.07, 0.71)     # 右手（Unity +X）那隻袖口下面，垂在袖口外（不卡進袖子）
    tag.parent = ob
    return ob


def build_fabric_bolts(col):
    """木箱裡站著的布捲（第二版：圓的）：8 捲，每捲是圓筒＋上面露出的紙芯＋一片沒捲好的布邊，各種顏色、高度不一、微微往後靠。"""
    A = Asset("v3_fabric_bolts")
    mats = ["ClothNavy", "ClothCream", "ClothBrick", "ClothSage", "ClothMustard", "ClothSlate", "ClothRose", "ClothOlive"]
    for i in range(8):
        x = -0.255 + (i % 4) * 0.17 + J(0.015)
        y = -0.075 + (i // 4) * 0.15 + J(0.01)
        h = rng.uniform(1.0, 1.32)
        rad = rng.uniform(0.05, 0.066)
        tilt = (rng.uniform(3, 7), J(4), 0)
        A.cyl(rad, h, (x, y, 0.04 + h / 2), mats[i], rot=tilt, segs=14, bevel=0.0)
        A.cyl(0.016, 0.03, (x + math.sin(math.radians(tilt[1])) * h / 2, y - math.sin(math.radians(tilt[0])) * h / 2, 0.04 + h + 0.005), "WoodPale", rot=tilt, segs=8)
        A.cube((0.004, rad * 1.1, h * 0.5), (x + rad * 0.9, y, 0.04 + h * 0.62), mats[i], rot=tilt, bevel=0.0)   # 布邊
    return A.build(col)

BUILDERS = [
    ("v3_shop_counter", build_counter, 2048),
    ("v3_button_cabinet", build_button_cabinet, 1024),
    ("v3_clothes_rack", build_clothes_rack, 1024),
    ("v3_rack_clothes", build_rack_clothes, 1024),
    ("v3_ironing_board", build_ironing_board, 1024),
    ("v3_iron", build_iron, 512),
    ("v3_fabric_shelf", build_fabric_shelf, 2048),
    ("v3_shelf_fabrics", build_shelf_fabrics, 1024),
    ("v3_cheval_mirror", build_mirror, 1024),
    ("v3_cut_table", build_cut_table, 1024),
    ("v3_fabric_roll", build_fabric_roll, 512),
    ("v3_fitting_booth", build_fitting_booth, 1024),
    ("v3_booth_stool", build_booth_stool, 512),
    ("v3_customer_chair", build_customer_chair, 1024),
    # 第二批
    ("v3_cash_register", build_cash_register, 1024),
    ("v3_button_display", build_button_display, 1024),
    ("v3_button_jar", build_button_jar, 512),
    ("v3_ledger", build_ledger, 512),
    ("v3_receipt_book", build_receipt_book, 512),
    ("v3_tape_measure", build_tape_measure, 512),
    ("v3_scissors", build_scissors, 512),
    ("v3_pencil", build_pencil, 256),
    ("v3_chalk", build_chalk, 256),
    ("v3_teacup", build_teacup, 512),
    ("v3_pin_cushion", build_pin_cushion, 512),
    ("v3_counter_bell", build_counter_bell, 512),
    ("v3_counter_lamp", build_counter_lamp, 512),
    ("v3_spool", build_spool, 256),
    ("v3_thread_rack", build_thread_rack, 1024),
    ("v3_wall_shelf", build_wall_shelf, 512),
    ("v3_radio", build_radio, 512),
    ("v3_folded_cloth", build_folded_cloth, 512),
    ("v3_wall_clock", build_wall_clock, 512),
    ("v3_fabric_bin", build_fabric_bin, 1024),
    ("v3_fabric_bolts", build_fabric_bolts, 1024),
    ("v3_umbrella_stand", build_umbrella_stand, 512),
    ("v3_umbrellas", build_umbrellas, 512),
    ("v3_box_fixed", build_box_fixed, 512),
    ("v3_box_movable", build_box_movable, 512),
    ("v3_stool", build_stool, 512),
    ("v3_coat_pegboard", build_coat_pegboard, 512),
    ("v3_hanging_coat", build_hanging_coat, 1024),
    ("v3_power_box", build_power_box, 512),
    ("v3_picture_frame", build_picture_frame, 512),
    # 第三批
    ("v3_shop_window", build_shop_window, 1024),
]


def run_shop(only=None):
    sc = bpy.data.scenes.get("ShopStylized") or bpy.data.scenes.new("ShopStylized")
    bpy.context.window.scene = sc
    col = sc.collection
    report = []
    for name, fn, res in BUILDERS:
        if only and name not in only:
            continue
        old = bpy.data.objects.get(name)
        if old:
            for c in list(old.children):
                bpy.data.objects.remove(c, do_unlink=True)
            bpy.data.objects.remove(old, do_unlink=True)
        ob = fn(col)
        ob.name = name
        kids = [c for c in ob.children]
        uv_unwrap(ob)
        bake(ob, res)
        for c in kids:                 # 子物件（例如外套的吊牌）各自烘一張小貼圖
            uv_unwrap(c)
            bake(c, 256)
        if kids:
            select_only(ob)
            for c in kids:
                c.select_set(True)
            path = os.path.join(MODELS, ob.name + ".fbx")
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                                     bake_space_transform=True, object_types={"MESH"}, use_mesh_modifiers=True,
                                     mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False, bake_anim=False,
                                     axis_forward="-Z", axis_up="Y", path_mode="STRIP")
        else:
            export_fbx(ob)
        me = ob.data
        report.append("%s: %d verts, %d tris, %d mats, %.2fx%.2fx%.2f" % (name, len(me.vertices), sum(len(p.vertices) - 2 for p in me.polygons), len(me.materials),
                                                                     ob.dimensions.x, ob.dimensions.y, ob.dimensions.z))
    return report


try:
    SURFACES
except NameError:
    SURFACES = os.path.join(os.path.dirname(OUT_UNITY), "Lantern", "Resources", "StyleShop")

if globals().get("SHOP_LIBRARY_ONLY") is not True:
    REPORT = run_shop(ONLY)
    if ONLY is None or "surfaces" in ONLY:
        REPORT += build_surfaces()
    print("\n".join(REPORT))
