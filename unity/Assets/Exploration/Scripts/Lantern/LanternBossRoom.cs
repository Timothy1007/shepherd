using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;
using Mode = Shepherd.Exploration.Lantern.LanternReveal.Mode;

namespace Shepherd.Exploration.Lantern
{
    /// <summary>
    /// 最終戰房間的電燈開關：射擊（或靠近按 E）就會打開，照亮一塊區域；那塊區域會恢復成真實的客廳。
    /// 最後的反撲只有站在燈光裡才會真的受傷；它的生命掉到一定程度時會把所有燈熄掉。
    /// </summary>
    public sealed class LanternSwitch : MonoBehaviour
    {
        public string Label = "電燈開關";
        public bool On { get; private set; }
        public Light[] Lights;
        public float OnIntensity = 5f;
        public Vector3 Zone;
        public float Radius = 5f;
        public int Slot;
        public Material IndicatorMat;
        public Light Glow;
        public Transform Nub;
        public int TimesSwitched { get; private set; }

        public void Set(bool on)
        {
            On = on;
            if (Lights != null) foreach (var l in Lights) if (l != null) { l.enabled = on; l.intensity = on ? OnIntensity : 0f; }
            if (Glow != null) Glow.enabled = !on;
            if (IndicatorMat != null)
            {
                var c = on ? new Color(2.4f, 1.8f, 1.0f) : new Color(2.0f, 0.3f, 0.2f);
                IndicatorMat.SetColor("_EmissionColor", c);
                IndicatorMat.SetColor("_AnomalyEmission", c);
            }
            if (Nub != null) Nub.localRotation = Quaternion.Euler(on ? -25f : 25f, 0f, 0f);
            LanternReveal.SetAnchor(Slot, Zone + Vector3.up * 1.4f, on ? Radius * 1.1f : 0f);
        }

        /// <summary>被射中或按 E：打開（已經開著就不變）。回傳是否剛打開。</summary>
        public bool Hit()
        {
            if (On) return false;
            Set(true);
            TimesSwitched++;
            return true;
        }

        /// <summary>這個位置有沒有被這盞燈照亮。</summary>
        public bool Covers(Vector3 p)
        {
            if (!On) return false;
            var d = p - Zone;
            d.y = 0f;
            return d.magnitude < Radius;
        }
    }

    public sealed partial class LanternGraybox
    {
        public static readonly Vector3 BossOrigin = new Vector3(100f, 0f, 0f);
        public const float BossSize = 18f, BossHeight = 6.5f;
        public Bounds BossBounds { get; private set; }
        public Transform BossRoomRoot { get; private set; }
        public Vector3 BossSpawn { get; private set; }
        public readonly List<LanternSwitch> Switches = new List<LanternSwitch>();
        public Transform BossPhoto { get; private set; }

        private Vector3 BP(float x, float y, float z) => BossOrigin + new Vector3(x, y, z);

