using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// v0.5 失序體的可見狀態（探索 → 戰鬥）。任何時刻都不會出現「沒有碎片外殼的舊黑色人形」：
    /// Hidden＝什麼都看不到；DebrisOnly＝只有地上的書頁與照片（人形未建立可見）；Assembling＝碎片聚攏、人形在外殼內凝實；
    /// Manifested＝完成的碎片外殼＋人形；Battle＝戰鬥場景中的同一份外殼（掛在戰鬥敵人身上）。
    /// </summary>
    public enum Case01EnemyVisual
    {
        Hidden,
        DebrisOnly,
        Assembling,
        Manifested,
        Battle,
    }

    /// <summary>
    /// 失序體的碎片外殼：探索中的形成演出與戰鬥中的敵人共用同一份「人形本地座標」配置（同一個亂數種子），
    /// 所以進戰鬥時外殼的形狀、材質、位置都一樣，不會換回舊模型。只是外觀：不加碰撞體、不改敵人 ID、數量、AI 或血條。
    /// 這仍是程序產生的簡化外殼，不是正式失序體美術。
    /// </summary>
    public static class Case01EnemyShell
    {
        public const int PiecesPerFigure = 84;
        public const int Seed = 17;

        /// <summary>失序體原本的「裸身」部位（黑色膠囊與方塊）。外殼建好後隱藏；保留發光的核、裂紋、眼睛與環繞碎片。</summary>
        public static readonly string[] BareParts = { "LegL", "LegR", "Torso", "Head", "ArmL", "ArmR" };

        /// <summary>隱藏裸身部位的 Renderer（碰撞體、骨架、受擊晃動與解除動畫都不變）。</summary>
        public static void HideBareBody(Transform enemyRoot)
        {
            if (enemyRoot == null) return;
            var body = enemyRoot.Find("Body");
            if (body == null) return;
            foreach (var name in BareParts)
            {
                var part = body.Find(name);
                if (part == null) continue;
                var r = part.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }
        }

        public struct Piece
        {
            /// <summary>相對於敵人根物件（未縮放、含朝向）的位置與旋轉。</summary>
            public Vector3 LocalPos;
            public Quaternion LocalRot;
            public Vector3 Scale;
            public int Mat;
        }

        /// <summary>第 index 個失序體的外殼配置（同一個 index／高度永遠得到同一份結果）。</summary>
        public static List<Piece> Layout(int index, float enemyHeight)
        {
            var rng = new System.Random(Seed * 101 + index * 7919);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            // 依 ArenaArt.BuildEnemy 的身體比例（身高 s）貼出外殼：書頁、照片、書本覆蓋整個人形，取代黑色裸身。
            float s = Mathf.Max(0.6f, enemyHeight);
            var list = new List<Piece>(PiecesPerFigure);
            for (int k = 0; k < PiecesPerFigure; k++)
            {
                int kind = k % 3;
                var scale = (kind == 0 ? new Vector3(0.24f, 0.006f, 0.32f) : kind == 1 ? new Vector3(0.2f, 0.06f, 0.27f) : new Vector3(0.16f, 0.008f, 0.21f)) * s;
                Vector3 pos, outward;
                SurfacePoint(k, s, R, out pos, out outward);
                var rot = Quaternion.LookRotation(outward, Mathf.Abs(outward.y) > 0.95f ? Vector3.forward : Vector3.up) *Quaternion.Euler(90f + R(-20f, 20f), 0f, R(-35f, 35f));
                list.Add(new Piece { LocalPos = pos, LocalRot = rot, Scale = scale, Mat = (k + index) % MaterialCount });
            }
            return list;
        }

        /// <summary>人形表面上的一點（頭 12、軀幹 30、雙臂 20、雙腿 22 片）與朝外的方向。</summary>
        private static void SurfacePoint(int k, float s, System.Func<float, float, float> R, out Vector3 pos, out Vector3 outward)
        {
            int slot = k % 42;
            if (slot < 6)
            {
                // 頭（方塊頭 0.34，中心 y2.2）。
                var d = new Vector3(R(-1f, 1f), R(-0.6f, 1f), R(-1f, 1f)).normalized;
                pos = new Vector3(0.02f, 2.2f, 0f) * s + d * 0.24f * s;
                outward = d;
            }
            else if (slot < 21)
            {
                // 軀幹（橢圓柱，半徑 0.3×0.21，y0.95～1.95）。
                float a = R(0f, Mathf.PI * 2f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                pos = new Vector3(d.x * 0.3f, R(0.95f, 1.95f), d.z * 0.21f) * s;
                outward = d;
            }
            else if (slot < 31)
            {
                // 手臂（x±0.43，y0.65～1.85）。
                float side = slot < 26 ? -1f : 1f;
                float a = R(0f, Mathf.PI * 2f);
                var d = new Vector3(Mathf.Cos(a) * 0.6f + side * 0.4f, 0f, Mathf.Sin(a)).normalized;
                pos = new Vector3(side * 0.43f + d.x * 0.1f, R(0.65f, 1.85f), 0.05f + d.z * 0.1f) * s;
                outward = d;
            }
            else
            {
                // 雙腿（x±0.16，y0.05～1.0）。
                float side = slot < 36 ? -1f : 1f;
                float a = R(0f, Mathf.PI * 2f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                pos = new Vector3(side * 0.16f + d.x * 0.11f, R(0.08f, 1.0f), d.z * 0.11f) * s;
                outward = d;
            }
            if (outward.sqrMagnitude < 1e-4f) outward = Vector3.forward;
            outward.Normalize();
        }

        public const int MaterialCount = 6;

        /// <summary>紙、舊紙、書、帶微弱裂隙光的紙、書、相框木（探索與戰鬥共用同一組）。</summary>
        public static Material[] Materials(Case01Palette p)
        {
            var glowPaper = p.Own(ArenaArt.Mat(new Color(0.85f, 0.8f, 0.9f), 0.2f, ArenaArt.Rift * 0.7f));
            return new[] { p.Get("paper"), p.Get("paper_old"), p.Get("book_0"), glowPaper, p.Get("book_3"), p.Get("frame_wood") };
        }

        /// <summary>
        /// 戰鬥：把同一份外殼掛到戰鬥敵人的 Body 底下（受擊晃動、解除時一起壓扁消失）。
        /// 請在戰鬥物件啟用前呼叫，第一個畫面就有外殼。
        /// </summary>
        public static Transform AttachToEnemy(Transform enemyRoot, int index, float enemyHeight, Material[] mats)
        {
            if (enemyRoot == null) return null;
            var body = enemyRoot.Find("Body");
            var parent = body != null ? body : enemyRoot;
            var shell = ArenaArt.Group("Case01Shell", parent, Vector3.zero);
            // 外殼以敵人根物件為基準（Body 有等比縮放＝身高）。
            shell.localScale = new Vector3(1f / parent.localScale.x, 1f / parent.localScale.y, 1f / parent.localScale.z);
            foreach (var piece in Layout(index, enemyHeight))
            {
                var t = ArenaArt.Prim(PrimitiveType.Cube, shell, piece.LocalPos, piece.Scale, mats[piece.Mat], null, "Fragment", false, false);
                t.localRotation = piece.LocalRot;
            }
            return shell;
        }
    }
}
