using System;
using System.Collections.Generic;
using System.Text;
using Shepherd.Battle.UI.Arena;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 專案改用 URP：建立管線資產（Forward+、柔和陰影、HDR、SSAO）、切到 Linear 色彩空間、把 Standard 材質資產轉成 URP Lit。
/// 可重複執行（已存在的資產會沿用並重設參數）。
/// </summary>
public static class ShepherdUrpSetup
{
    public const string Folder = "Assets/Settings/Rendering";
    public const string AssetPath = Folder + "/ShepherdURP.asset";
    public const string RendererPath = Folder + "/ShepherdURP_Renderer.asset";

    [MenuItem("Shepherd/Rendering/套用 URP 設定")]
    public static void Run()
    {
        var log = new StringBuilder();
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "Rendering");

        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
        }
        ResourceReloader.TryReloadAllNullIn(data, UniversalRenderPipelineAsset.packagePath);
        var so = new SerializedObject(data);
        Set(so, "m_RenderingMode", 2); // Forward+
        Set(so, "m_DepthPrimingMode", 0);
        Set(so, "m_IntermediateTextureMode", 1);
        so.ApplyModifiedPropertiesWithoutUndo();
        EnsureSsao(data, log);

        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            asset = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        var a = new SerializedObject(asset);
        Set(a, "m_SupportsHDR", true);
        Set(a, "m_HDRColorBufferPrecision", 0);
        Set(a, "m_MSAA", 1);
        Set(a, "m_RenderScale", 1f);
        Set(a, "m_RequireDepthTexture", true);
        Set(a, "m_RequireOpaqueTexture", false);
        Set(a, "m_MainLightRenderingMode", 1);
        Set(a, "m_MainLightShadowsSupported", true);
        Set(a, "m_MainLightShadowmapResolution", 4096);
        Set(a, "m_AdditionalLightsRenderingMode", 1);
        Set(a, "m_AdditionalLightsPerObjectLimit", 8);
        Set(a, "m_AdditionalLightShadowsSupported", true);
        Set(a, "m_AdditionalLightsShadowmapResolution", 4096);
        Set(a, "m_AdditionalLightsShadowResolutionTierLow", 512);
        Set(a, "m_AdditionalLightsShadowResolutionTierMedium", 1024);
        Set(a, "m_AdditionalLightsShadowResolutionTierHigh", 2048);
        Set(a, "m_AdditionalLightsCookieResolution", 2048);
        Set(a, "m_ReflectionProbeBlending", true);
        Set(a, "m_ReflectionProbeBoxProjection", true);
        Set(a, "m_ShadowDistance", 30f);
        Set(a, "m_ShadowCascadeCount", 2);
        Set(a, "m_SoftShadowsSupported", true);
        Set(a, "m_SoftShadowQuality", 3);
        Set(a, "m_SupportsLightCookies", true);
        Set(a, "m_ColorGradingMode", 1); // HDR grading
        Set(a, "m_ColorGradingLutSize", 32);
        Set(a, "m_UseFastSRGBLinearConversion", false);
        a.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(data);

        GraphicsSettings.defaultRenderPipeline = asset;
        int saved = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null; // 全部跟隨預設管線
        }
        QualitySettings.SetQualityLevel(saved, false);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        log.AppendLine("pipeline=" + AssetPath + " colorSpace=" + PlayerSettings.colorSpace);

        int upgraded = 0, skipped = 0;
        var unknown = new Dictionary<string, int>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            // 使用者本機未提交的美術工作區不動（由使用者自行處理）。
            if (path.StartsWith("Assets/Exploration/Art/", StringComparison.Ordinal)) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) continue;
            if (ShepherdShading.UpgradeFromStandard(m)) { upgraded++; EditorUtility.SetDirty(m); }
            else
            {
                skipped++;
                var sn = m.shader != null ? m.shader.name : "(null)";
                unknown[sn] = unknown.TryGetValue(sn, out var c) ? c + 1 : 1;
            }
        }
        AssetDatabase.SaveAssets();
        log.AppendLine("materials upgraded=" + upgraded + " untouched=" + skipped);
        foreach (var kv in unknown) log.AppendLine("  untouched shader " + kv.Key + " x" + kv.Value);
        Debug.Log("[ShepherdUrpSetup]\n" + log);
    }

    private static void EnsureSsao(UniversalRendererData data, StringBuilder log)
    {
        foreach (var f in data.rendererFeatures)
            if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") { ConfigureSsao(f); log.AppendLine("ssao=existing"); return; }
        var t = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        if (t == null) { log.AppendLine("ssao=type missing"); return; }
        var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
        feature.name = "ScreenSpaceAmbientOcclusion";
        AssetDatabase.AddObjectToAsset(feature, data);
        ConfigureSsao(feature);
        data.rendererFeatures.Add(feature);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id);
        var so = new SerializedObject(data);
        var map = so.FindProperty("m_RendererFeatureMap");
        map.arraySize = data.rendererFeatures.Count;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty();
        log.AppendLine("ssao=added");
    }

    private static void ConfigureSsao(ScriptableRendererFeature f)
    {
        var so = new SerializedObject(f);
        Set(so, "m_Settings.Intensity", 2.2f);
        Set(so, "m_Settings.Radius", 0.32f);
        Set(so, "m_Settings.DirectLightingStrength", 0.35f);
        Set(so, "m_Settings.Source", 1); // DepthNormals
        Set(so, "m_Settings.NormalSamples", 1);
        Set(so, "m_Settings.Downsample", false);
        Set(so, "m_Settings.AfterOpaque", false);
        Set(so, "m_Settings.Samples", 0); // High
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject so, string name, object v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("[ShepherdUrpSetup] no property " + name + " on " + so.targetObject.GetType().Name); return; }
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: p.boolValue = Convert.ToBoolean(v); break;
            case SerializedPropertyType.Float: p.floatValue = Convert.ToSingle(v); break;
            case SerializedPropertyType.Enum: p.intValue = Convert.ToInt32(v); break;
            default: p.intValue = Convert.ToInt32(v); break;
        }
    }
}