        /// <summary>
        /// 她的記憶深處：放大、歪斜的客廳（灰盒＋CC0 材質）。牆有點傾斜、家具漂在半空；
        /// 四個角落各有一盞燈，牆上的開關射一下就亮，亮起的地方恢復成真實的客廳。
        /// </summary>
        public void BuildBossRoom()
        {
            if (BossRoomRoot != null) ArenaArt.Kill(BossRoomRoot.gameObject);
            Switches.Clear();
            HouseMaterials();
            BossRoomRoot = new GameObject("BossRoom").transform;
            BossRoomRoot.SetParent(Root, false);
            var saved = into;
            into = BossRoomRoot;
            try
            {
                float S = BossSize, Hh = BossHeight;
                var anomCold = new Color(0.55f, 0.66f, 0.72f);
                var ruin = new Color(0.55f, 0.58f, 0.62f);
                var floorM = PH("LB_Floor", "herringbone_parquet", "Tex", 4.2f, new Color(1f, 0.9f, 0.78f), anomCold * 0.7f, 0.8f);
                LanternReveal.AddRuin(floorM, "old_planks_02", 4.5f, ruin * 0.75f, 0.35f);
                var wallM = PH("LB_Wall", "painted_plaster_wall", "Tex", 5.5f, new Color(0.95f, 0.88f, 0.76f), anomCold, 0.6f, 0.2f, 0.03f);
                LanternReveal.AddRuin(wallM, "damaged_plaster", 5.5f, ruin, 0.35f);
                var ceilM = PH("LB_Ceiling", "plastered_wall_03", "Tex", 6f, new Color(0.95f, 0.94f, 0.9f), anomCold * 0.6f, 0.4f, 0.2f, 0.03f);
                LanternReveal.AddRuin(ceilM, "damaged_plaster", 6f, ruin * 0.6f, 0.3f);

                Box("BossFloor", BP(S / 2f, -0.1f, S / 2f), new Vector3(S + 1f, 0.2f, S + 1f), floorM);
                Box("BossCeiling", BP(S / 2f, Hh + 0.1f, S / 2f), new Vector3(S + 2f, 0.2f, S + 2f), ceilM, false, new Vector3(1.2f, 0f, -0.8f));
                // 歪斜的牆（只有外觀）；真正擋人的是看不見的直牆。
                Box("BossWall", BP(S / 2f, Hh / 2f, S + 0.1f), new Vector3(S + 2f, Hh + 1f, 0.3f), wallM, false, new Vector3(-2.5f, 1.5f, 0f));
                Box("BossWall", BP(S / 2f, Hh / 2f, -0.1f), new Vector3(S + 2f, Hh + 1f, 0.3f), wallM, false, new Vector3(2f, -1f, 0f));
                Box("BossWall", BP(-0.1f, Hh / 2f, S / 2f), new Vector3(0.3f, Hh + 1f, S + 2f), wallM, false, new Vector3(0f, 1.5f, 3f));
                Box("BossWall", BP(S + 0.1f, Hh / 2f, S / 2f), new Vector3(0.3f, Hh + 1f, S + 2f), wallM, false, new Vector3(0f, -2f, -2.5f));
                Invisible(BP(S / 2f, 4f, S + 0.2f), new Vector3(S + 2f, 8f, 0.3f));
                Invisible(BP(S / 2f, 4f, -0.2f), new Vector3(S + 2f, 8f, 0.3f));
                Invisible(BP(-0.2f, 4f, S / 2f), new Vector3(0.3f, 8f, S + 2f));
                Invisible(BP(S + 0.2f, 4f, S / 2f), new Vector3(0.3f, 8f, S + 2f));
                // 放大的踢腳板。
                Box("BossSkirting", BP(S / 2f, 0.25f, S - 0.12f), new Vector3(S, 0.5f, 0.12f), hTrim, false, new Vector3(-2.5f, 1.5f, 0f));
                Box("BossSkirting", BP(0.12f, 0.25f, S / 2f), new Vector3(0.12f, 0.5f, S), hTrim, false, new Vector3(0f, 1.5f, 3f));

                // 壁爐與巨大的全家福（北牆中央）。
                Box("BossChimney", BP(S / 2f, Hh / 2f, S - 0.6f), new Vector3(4.2f, Hh, 1.2f), wallM);
                Box("BossMantel", BP(S / 2f, 2.7f, S - 1.35f), new Vector3(4.8f, 0.22f, 0.5f), hWood, false);
                Box("BossHearth", BP(S / 2f, 0.06f, S - 1.6f), new Vector3(3.8f, 0.12f, 1.2f), hDark, false);
                Box("BossFirebox", BP(S / 2f, 1.1f, S - 1.19f), new Vector3(2.0f, 1.9f, 0.05f), hDark, false);
                var photoMat = Mat("LB_Photo", Mode.Dual, Color.white, Color.white, LanternReveal.Photo(true), LanternReveal.Photo(false), 0.4f, 0.4f, 0.004f);
                Box("BossPhotoFrame", BP(S / 2f, 4.3f, S - 1.24f), new Vector3(3.0f, 2.3f, 0.08f), hWood, false, new Vector3(0f, 0f, -4f));
                BossPhoto = Quad("BossPhoto", BP(S / 2f, 4.3f, S - 1.29f), new Vector3(2.7f, 2.0f, 1f), new Vector3(0f, 0f, -4f), photoMat);

                // 放大、漂浮、歪斜的家具。
                var sofa = Model("BossSofa", "Sofa_01", 0f, 0f, 0f, 1.9f, 0f, true, hFabric, new Vector3(2f, 1.9f, 4.5f));
                sofa.position = BP(3.4f, 0f, 9.5f);
                sofa.rotation = Quaternion.Euler(0f, 95f, 0f);
                var arm = Model("BossArmchair", "ArmChair_01", 0f, 0f, 0f, 2.1f, 0f, true, hLeather, new Vector3(1.8f, 2f, 1.8f));
                arm.position = BP(14.8f, 0f, 9.8f);
                arm.rotation = Quaternion.Euler(0f, -120f, 6f);
                int ci = 0;
                foreach (var p in new[] { new Vector4(6.5f, 3.6f, 6.5f, 35f), new Vector4(12.2f, 4.4f, 6.8f, -70f), new Vector4(9.6f, 5.0f, 12.5f, 160f) })
                {
                    var ch = Model("BossFloatingChair", "dining_chair_02", 0f, 0f, 0f, 1.5f, 0f, false, hWood, new Vector3(0.6f, 1.4f, 0.6f));
                    ch.position = BP(p.x, p.y, p.z);
                    ch.rotation = Quaternion.Euler(25f + ci * 30f, p.w, -20f + ci * 25f);
                    ci++;
                }
                // 漂在半空、通往哪裡都不是的門框。
                var door = new GameObject("BossFloatingDoor").transform;
                door.SetParent(Into, false);
                door.position = BP(14.5f, 2.6f, 15.6f);
                door.rotation = Quaternion.Euler(0f, -30f, 8f);
                Box("DoorTrim", door.TransformPoint(new Vector3(-0.75f, 0f, 0f)), new Vector3(0.14f, 3.0f, 0.2f), hTrim, false, door.eulerAngles, door);
                Box("DoorTrim", door.TransformPoint(new Vector3(0.75f, 0f, 0f)), new Vector3(0.14f, 3.0f, 0.2f), hTrim, false, door.eulerAngles, door);
                Box("DoorTrim", door.TransformPoint(new Vector3(0f, 1.5f, 0f)), new Vector3(1.64f, 0.14f, 0.2f), hTrim, false, door.eulerAngles, door);
                WallProp("BossPicture", "hanging_picture_frame_01", BP(0.25f, 3.6f, 6.5f), 1.6f, 90f, Mode.Dual).rotation *= Quaternion.Euler(0f, 0f, 9f);
                WallProp("BossClock", "wall_clock", BP(S - 0.25f, 4.4f, 8f), 1.1f, -90f, Mode.Dual);

                // 四盞燈與開關（角落）。暗處只看得到開關上的小紅燈。
                AddSwitch(0, "西側牆上的開關", new Vector3(4.5f, 0f, 13.5f), BP(0.32f, 1.45f, 13.5f), 90f, false);
                AddSwitch(1, "東側牆上的開關", new Vector3(13.5f, 0f, 13.5f), BP(S - 0.32f, 1.45f, 13.5f), -90f, false);
                AddSwitch(2, "桌燈", new Vector3(13.5f, 0f, 4.5f), BP(15.6f, 0f, 2.4f), 0f, true);
                AddSwitch(3, "南側牆上的開關", new Vector3(4.5f, 0f, 4.5f), BP(4.5f, 1.45f, 0.32f), 0f, false);

                BossBounds = new Bounds(BP(S / 2f, Hh / 2f, S / 2f), new Vector3(S, Hh, S));
                BossSpawn = BP(S / 2f, 0f, 11f);
                Mark("BossStart", BP(S / 2f, 0.05f, 1.8f), BP(S / 2f, 2.4f, 11f));
            }
            finally { into = saved; }
            foreach (var s in Switches) s.Set(false);
        }

