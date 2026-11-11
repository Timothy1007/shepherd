"""產生 Case01 沉浸式住宅 QA 的模型（FBX）、貼圖（PNG）與家庭合照（Cycles 算圖＋做舊）。

用法（Blender 5.2，背景模式）：
  blender -b --factory-startup --python tools/blender/case01_immersive/build.py -- [輸出資料夾] [all|tex|models|photo]
預設輸出：unity/Assets/Exploration/Case01Art/Immersive/Resources/Case01Immersive。全部程序生成，沒有外部模型、貼圖或照片。
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
repo = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
out = os.path.abspath(argv[0]) if argv and argv[0] not in ("all", "tex", "models", "photo") else os.path.join(repo, "unity", "Assets", "Exploration", "Case01Art", "Immersive", "Resources", "Case01Immersive")
which = next((a for a in argv if a in ("all", "tex", "models", "photo")), "all")

if which in ("all", "tex"):
    import textures
    textures.build_all(out)
if which in ("all", "models"):
    import models
    models.build_all(out)
if which in ("all", "photo"):
    import photo
    import tempfile
    photo.build(out, os.path.join(tempfile.gettempdir(), "case01_photo_render"))
