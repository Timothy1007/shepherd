using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 失序體的找路（原型）：執行時用碰撞體烤一張 NavMesh（包含樓梯坡道），失序體沿著路徑點走，不會卡在家具或牆角。
    /// 只用 NavMesh.CalculatePath 查詢（編輯器模式的測試、截圖也能用），不使用 NavMeshAgent。
    /// 會被牽引、丟出的東西（有剛體）、玩家、失序體不算障礙物。
    /// </summary>
    public static class LanternNav
    {
        private static readonly Dictionary<string, NavMeshDataInstance> instances = new Dictionary<string, NavMeshDataInstance>();
        private static readonly NavMeshPath scratch = new NavMeshPath();

        public static bool Has(string key) => instances.ContainsKey(key);
        public static int Count => instances.Count;

        public static NavMeshBuildSettings Settings
        {
            get
            {
                var s = NavMesh.GetSettingsByID(0);
                s.agentRadius = 0.3f;
                s.agentHeight = 1.8f;
                s.agentSlope = 42f;
                s.agentClimb = 0.3f;
                return s;
            }
        }

        /// <summary>重新烤一個範圍（同名的舊資料會先移除）。</summary>
        public static bool Build(string key, Bounds bounds)
        {
            Remove(key);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~(1 << LanternRig.ViewLayer), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s =>
            {
                var c = s.component as Collider;
                if (c == null) return false;
                return c.isTrigger || c.attachedRigidbody != null || c is CharacterController
                    || c.GetComponentInParent<LanternToggle>() != null || c.GetComponentInParent<LanternEnemy>() != null || c.GetComponentInParent<LanternBoss>() != null || c.GetComponentInParent<LanternPhrase>() != null;
            });
            var data = NavMeshBuilder.BuildNavMeshData(Settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return false;
            data.name = "LanternNav_" + key;
            var inst = NavMesh.AddNavMeshData(data);
            if (!inst.valid) return false;
            instances[key] = inst;
            return true;
        }

        public static void Remove(string key)
        {
            NavMeshDataInstance inst;
            if (instances.TryGetValue(key, out inst)) { inst.Remove(); instances.Remove(key); }
        }

        public static void Clear()
        {
            foreach (var kv in instances) kv.Value.Remove();
            instances.Clear();
        }

        public static bool Sample(Vector3 p, float range, out Vector3 onMesh)
        {
            NavMeshHit hit;
            if (instances.Count > 0 && NavMesh.SamplePosition(p, out hit, range, NavMesh.AllAreas)) { onMesh = hit.position; return true; }
            onMesh = p;
            return false;
        }

        /// <summary>完整的路徑（找不到或不完整回傳 false）。corners 會被清空後填入。</summary>
        public static bool Path(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            corners.Clear();
            Vector3 a, b;
            if (!Sample(from, 1.2f, out a) || !Sample(to, 1.6f, out b)) return false;
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, scratch) || scratch.status != NavMeshPathStatus.PathComplete) return false;
            corners.AddRange(scratch.corners);
            return corners.Count > 0;
        }

        public static float Length(List<Vector3> corners)
        {
            float d = 0f;
            for (int i = 1; i < corners.Count; i++) d += Vector3.Distance(corners[i - 1], corners[i]);
            return d;
        }
    }
}
