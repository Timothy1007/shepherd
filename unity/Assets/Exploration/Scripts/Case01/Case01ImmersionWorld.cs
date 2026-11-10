using System.Collections.Generic;
using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    /// <summary>
    /// Phase 1.8 沉浸層（只在 Phase 1.8 QA 的 R2「沉浸版」建立）：
    /// 真門（Case01SwingDoor）、逐房光影、3D 環境聲＋門的聲音阻隔、依地面材質的腳步聲、房間殘響、
    /// 三種非關鍵輕互動（檯燈、翻書、水龍頭）、第一人稱手勢與極輕微鏡頭步態、H2 的空間異變（走廊上的同一扇門改通往廚房）、
    /// 個人錨點痕跡原型。劇情、熱區 ID、Portal 路由都不改（門只管開關，通往哪裡仍由 Flow 決定）。
    /// </summary>
    public sealed class Case01ImmersionWorld
    {
        public sealed class Options
        {
            public bool NewLighting = true;
            public bool Anomaly = true;
            public bool AudioOn = true;
            public bool ReducedMotion;
            public bool LowFx;
            /// <summary>Phase 1.9：R3 動線（無內門）、貼合門板（縫 2 cm、高 2.18 m）、夾層／中庭／圖文重構。</summary>
            public bool Phase19;
        }

        public Case01Phase19Art P19 { get; private set; }
        public bool R3 { get { return Layout != null && Layout.Floorplan == Case01Floorplan.R3; } }
        public float DoorInset { get { return Opt.Phase19 ? 0.02f : 0.1f; } }
        public float DoorLeafH { get { return Opt.Phase19 ? 2.18f : 2.15f; } }

        public Options Opt { get; private set; }
        public Case01HouseLayout Layout { get; private set; }
        public Transform Root { get; private set; }
        public Case01ImmersiveArt Art { get; private set; }
        public readonly List<Case01SwingDoor> Doors = new List<Case01SwingDoor>();
        public readonly List<Light> AddedLights = new List<Light>();
        public Light FireLight { get; private set; }
        public Light DiningSpot { get; private set; }
        public Light LivingLamp { get; private set; }

        public Case01SwingDoor Door(string id)
        {
            foreach (var d in Doors) if (d.Id == id) return d;
            return null;
        }

        // ---------------- 建立 ----------------

        public static Case01ImmersionWorld Build(Case01HouseLayout L, Case01Palette p, Options opt)
        {
            if (L == null || (L.Floorplan != Case01Floorplan.R2 && L.Floorplan != Case01Floorplan.R3)) return null;
            var w = new Case01ImmersionWorld { Layout = L, Opt = opt ?? new Options(), palette = p };
            w.Root = ArenaArt.Group(L.Floorplan == Case01Floorplan.R3 ? "ImmersionR3" : "ImmersionR2", L.Root, Vector3.zero);
            w.Art = Case01ImmersiveArt.Build(L, p);
            w.BuildDoors();
            if (w.Opt.NewLighting) w.BuildLights();
            w.BuildAudio();
            w.BuildFx();
            w.BuildAnomaly();
            if (w.Opt.Phase19 && w.Art != null) w.P19 = Case01Phase19Art.Build(L, p, w.Art.Kit);
            Physics.SyncTransforms();
            return w;
        }

        private Case01Palette palette;

        private Material DoorMat()
        {
            var m = Art != null ? Art.Kit.Get("P1_WoodPaint") : null;
            return m != null ? m : palette.Get("wood_dark");
        }

        private void BuildDoors()
        {
            float y2 = Case01HouseBuilder.FloorHeight;
            var leaf = DoorMat();
            var knob = Art != null ? Art.Kit.Get("LR_Brass") : palette.Get("brass");
            System.Func<string, string, Vector3, Vector3, float, int, float, Case01SwingDoor> make = (id, name, c, n, w, hinge, inset) =>
            {
                // Phase 1.9：門板貼合門洞（兩側縫 2 cm、高 2.18 m，門洞 2.2 m）——舊的 0.1 m 內縮會留下明顯空隙、模型與碰撞對不上。
                if (Opt.Phase19) inset = DoorInset;
                float leafH = DoorLeafH;
                var d = Case01SwingDoor.Build(Root, id, name, c, n, w, hinge, leaf, knob, inset, leafH);
                // 門板換成既有的四片嵌板門模型（碰撞仍是門板方塊，模型縮放到與碰撞同尺寸）。
                if (Art != null)
                {
                    float leafW = w - 2f * inset;
                    Art.Kit.Skin(d.Leaf.transform, "PanelDoor", new Vector3(0f, -0.5f, 0f), 0f, new Vector3(leafW / 0.82f, leafH / 1.98f, 1f));
                }
                // 貼合的門板比較寬：開到 88° 就停（量測：書房門 92° 會碰到平台欄杆端柱），不靠撞到東西才停。
                if (Opt.Phase19) d.OpenAngle = 88f;
                Doors.Add(d);
                return d;
            };
            // R3：前門進來直接是玄關，不再有第二道內門。
            if (!R3)
            {
                var vest = make("door_vestibule", "Vestibule", new Vector3(Case01HouseBuilder.R17.FrontDoorX, 0f, Case01HouseBuilder.R17.R2VestibuleZ), Vector3.forward, 1.0f, -1, 0.1f);
                vest.KeyA = "vest";
                vest.KeyB = "hall";
            }
            var living = make("door_living", "Living", new Vector3(Case01HouseBuilder.R17.HallX, 0f, Case01HouseBuilder.R17.R2LivingDoorZ), Vector3.left, 1.0f, -1, 0.1f);
            living.KeyA = "hall";
            living.KeyB = "living";
            var ld = make("door_living_dining", "LivingDining", new Vector3(Case01HouseBuilder.R17.R2LivingDiningX, 0f, Case01HouseBuilder.R17.LivingZ), Vector3.forward, 1.1f, -1, 0.1f);
            ld.KeyA = "living";
            ld.KeyB = "dining";
            var hd = make("door_hall_dining", "HallDining", new Vector3(Case01HouseBuilder.R17.HallX, 0f, Case01HouseBuilder.R17.DiningHallDoorZ), Vector3.left, 1.0f, -1, 0.1f);
            hd.KeyA = "hall";
            hd.KeyB = "dining";
            hd.CreakClip = "door_creak_low";
            var kd = make("door_kitchen", "Kitchen", new Vector3(Case01HouseBuilder.R17.R2KitchenDoorX, 0f, Case01HouseBuilder.R17.ExtensionZ), Vector3.forward, 0.9f, -1, 0.06f);
            kd.KeyA = "dining";
            kd.KeyB = "kitchen";
            var study = make("door_study", "Study", new Vector3(Case01HouseBuilder.R17.HallX, y2, Case01HouseBuilder.R17.StudyDoorZ), Vector3.left, 1.1f, -1, 0.1f);
            study.KeyA = "landing";
            study.KeyB = "study";
            // 書房門只往平台側開：不碰到錯接時書房那側的失序門膜與虛空背板。
            study.OnlySide = -1;
            study.CreakClip = "door_creak_low";
            var bed = make("door_bedroom", "Bedroom", new Vector3(Case01HouseBuilder.R17.HallX, y2, Case01HouseBuilder.R17.BedroomDoorZ), Vector3.left, 1.1f, -1, 0.1f);
            bed.KeyA = "landing";
            bed.KeyB = "bedroom";
            ResetDoors();
        }

        /// <summary>正常初始：內門與餐廳門關著；客廳、廚房、臥室門半掩（看得到一點下一個房間）。</summary>
        public void ResetDoors()
        {
            foreach (var d in Doors)
            {
                if (d.State == Case01DoorState.Locked) continue;
                d.SetAngle(0f, null);
            }
            Door("door_living").SetAngle(28f, null);
            Door("door_kitchen").SetAngle(22f, null);
            Door("door_bedroom").SetAngle(30f, null);
            Physics.SyncTransforms();
        }

        /// <summary>QA 診斷：全部打開（往路徑上沒有障礙的一側）。</summary>
        public void OpenAllDoors()
        {
            foreach (var d in Doors)
            {
                d.SetAngle(d.OnlySide != 0 ? d.OnlySide * d.OpenAngle : d.OpenAngle, null);
                if (Mathf.Abs(d.Angle) < 30f) d.SetAngle(-d.OpenAngle, null);
            }
            Physics.SyncTransforms();
        }

        // ---------------- 光 ----------------

        private Light Point(Vector3 pos, Color c, float range, float intensity, bool shadows, bool lamp)
        {
            var l = ArenaArt.PointLight(Root, pos, c, range, intensity, shadows && !Opt.LowFx);
            AddedLights.Add(l);
            if (lamp) Layout.LampLights.Add(l);
            return l;
        }

        private Light Spot(Vector3 pos, Vector3 dir, Color c, float range, float intensity, float angle, bool shadows, bool lamp)
        {
            var go = new GameObject("SpotLight");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.spotAngle = angle;
            l.shadows = shadows && !Opt.LowFx ? LightShadows.Soft : LightShadows.None;
            AddedLights.Add(l);
            if (lamp) Layout.LampLights.Add(l);
            return l;
        }

        /// <summary>
        /// 逐房光影（H0 基準；H1～H3 由既有 Case01Mood 依這些基準失溫，H3 不回暖）：
        /// 吊燈全面調暗，主光改成有方向的局部光——玄關的門玻璃街燈與電話桌小燈、客廳的壁爐與檯燈、餐桌正上方的光圈、
        /// 廚房後窗的冷光與檯面下的暖光、樓梯上方後窗的一道窄光、書房門縫透出的燈光、書房綠罩桌燈、臥室床頭燈與月光。
        /// </summary>
        private void BuildLights()
        {
            var L = Layout;
            float y2 = Case01HouseBuilder.FloorHeight;
            System.Action<Case01Room, float> dim = (room, k) =>
            {
                if (L.RoomLightIntensity.ContainsKey(room)) L.RoomLightIntensity[room] *= k;
            };
            dim(Case01Room.Entrance, 0.65f);
            dim(Case01Room.Living, 0.45f);
            dim(Case01Room.Dining, 0.3f);
            dim(Case01Room.Kitchen, 0.8f);
            dim(Case01Room.Landing, 0.55f);
            dim(Case01Room.Study, 0.45f);
            dim(Case01Room.Bedroom, 0.4f);
            if (L.RoomLightColor.ContainsKey(Case01Room.Kitchen)) L.RoomLightColor[Case01Room.Kitchen] = new Color(0.86f, 0.92f, 1f);
            var warm = new Color(1f, 0.74f, 0.45f);
            var cool = new Color(0.66f, 0.74f, 0.96f);
            Point(new Vector3(9.55f, 1.85f, 4.0f), warm, 3.0f, 0.9f, false, true);
            Spot(new Vector3(Case01HouseBuilder.R17.FrontDoorX, 2.1f, 0.25f), new Vector3(0f, -0.45f, 1f), cool, 5f, 1.0f, 55f, false, false);
            FireLight = Point(new Vector3(0.85f, 0.35f, 2.9f), new Color(1f, 0.5f, 0.2f), 3.8f, 1.2f, true, true);
            foreach (var t in L.Root.Find("GroundFloor").GetComponentsInChildren<Transform>(true))
                if (t.name == "ReadingLamp")
                {
                    LivingLamp = t.GetComponentInChildren<Light>();
                    if (LivingLamp != null)
                    {
                        LivingLamp.intensity = 1.2f;
                        LivingLamp.range = 3.6f;
                    }
                }
            DiningSpot = Spot(new Vector3(3.0f, 2.65f, 9.0f), Vector3.down, new Color(1f, 0.72f, 0.42f), 4.2f, 2.6f, 80f, true, true);
            float back = Case01HouseBuilder.R17.Depth - 0.25f;
            Spot(new Vector3(2.4f, 2.3f, back), new Vector3(0f, -0.55f, -1f), new Color(0.7f, 0.8f, 1f), 6f, 0.8f, 70f, false, false);
            Spot(new Vector3(6.2f, 2.3f, back), new Vector3(0f, -0.55f, -1f), new Color(0.7f, 0.8f, 1f), 6f, 0.8f, 70f, false, false);
            Point(new Vector3(3.5f, 1.35f, back - 0.2f), new Color(1f, 0.8f, 0.55f), 2.0f, 0.6f, false, true);
            Spot(new Vector3(8.4f, y2 + 2.2f, Case01HouseBuilder.R17.ExtensionZ - 0.25f), new Vector3(0.1f, -0.65f, -1f), new Color(0.7f, 0.78f, 1f), 8f, 1.1f, 40f, false, false);
            // 書房門縫：書房裡靠門的暖光，平台側門縫下一條細細的亮線。
            float sz = Case01HouseBuilder.R17.StudyDoorZ;
            Point(new Vector3(5.95f, y2 + 0.4f, sz), warm, 1.8f, 0.7f, false, true);
            Point(new Vector3(6.75f, y2 + 0.08f, sz), warm, 1.2f, 0.25f, false, true);
            var strip = ArenaArt.Prim(PrimitiveType.Cube, Root, new Vector3(Case01HouseBuilder.R17.HallX + 0.03f, y2 + 0.004f, sz), new Vector3(0.05f, 0.004f, 0.85f),
                palette.Own(ArenaArt.Mat(new Color(1f, 0.8f, 0.5f), 0.1f, new Color(1.2f, 0.85f, 0.45f))), null, "StudyDoorGlow", false, false);
            StudyGlow = strip.gameObject;
            foreach (var t in L.Root.Find("LifeDetails").GetComponentsInChildren<Transform>(true))
                if (t.name == "DeskLamp")
                {
                    var l = t.GetComponentInChildren<Light>();
                    if (l != null)
                    {
                        l.intensity = 1.3f;
                        l.range = 3f;
                        l.color = new Color(0.92f, 1f, 0.78f);
                    }
                }
            Spot(new Vector3(3.0f, y2 + 1.7f, 0.3f), new Vector3(0f, -0.5f, 1f), new Color(0.6f, 0.7f, 0.95f), 5f, 0.6f, 60f, false, false);
        }

        public GameObject StudyGlow { get; private set; }

        // ---------------- 聲音 ----------------

        public sealed class Src
        {
            public AudioSource A;
            public AudioLowPassFilter Lp;
            public string Key, Name;
            public float Base;
            public int MinStage, MaxStage = 3;
            public bool AlwaysMuffled, Gate = true;
            public float Factor;
            public bool Muffled;
        }

        public readonly List<Src> Sources = new List<Src>();
        private AudioSource fx, steps;

        public Src Source(string name)
        {
            foreach (var s in Sources) if (s.Name == name) return s;
            return null;
        }

        private Src Loop(string name, string key, Vector3 pos, string clip, float vol, int minStage = 0, int maxStage = 3, bool muffled = false, float maxDist = 12f)
        {
            var go = new GameObject("Ambience_" + name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var a = go.AddComponent<AudioSource>();
            a.clip = Case01Sfx.Get(clip);
            a.loop = true;
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 1f;
            a.maxDistance = maxDist;
            a.volume = 0f;
            a.time = Random.Range(0f, Mathf.Max(0f, a.clip.length - 0.1f));
            var lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
            var s = new Src { A = a, Lp = lp, Key = key, Name = name, Base = vol, MinStage = minStage, MaxStage = maxStage, AlwaysMuffled = muffled };
            Sources.Add(s);
            if (Application.isPlaying) a.Play();
            return s;
        }

        private void Reverb(Vector3 pos, AudioReverbPreset preset, float min, float max)
        {
            var go = new GameObject("Reverb_" + preset);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var z = go.AddComponent<AudioReverbZone>();
            z.reverbPreset = preset;
            z.minDistance = min;
            z.maxDistance = max;
        }

        private void BuildAudio()
        {
            var L = Layout;
            float y2 = Case01HouseBuilder.FloorHeight;
            var fridge = Find("Fridge");
            var sink = Find("Sink");
            var desk = Find("Desk");
            Loop("street", "outside", new Vector3(Case01HouseBuilder.R17.FrontDoorX, 1.2f, -1.2f), "street_loop", 0.6f, 0, 3, false, 16f);
            Loop("radio", "hall", new Vector3(9.95f, 1.3f, 6.0f), "radio_loop", 0.2f, 0, 1, true, 6f);
            Loop("fire", "living", new Vector3(0.7f, 0.4f, 2.9f), "fire_loop", 0.5f, 0, 1, false, 8f);
            Loop("clock", "dining", new Vector3(0.45f, 1.1f, 9.4f), "clock_loop", 0.4f, 0, 1, false, 8f);
            Loop("fridge", "kitchen", (fridge != null ? fridge.position : new Vector3(4.75f, 0f, 12.4f)) + Vector3.up, "fridge_loop", 0.25f, 0, 3, false, 6f);
            water = Loop("water", "kitchen", (sink != null ? sink.position : new Vector3(2.4f, 0f, 17.7f)) + Vector3.up * 0.95f, "water_loop", 0.45f, 0, 3, false, 7f);
            water.Gate = false;
            Loop("wind", "landing", new Vector3(8.4f, y2 + 1.5f, Case01HouseBuilder.R17.ExtensionZ - 0.2f), "wind_loop", 0.35f, 0, 3, false, 9f);
            Loop("study_clock", "study", (desk != null ? desk.position : new Vector3(4f, y2, 11.4f)) + Vector3.up * 0.8f, "clock_loop", 0.25f, 0, 1, false, 6f);
            Loop("study_hum", "study", new Vector3(4f, y2 + 1.2f, 9f), "hum_loop", 0.4f, 2, 2, false, 8f);
            Loop("bedroom_street", "bedroom", new Vector3(4f, y2 + 1.4f, 0.3f), "street_loop", 0.2f, 0, 3, true, 6f);
            draft = Loop("draft", "corridor", new Vector3(5.8f, 1.2f, 10.2f), "draft_loop", 0.45f, 0, 3, false, 5f);
            draft.Gate = false;
            // 房間殘響：走廊（長、硬）、客廳（軟）、餐廳、廚房（磁磚）、樓梯與平台、書房、臥室。
            Reverb(new Vector3(8f, 1.4f, 6f), AudioReverbPreset.Hallway, 2.5f, 6f);
            Reverb(new Vector3(3.2f, 1.4f, 2.9f), AudioReverbPreset.Livingroom, 2.5f, 3.5f);
            Reverb(new Vector3(3.2f, 1.4f, 9f), AudioReverbPreset.Room, 2.5f, 3.5f);
            Reverb(new Vector3(5f, 1.4f, 15f), AudioReverbPreset.Bathroom, 3f, 4.5f);
            Reverb(new Vector3(8.2f, y2 + 1.4f, 6f), AudioReverbPreset.StoneCorridor, 2.5f, 6f);
            Reverb(new Vector3(4f, y2 + 1.4f, 8.8f), AudioReverbPreset.Livingroom, 2f, 3.2f);
            Reverb(new Vector3(4f, y2 + 1.4f, 2.8f), AudioReverbPreset.Room, 2f, 3.2f);
            var fxGo = new GameObject("FxAudio");
            fxGo.transform.SetParent(Root, false);
            fx = fxGo.AddComponent<AudioSource>();
            fx.playOnAwake = false;
            fx.spatialBlend = 1f;
            fx.rolloffMode = AudioRolloffMode.Linear;
            fx.maxDistance = 12f;
        }

        private Src water, draft;

        private Transform Find(string name)
        {
            foreach (var t in Layout.Root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>聲音的房間代號（門的兩側；前廊、H2 窄廊另外算）。</summary>
        public string KeyAt(Vector3 pos)
        {
            if (TopologyH2 && InCorridor(pos)) return "corridor";
            switch (Layout.RoomAt(pos))
            {
                case Case01Room.Entrance: return !R3 && pos.z < Case01HouseBuilder.R17.R2VestibuleZ ? "vest" : "hall";
                case Case01Room.Living: return "living";
                case Case01Room.Dining: return "dining";
                case Case01Room.Kitchen: return "kitchen";
                case Case01Room.Landing: return "landing";
                case Case01Room.Study: return "study";
                case Case01Room.Bedroom: return "bedroom";
                case Case01Room.Mezzanine:
                case Case01Room.Courtyard: return "anomaly";
                default: return "hall";
            }
        }

        private struct Edge
        {
            public string A, B;
            public Case01SwingDoor Door;
            public bool AlwaysClosed;
        }

        private List<Edge> Edges()
        {
            var e = new List<Edge>
            {
                new Edge { A = "vest", B = "hall", Door = Door("door_vestibule") },
                new Edge { A = "hall", B = "living", Door = Door("door_living") },
                new Edge { A = "living", B = "dining", Door = Door("door_living_dining") },
                new Edge { A = "hall", B = TopologyH2 ? "corridor" : "dining", Door = Door("door_hall_dining") },
                new Edge { A = TopologyH2 ? "corridor" : "dining", B = "kitchen", Door = Door("door_kitchen") },
                new Edge { A = "hall", B = "landing" },
                new Edge { A = "landing", B = "study", Door = Door("door_study") },
                new Edge { A = "landing", B = "bedroom", Door = Door("door_bedroom") },
                new Edge { A = "vest", B = "outside", AlwaysClosed = true },
                new Edge { A = "kitchen", B = "outside", AlwaysClosed = true },
                new Edge { A = "landing", B = "outside", AlwaysClosed = true },
                new Edge { A = "bedroom", B = "outside", AlwaysClosed = true },
            };
            return e;
        }

        /// <summary>從聽者所在房間到 key 的聲音傳遞：同房 1、每經過一個開口／開著的門 ×0.55、經過關著的門 ×0.18 並悶住（低通）。</summary>
        public float Propagation(string from, string to, out bool muffled)
        {
            muffled = false;
            if (from == to) return 1f;
            if (from == "anomaly" || to == "anomaly") return 0f;
            var best = new Dictionary<string, float> { { from, 1f } };
            var muff = new Dictionary<string, bool> { { from, false } };
            var edges = Edges();
            for (int iter = 0; iter < 8; iter++)
            {
                bool changed = false;
                foreach (var e in edges)
                {
                    foreach (var pair in new[] { new[] { e.A, e.B }, new[] { e.B, e.A } })
                    {
                        float f;
                        if (!best.TryGetValue(pair[0], out f)) continue;
                        bool closed = e.AlwaysClosed || (e.Door != null && e.Door.Closed);
                        bool half = e.Door != null && !closed && !e.Door.IsOpenEnough;
                        float k = closed ? 0.18f : half ? 0.35f : 0.55f;
                        float nf = f * k;
                        float old;
                        if (!best.TryGetValue(pair[1], out old) || nf > old + 1e-4f)
                        {
                            best[pair[1]] = nf;
                            muff[pair[1]] = muff[pair[0]] || closed;
                            changed = true;
                        }
                    }
                }
                if (!changed) break;
            }
            float r;
            if (!best.TryGetValue(to, out r)) return 0f;
            muffled = muff[to];
            return r;
        }

        // ---------------- 輕互動 ----------------

        public bool LampOn { get; private set; } = true;
        public bool BookOpen { get; private set; }
        public bool TapOn { get; private set; }
        private Transform bookCover, waterStream;

        private Case01Interactable Hot(string id, string prompt, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Interact_" + id);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var c = go.AddComponent<BoxCollider>();
            c.isTrigger = true;
            c.size = size;
            var it = go.AddComponent<Case01Interactable>();
            it.Id = id;
            it.Prompt = prompt;
            return it;
        }

        private void BuildFx()
        {
            if (LivingLamp != null || Find("ReadingLamp") != null)
            {
                var lamp = Find("ReadingLamp");
                Hot("fx_lamp", "檯燈開關", lamp.position + new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.5f, 0.45f));
                if (LivingLamp == null) LivingLamp = lamp.GetComponentInChildren<Light>();
            }
            // 茶几上一本雜誌：可以翻開／闔上（封面繞書背轉）。
            var table = Find("CoffeeTable");
            var bookPos = table != null ? table.position + new Vector3(0f, 0.465f, 0.4f) : new Vector3(2.4f, 0.465f, 3.3f);
            var book = ArenaArt.Group("FxBook", Root, bookPos);
            book.localRotation = Quaternion.Euler(0f, 12f, 0f);
            ArenaArt.Prim(PrimitiveType.Cube, book, new Vector3(0f, 0.008f, 0f), new Vector3(0.21f, 0.014f, 0.28f), palette.Get("paper"), null, "Pages", false, false);
            bookCover = ArenaArt.Group("CoverHinge", book, new Vector3(-0.105f, 0.017f, 0f));
            ArenaArt.Prim(PrimitiveType.Cube, bookCover, new Vector3(0.105f, 0f, 0f), new Vector3(0.215f, 0.004f, 0.285f), palette.Get("book_2"), null, "Cover", false, false);
            Hot("fx_book", "茶几上的雜誌", bookPos + Vector3.up * 0.06f, new Vector3(0.32f, 0.2f, 0.36f));
            // 廚房水龍頭：可以打開／關上（水流與水聲）。
            var sink = Find("Sink");
            if (sink != null)
            {
                var spout = sink.position + new Vector3(0f, 1.0f, -0.1f);
                waterStream = ArenaArt.Prim(PrimitiveType.Cylinder, Root, spout - new Vector3(0f, 0.05f, 0f), new Vector3(0.015f, 0.05f, 0.015f), palette.Own(ArenaArt.Mat(new Color(0.75f, 0.85f, 0.95f), 0.95f)), null, "WaterStream", false, false);
                waterStream.gameObject.SetActive(false);
                Hot("fx_tap", "水龍頭", sink.position + new Vector3(0f, 1.05f, -0.18f), new Vector3(0.35f, 0.3f, 0.3f));
            }
        }

        /// <summary>QA 的互動入口（Case01HouseQA.CustomInteract）：門與輕互動由這裡處理（回傳 true）；其他物件只加聲音與手勢，劇情照舊。</summary>
        public bool Interact(Case01Interactable it, Case01HouseQA qa)
        {
            Reach();
            var id = it.Id;
            if (id.StartsWith("door_"))
            {
                var d = Door(id);
                if (d == null) return false;
                var note = d.Toggle(qa.Player.transform.position, qa.Player);
                if (note != null) qa.Hud.ShowNote(note);
                return true;
            }
            switch (id)
            {
                case "fx_lamp":
                    LampOn = !LampOn;
                    if (LivingLamp != null) LivingLamp.enabled = LampOn;
                    OneShot("switch", it.transform.position, 0.8f);
                    return true;
                case "fx_book":
                    BookOpen = !BookOpen;
                    if (bookCover != null) bookCover.localRotation = Quaternion.Euler(0f, 0f, BookOpen ? 172f : 0f);
                    OneShot("page", it.transform.position, 0.9f);
                    if (BookOpen) qa.Hud.ShowNote("園藝雜誌，摺角停在玫瑰那一頁。");
                    return true;
                case "fx_tap":
                    TapOn = !TapOn;
                    if (waterStream != null) waterStream.gameObject.SetActive(TapOn);
                    water.Gate = TapOn;
                    OneShot("touch", it.transform.position, 0.8f);
                    return true;
                case Case01Ids.StorageDoor:
                case "qa_obs_backdoor":
                    OneShot("door_locked", it.transform.position, 0.8f);
                    return false;
                default:
                    OneShot(it.Id.StartsWith("qa_obs") ? "touch" : "page", it.transform.position, 0.6f);
                    return false;
            }
        }

        public int OneShots { get; private set; }

        private void OneShot(string clip, Vector3 pos, float vol)
        {
            OneShots++;
            if (fx == null || !Opt.AudioOn) return;
            fx.transform.position = pos;
            fx.PlayOneShot(Case01Sfx.Get(clip), vol);
        }

        // ---------------- 空間異變（H2） ----------------

        private Transform corridor;
        private Case01RoomVolume corridorVolume;
        private bool forceTopology = true;
        public bool TopologyH2 { get; private set; }
        public int TopologySwaps { get; private set; }

        private void BuildAnomaly()
        {
            corridor = Case01HouseBuilder.BuildR2AnomalyCorridor(Layout);
            // 窄廊：裸灰泥、冷光、穿堂風；牆上一個空白相框（和客廳那個 H1 變空白的相框同款）。
            ArenaArt.PointLight(corridor, new Vector3(5.8f, 2.3f, 10.4f), new Color(0.62f, 0.7f, 0.9f), 4.5f, 0.9f, false);
            var frame = ArenaArt.Group("BlankFrame", corridor, new Vector3(Case01HouseBuilder.AnomalyCorridorX + 0.085f, 1.6f, 10.6f));
            frame.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ArenaArt.Prim(PrimitiveType.Cube, frame, Vector3.zero, new Vector3(0.62f, 0.46f, 0.04f), palette.Get("frame_wood"), null, "Frame", false, false);
            ArenaArt.Prim(PrimitiveType.Cube, frame, new Vector3(0f, 0f, 0.022f), new Vector3(0.54f, 0.38f, 0.005f), palette.Get("fabric_cream"), null, "Blank", false, false);
            var b = new Bounds();
            b.SetMinMax(new Vector3(Case01HouseBuilder.AnomalyCorridorX, -1f, Case01HouseBuilder.AnomalyCorridorZ0), new Vector3(Case01HouseBuilder.R17.HallX, 2.75f, Case01HouseBuilder.R17.ExtensionZ));
            corridorVolume = new Case01RoomVolume { Room = Case01Room.Entrance, Bounds = b };
            corridor.gameObject.SetActive(false);
        }

        public bool InCorridor(Vector3 p)
        {
            return p.x > Case01HouseBuilder.AnomalyCorridorX && p.x < Case01HouseBuilder.R17.HallX && p.z > Case01HouseBuilder.AnomalyCorridorZ0 && p.z < Case01HouseBuilder.R17.ExtensionZ && p.y < 2.75f;
        }

        /// <summary>只在玩家看不到一樓這一帶時才換（在二樓或異常空間）；建置後第一個影格直接套用。</summary>
        private void UpdateTopology(Case01HouseQA qa)
        {
            bool want = Opt.Anomaly && qa.Flow != null && qa.Flow.WrongConnectionActive && !qa.Flow.CoreStabilized;
            if (want == TopologyH2) { forceTopology = false; return; }
            var room = qa.CurrentRoom;
            bool hidden = room == Case01Room.Landing || room == Case01Room.Study || room == Case01Room.Bedroom || room == Case01Room.Storage ||
                          room == Case01Room.Mezzanine || room == Case01Room.Courtyard;
            if (!hidden && !forceTopology) return;
            forceTopology = false;
            SetTopology(want, !hidden ? false : true);
        }

        /// <summary>直接切換拓樸（QA／測試）。settle＝播放遠處的沉降聲。</summary>
        public void SetTopology(bool h2, bool settle)
        {
            if (TopologyH2 == h2) return;
            TopologyH2 = h2;
            corridor.gameObject.SetActive(h2);
            Layout.Rooms.Remove(corridorVolume);
            if (h2) Layout.Rooms.Insert(0, corridorVolume);
            TopologySwaps++;
            Physics.SyncTransforms();
            if (settle) OneShot("settle", Door("door_hall_dining").Center + Vector3.up, 0.9f);
        }

        // ---------------- 每影格 ----------------

        private Camera cam;
        private Transform hand;
        private float handT = -1f, bobPhase;
        private Vector3 lastStepPos;
        private bool stepInit;
        private float stepAcc;
        public string LastStepSurface { get; private set; }
        public int StepCount { get; private set; }
        private LineRenderer trace;
        private Vector3 lastTrace;
        public bool AnchorOn { get; private set; }
        public int TracePoints { get { return trace != null ? trace.positionCount : 0; } }

        /// <summary>接到探索相機：第一人稱手、腳步聲來源。</summary>
        public void Attach(Camera camera)
        {
            cam = camera;
            if (cam == null) return;
            hand = ArenaArt.Group("FirstPersonHand", cam.transform, new Vector3(0.24f, -0.34f, 0.42f));
            var skin = palette.Own(ArenaArt.Mat(new Color(0.86f, 0.68f, 0.56f), 0.2f));
            var sleeve = palette.Own(ArenaArt.Mat(new Color(0.18f, 0.2f, 0.24f), 0.15f));
            ArenaArt.Prim(PrimitiveType.Cylinder, hand, new Vector3(0f, -0.02f, -0.12f), new Vector3(0.075f, 0.13f, 0.075f), sleeve, new Vector3(90f, 0f, 0f), "Sleeve", false, false);
            ArenaArt.Prim(PrimitiveType.Capsule, hand, new Vector3(0f, 0f, 0.05f), new Vector3(0.07f, 0.06f, 0.1f), skin, new Vector3(90f, 0f, 0f), "Palm", false, false);
            hand.gameObject.SetActive(false);
            var sGo = new GameObject("Footsteps");
            sGo.transform.SetParent(Root, false);
            steps = sGo.AddComponent<AudioSource>();
            steps.playOnAwake = false;
            steps.spatialBlend = 0f;
        }

        public void Dispose()
        {
            if (hand != null) ArenaArt.Kill(hand.gameObject);
            hand = null;
        }

        private void Reach()
        {
            if (Opt.ReducedMotion || hand == null) return;
            handT = 0f;
        }

        public bool HandVisible { get { return hand != null && hand.gameObject.activeSelf; } }

        public void ToggleAnchor(Vector3 feet)
        {
            AnchorOn = !AnchorOn;
            if (trace == null)
            {
                var go = new GameObject("AnchorTrace");
                go.transform.SetParent(Root, false);
                trace = go.AddComponent<LineRenderer>();
                trace.widthMultiplier = 0.05f;
                trace.material = palette.Own(ArenaArt.Mat(new Color(0.5f, 0.9f, 1f), 0.1f, new Color(0.35f, 0.75f, 0.95f)));
                trace.useWorldSpace = true;
                trace.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            trace.positionCount = 0;
            if (AnchorOn) AddTrace(feet);
        }

        private void AddTrace(Vector3 feet)
        {
            var p = feet + Vector3.up * 0.06f;
            trace.positionCount++;
            trace.SetPosition(trace.positionCount - 1, p);
            lastTrace = feet;
        }

        public void Tick(float dt, Case01HouseQA qa)
        {
            var player = qa.Player;
            foreach (var d in Doors) d.Tick(dt, player);
            UpdateTopology(qa);
            if (P19 != null) P19.Tick(qa);
            if (water != null && waterStream != null) waterStream.gameObject.SetActive(TapOn);
            int stage = qa.HouseStage;
            // 聲音：依房間、門的開關、H 階段。
            string here = player != null ? KeyAt(player.transform.position) : "hall";
            foreach (var s in Sources)
            {
                bool muffled;
                float f = Propagation(here, s.Key, out muffled);
                bool stageOk = stage >= s.MinStage && stage <= s.MaxStage;
                bool gate = s == draft ? TopologyH2 : s.Gate;
                s.Factor = f;
                s.Muffled = muffled || s.AlwaysMuffled;
                float v = Opt.AudioOn && stageOk && gate ? s.Base * f : 0f;
                s.A.volume = Mathf.MoveTowards(s.A.volume, v, dt * 1.5f);
                if (dt <= 0f) s.A.volume = v;
                s.Lp.cutoffFrequency = s.Muffled ? 900f : 22000f;
                // 建置時宿主物件還沒啟用（Unity 不讓停用中的 AudioSource 播放）：第一個影格再開始循環播放。
                if (Application.isPlaying && !s.A.isPlaying && s.A.isActiveAndEnabled) s.A.Play();
            }
            // 壁爐火光只在 H0 跳動。
            if (FireLight != null && stage == 0) FireLight.intensity *= 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3.1f, 0.7f);
            if (StudyGlow != null) StudyGlow.SetActive(stage < 2 && Door("door_study").Closed);
            // 腳步。
            if (player != null && qa.Mode == Case01Mode.Exploring)
            {
                var pos = player.transform.position;
                if (!stepInit)
                {
                    lastStepPos = pos;
                    stepInit = true;
                }
                float moved = new Vector2(pos.x - lastStepPos.x, pos.z - lastStepPos.z).magnitude;
                lastStepPos = pos;
                if (moved < 1f) stepAcc += moved;
                float stride = player.LastHorizontalSpeed > player.WalkSpeedValue + 0.3f ? 0.95f : 0.72f;
                if (stepAcc >= stride)
                {
                    stepAcc = 0f;
                    StepCount++;
                    LastStepSurface = Surface(pos);
                    if (Opt.AudioOn && steps != null)
                    {
                        steps.PlayOneShot(Case01Sfx.Get("step_" + LastStepSurface), 0.4f);
                        if (LastStepSurface == "stair" && Random.value < 0.2f) steps.PlayOneShot(Case01Sfx.Get("stair_creak"), 0.35f);
                    }
                }
                if (AnchorOn && trace != null && (pos - lastTrace).magnitude > 0.4f && trace.positionCount < 600) AddTrace(pos);
            }
            // 手勢：伸手 → 收回（約 0.4 秒）。
            if (hand != null)
            {
                if (handT >= 0f)
                {
                    handT += dt;
                    float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(handT / 0.4f));
                    hand.gameObject.SetActive(handT < 0.4f);
                    hand.localPosition = Vector3.Lerp(new Vector3(0.24f, -0.34f, 0.42f), new Vector3(0.16f, -0.2f, 0.62f), k);
                    if (handT >= 0.4f) handT = -1f;
                }
                else if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false);
            }
        }

        /// <summary>腳步的地面材質：樓梯、地毯／長毯、磁磚（玄關與廚房）、木地板。</summary>
        public string Surface(Vector3 feet)
        {
            bool up = feet.y > 2.5f;
            if (feet.x > Case01HouseBuilder.R17.StairX0 - 0.05f && feet.z > Case01HouseBuilder.R17.StairTopZ && feet.z < Case01HouseBuilder.R17.StairFootZ + 0.1f && feet.y > 0.08f && feet.y < 2.95f) return "stair";
            if (Art != null)
                foreach (var r in up ? Art.RugsUp : Art.Rugs)
                    if (r.Contains(new Vector2(feet.x, feet.z))) return "rug";
            if (TopologyH2 && InCorridor(feet)) return "wood";
            var room = Layout.RoomAt(feet);
            return room == Case01Room.Entrance || room == Case01Room.Kitchen ? "tile" : "wood";
        }

        /// <summary>LateUpdate：鏡頭的極輕微步態（可關）與新光影下較暗的環境光。</summary>
        public void LateCamera(Camera camera, FirstPersonController player, bool exploring)
        {
            if (camera == null || player == null) return;
            if (!Opt.ReducedMotion && exploring && player.LastHorizontalSpeed > 0.3f)
            {
                bobPhase += Time.deltaTime * player.LastHorizontalSpeed * 2.6f;
                camera.transform.position += new Vector3(0f, Mathf.Sin(bobPhase * 2f) * 0.012f, 0f) + camera.transform.right * (Mathf.Sin(bobPhase) * 0.006f);
            }
            if (Opt.NewLighting && Application.isPlaying)
            {
                RenderSettings.ambientSkyColor *= 0.8f;
                RenderSettings.ambientEquatorColor *= 0.8f;
                RenderSettings.ambientGroundColor *= 0.8f;
            }
        }
    }
}
