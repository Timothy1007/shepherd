using System;
using System.IO;
using Shepherd.Battle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Shepherd.Battle.Editor
{
    /// <summary>
    /// 離屏截圖：在指定解析度下渲染 Combat Lab 介面並存成 PNG，用來實際檢查文字清晰度與排版。
    /// 縮放倍率與 Game 視窗中 CanvasScaler 的計算方式相同。不修改場景或任何資產。
    /// batchmode（需圖形裝置，不可加 -nographics）：
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod Shepherd.Battle.Editor.CombatLabCapture.CaptureFromCommandLine
    ///   -captureDir &lt;資料夾&gt; -captureSizes 1920x1080,1280x720
    /// </summary>
    public static class CombatLabCapture
    {
        [MenuItem("Shepherd/Combat Lab/截圖檢查（1920×1080、1280×720）")]
        public static void CaptureFromMenu()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CombatLabCapture"));
            CaptureAll(dir, new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) });
            EditorUtility.RevealInFinder(dir);
        }

        public static void CaptureFromCommandLine()
        {
            string dir = Arg("-captureDir") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CombatLabCapture"));
            string sizes = Arg("-captureSizes") ?? "1920x1080,1280x720";
            var list = new System.Collections.Generic.List<Vector2Int>();
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
            foreach (var size in sizes)
            {
                string path = Path.Combine(dir, "combatlab_" + size.x + "x" + size.y + ".png");
                Capture(size.x, size.y, path, true);
                Debug.Log("[CombatLab] 截圖 " + path);
            }
        }

        /// <summary>渲染一張截圖。populate＝true 時先操作一回合，讓預覽、編排與紀錄都有內容。</summary>
        public static void Capture(int width, int height, string path, bool populate)
        {
            var root = new GameObject("CombatLabCaptureRoot");
            var camGo = new GameObject("CombatLabCaptureCamera");
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D tex = null;
            try
            {
                var view = root.AddComponent<CombatLabView>();
                view.Build(false);
                if (populate) Populate(view);

                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.orthographic = true;
                cam.targetTexture = rt;

                var canvas = root.GetComponentInChildren<Canvas>();
                var scaler = canvas.GetComponent<CanvasScaler>();
                float scale = CombatLabView.ScaleFactorFor(scaler, width, height);
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                canvas.scaleFactor = scale;

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
                view.Refresh();
                Canvas.ForceUpdateCanvases();
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Populate(CombatLabView view)
        {
            var s = view.Session;
            var hand = s.Snapshot().Hand;
            s.AddCard(hand[0].Id);
            s.AddCard(hand[1].Id);
            s.Commit();
            hand = s.Snapshot().Hand;
            s.SelectSchool(Core.SchoolKind.Relation);
            s.AddCard(hand[2].Id);
            s.AddCard(hand[3].Id);
            s.Preview();
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
