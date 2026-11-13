using UnityEditor;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// 提燈 QA 用的 Poly Haven（CC0）素材匯入設定：Lantern/Resources/PH 底下
    /// *_nor＝法線貼圖、*_rough＝線性灰階；模型不自動產生材質（執行時改用 LanternReveal 雙狀態材質）。
    /// </summary>
    public sealed class LanternAssetImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        private bool Ours => assetPath.Replace('\\', '/').Contains("/Lantern/Resources/PH/");

        private void OnPreprocessTexture()
        {
            if (!Ours) return;
            var ti = (TextureImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            if (n.EndsWith("_nor")) ti.textureType = TextureImporterType.NormalMap;
            else if (n.EndsWith("_rough")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; }
            else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; }
        }

        private void OnPreprocessModel()
        {
            if (!Ours) return;
            var mi = (ModelImporter)assetImporter;
            // 材質只保留名稱（嵌在模型裡、不產生檔案）：執行時依名稱對應到哪一組貼圖，再換成 LanternReveal 材質。
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.materialSearch = ModelImporterMaterialSearch.Local;
            mi.addCollider = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
        }
    }
}
