using System;
using System.Collections.Generic;
using System.IO;
using Shepherd.Exploration.Case01;
using UnityEditor;
using UnityEngine;

namespace Shepherd.Exploration.Editor
{
    /// <summary>
    /// v0.4 空間敘事截圖（1920×1080／1280×720）：同一視角的 H0～H3（客廳、餐廳、書房）、失序門五種狀態、主角自語、記憶殘響。
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Exploration.Editor.Case01NarrativeCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720
    /// </summary>
    public static class Case01NarrativeCapture
    {
        [MenuItem("Shepherd/Case01/空間敘事 v0.4 截圖")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01NarrativeCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Case01HouseCapture.Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Case01NarrativeCapture"));
            string sizes = Case01HouseCapture.Arg("-captureSizes") ?? "1920x1080,1280x720";
            var list = new List<Vector2Int>();
            foreach (var s in sizes.Split(','))
            {
                var p = s.Trim().Split('x');
                list.Add(new Vector2Int(int.Parse(p[0]), int.Parse(p[1])));
            }
            CaptureAll(dir, list.ToArray());
        }

        public static void CaptureAll(string dir, Vector2Int[] sizes)
        {
            Directory.CreateDirectory(dir);
            foreach (var size in sizes) Sequence(dir, size.x, size.y);
        }

        private static readonly Vector3 LivingPos = new Vector3(3.9f, 0.05f, 6.3f);
        private static readonly Vector3 DiningPos = new Vector3(6.2f, 0.05f, 5.6f);
        private static readonly Vector3 StudyPos = new Vector3(6.3f, 3.05f, 6.3f);

        private static void Sequence(string dir, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            GameObject root = null;
            Case01HouseQA qa = null;
            string prefix = Path.Combine(dir, "v04_" + width + "x" + height + "_");
            try
            {
                root = new GameObject("Case01NarrativeCaptureRoot");
                qa = root.AddComponent<Case01HouseQA>();
                qa.Build(false, null, 1, null, Case01QaMode.NarrativeV04);
                qa.ApplyAtmosphere();
                qa.Hud.ClearMonologue();
                for (int h = 0; h < 4; h++)
                {
                    qa.SetArtStage(h);
                    qa.Hud.ClearMonologue();
                    Case01HouseCapture.Pose(qa, LivingPos, -55f, 8f);
                    Case01HouseCapture.Advance(qa, 2.5f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "H" + h + "_living.png");
                    Case01HouseCapture.Pose(qa, DiningPos, 35f, 12f);
                    Case01HouseCapture.Advance(qa, 0.3f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "H" + h + "_dining.png");
                    Case01HouseCapture.Pose(qa, StudyPos, 40f, 12f);
                    Case01HouseCapture.Advance(qa, 0.3f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "H" + h + "_study.png");
                }
                for (int g = 0; g < Case01HouseQA.GlitchPreviewCount; g++)
                {
                    qa.PreviewGlitch(g);
                    qa.Hud.ClearMonologue();
                    Case01HouseCapture.Advance(qa, 1.2f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "door" + g + "_" + qa.PortalState(g == 0 ? Case01Door.StudyDoor : Case01Door.MezzTriangle) + ".png");
                }
                for (int e = 0; e < Case01Echoes.All.Length; e++)
                {
                    qa.PreviewEcho(e);
                    Case01HouseCapture.AdvanceUntil(qa, q => q.Echo.Alpha > 0.95f || q.Hud.CurrentGroup == Case01Echoes.All[e] && q.Echo.FigureCount == 0, 0.3f);
                    Case01HouseCapture.Shot(qa, rt, prefix + "echo" + e + "_" + Case01Echoes.All[e] + ".png");
                }
                Debug.Log("[Case01NarrativeCapture] 截圖完成 " + prefix + "*.png");
            }
            finally
            {
                if (qa != null) qa.RestoreAtmosphere();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }
    }
}
