using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// v0.6 客廳美術標準間的匯入設定（只作用於 Case01Art/LivingRoom）：
    /// FBX 以公尺匯入、烘焙座標軸轉換、不帶攝影機／燈光／動畫／碰撞體（碰撞由 Case01LivingArt 依家具外形另外放）；
    /// 貼圖重複平鋪、三線性過濾、最大 1024；常春藤葉卡使用 alpha。
    /// 模型與貼圖由 tools/blender/case01_living_room/build.py 重建。
    /// </summary>
    public sealed class Case01LivingRoomImport : AssetPostprocessor
    {
        public const string Folder = "Case01Art/LivingRoom/";

        private void OnPreprocessModel()
        {
            if (!assetPath.Contains(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.generateSecondaryUV = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.wrapMode = assetPath.EndsWith("LR_Figure.png") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.alphaIsTransparency = assetPath.EndsWith("LR_Leaf.png") || assetPath.EndsWith("LR_Figure.png");
        }
    }
}