        private void AddSwitch(int slot, string label, Vector3 zoneLocal, Vector3 at, float faceYaw, bool tableLamp)
        {
            var zone = BossOrigin + zoneLocal;
            var go = new GameObject("BossSwitch_" + slot);
            go.transform.SetParent(Into, false);
            var ind = Mat("LB_SwitchLed" + slot, Mode.Dual, new Color(0.9f, 0.85f, 0.75f), new Color(0.9f, 0.85f, 0.75f), null, null, 0.5f, 0.5f, 0f);
            var sw = go.AddComponent<LanternSwitch>();
            sw.Label = label;
            sw.Zone = zone;
            sw.Radius = 5.2f;
            sw.Slot = slot;
            sw.IndicatorMat = ind;
            var rot = Quaternion.Euler(0f, faceYaw, 0f);
            if (tableLamp)
            {
                // 桌燈：小桌子＋燈罩；射燈罩就會亮。
                go.transform.position = at;
                Box("LampTable", at + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.9f), hWood, true);
                Cyl("LampPole", at + Vector3.up * 1.2f, new Vector3(0.06f, 0.32f, 0.06f), hDark);
                Cyl("LampShade", at + Vector3.up * 1.62f, new Vector3(0.7f, 0.22f, 0.7f), ind);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 1.55f, 0f);
                col.size = new Vector3(0.9f, 0.7f, 0.9f);
                var l = NewLight("BossTableLamp", at + Vector3.up * 1.6f, 9f);
                sw.Lights = new[] { l, NewLight("BossTableFill", zone + Vector3.up * 4.8f, 7.5f) };
                sw.OnIntensity = 4.2f;
                sw.Glow = NewGlow(at + Vector3.up * 1.62f);
            }
            else
            {
                go.transform.position = at;
                go.transform.rotation = rot;
                var fwd = rot * Vector3.forward;
                Box("SwitchPlate", at, new Vector3(0.22f, 0.32f, 0.03f), hWhite, false, rot.eulerAngles);
                var nub = new GameObject("SwitchNub").transform;
                nub.SetParent(go.transform, false);
                nub.localPosition = new Vector3(0f, 0f, 0.03f);
                Box("Toggle", nub.position + fwd * 0.02f, new Vector3(0.05f, 0.1f, 0.04f), ind, false, rot.eulerAngles, nub);
                sw.Nub = nub;
                var col = go.AddComponent<BoxCollider>();
                col.size = new Vector3(0.6f, 0.7f, 0.25f);
                // 天花板吊燈（照亮這一角）。
                Ball("BossBulb" + slot, zone + Vector3.up * 5.3f, new Vector3(0.25f, 0.3f, 0.25f), ind);
                Cyl("BossShade" + slot, zone + Vector3.up * 5.55f, new Vector3(0.9f, 0.12f, 0.9f), hTrim);
                Cyl("BossCord" + slot, zone + Vector3.up * 6.05f, new Vector3(0.03f, 0.45f, 0.03f), hDark);
                sw.Lights = new[] { NewLight("BossLamp" + slot, zone + Vector3.up * 5.0f, 9.5f) };
                sw.OnIntensity = 5.5f;
                sw.Glow = NewGlow(at + fwd * 0.12f);
            }
            Switches.Add(sw);
        }

        private Light NewLight(string name, Vector3 at, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Into, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = WarmLamp;
            l.range = range;
            l.intensity = 0f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            l.enabled = false;
            return l;
        }

        /// <summary>開關的小紅光：暗處也找得到。</summary>
        private Light NewGlow(Vector3 at)
        {
            var go = new GameObject("SwitchGlow");
            go.transform.SetParent(Into, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.3f, 0.2f);
            l.range = 1.6f;
            l.intensity = 0.9f;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>這個位置有沒有被任何一盞燈照亮。</summary>
        public bool BossLit(Vector3 p)
        {
            foreach (var s in Switches) if (s != null && s.Covers(p)) return true;
            return false;
        }

        public int SwitchesOn
        {
            get { int n = 0; foreach (var s in Switches) if (s != null && s.On) n++; return n; }
        }

        /// <summary>所有燈熄掉（最後的反撲生命掉到門檻時）。</summary>
        public void BossBlackout()
        {
            foreach (var s in Switches) if (s != null) s.Set(false);
        }
    }
}
