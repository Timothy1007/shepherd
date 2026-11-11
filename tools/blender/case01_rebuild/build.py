"""Case01 全屋重製 v2 的 Blender 產線（Blender 5.2，背景模式）。

  blender -b --factory-startup --python tools/blender/case01_rebuild/build.py -- [pbr] [photo] [room] [ref]
輸出：unity/Assets/Exploration/Case01Rebuild/Art/Resources/Case01Rebuild/（FBX、PBR 貼圖、暫用合照）；
參考算圖：docs/story/case01_seventeenth_door/house_rebuild_v2/。全部程序生成、原創，沒有第三方素材。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "unity", "Assets", "Exploration", "Case01Rebuild", "Art", "Resources", "Case01Rebuild")
DOCS = os.path.join(REPO, "docs", "story", "case01_seventeenth_door", "house_rebuild_v2")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
steps = set(argv) or {"pbr", "photo", "room", "ref"}

import pbr  # noqa: E402
import room  # noqa: E402
import render  # noqa: E402

os.makedirs(OUT, exist_ok=True)
if "pbr" in steps:
    pbr.build_all(OUT)
if "photo" in steps:
    render.photo(os.path.join(OUT, "RB_Photo_A.png"))
if "room" in steps or "ref" in steps:
    room.build(OUT)
    if "ref" in steps:
        render.reference(DOCS, OUT, os.path.join(OUT, "RB_Photo_A.png"))
