"""重建 Case01 v0.6 客廳美術標準間的模型（FBX）與貼圖（PNG）。

用法（Blender 5.2，背景模式）：
  blender -b --factory-startup --python tools/blender/case01_living_room/build.py -- <輸出資料夾>
預設輸出到 unity/Assets/Exploration/Case01Art/LivingRoom/Resources/Case01LivingRoom。
全部是程序產生（固定亂數種子），沒有外部模型或貼圖素材。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import models  # noqa: E402
import textures  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
repo = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
out = os.path.abspath(argv[0]) if argv else os.path.join(repo, "unity", "Assets", "Exploration", "Case01Art", "LivingRoom", "Resources", "Case01LivingRoom")
which = argv[1] if len(argv) > 1 else "all"
if which in ("all", "tex"):
    textures.build_all(out)
    print("[Case01LivingRoom] textures ->", out)
if which in ("all", "models"):
    models.build_all(out)
    print("[Case01LivingRoom] models ->", out)
