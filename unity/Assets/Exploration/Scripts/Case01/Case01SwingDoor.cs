using Shepherd.Battle.UI.Arena;
using UnityEngine;

namespace Shepherd.Exploration.Case01
{
    public enum Case01DoorState
    {
        Closed,
        Opening,
        Open,
        Closing,
        Locked,
    }

    /// <summary>
    /// Phase 1.8：真的門扇。門軸在門洞一側、繞軸轉動（不是隱藏門板）；門板有碰撞（擋人、擋視線、擋聲音），兩面都有門把與調查熱區。
    /// 開門時往「遠離玩家的那一側」轉（只允許單側的門例外，例如書房門只往平台側開，不碰到失序門的膜與虛空背板）；
    /// 轉動路徑上有家具或玩家就停在那裡；玩家站在關門路徑上不會關（不把人夾住、不困住）。
    /// 門的開關只是物理狀態；門通往哪裡由原本的 Portal／Flow 決定（兩者分開）。
    /// </summary>
    public sealed class Case01SwingDoor
    {
        public string Id;
        public string Name;
        /// <summary>兩側的房間（聲音阻隔用；同一房間兩側可相同）。</summary>
        public Case01Room RoomA, RoomB;
        public string KeyA, KeyB;
        public Case01DoorState State { get; private set; }
        /// <summary>目前角度（0＝關；正負＝往法線正向／反向轉開）。</summary>
        public float Angle { get; private set; }
        public float TargetAngle { get; private set; }
        public Transform Hinge { get; private set; }
        public BoxCollider Leaf { get; private set; }
        public Case01Interactable Hotspot { get; private set; }
        public AudioSource Audio { get; private set; }
        /// <summary>只允許往哪一側開：0＝兩側都可以；+1／-1＝只往法線的正／反向。</summary>
        public int OnlySide;
        public float OpenAngle = 92f;
        public float Speed = 150f;
        public string CreakClip = "door_creak";
        public string LockedText;
        /// <summary>門洞中心（地面）、牆的法線（水平）、沿牆方向。</summary>
        public Vector3 Center, Normal, Along;
        public float Width;
        public int LastBlocked { get; private set; }
        /// <summary>角度正負與「往法線哪一側」的換算（門軸在哪一端決定）。</summary>
        private float dirSign = 1f;
        /// <summary>目前門板轉向法線的哪一側（+1／-1；關著為 0）。</summary>
        public int SwingSide { get { return Mathf.Abs(Angle) < 0.5f ? 0 : (int)Mathf.Sign(Angle * dirSign); } }

        public bool IsOpenEnough { get { return Mathf.Abs(Angle) > 60f; } }
        public bool Closed { get { return State == Case01DoorState.Closed || State == Case01DoorState.Locked; } }

        /// <summary>建立門扇。hingeSide：門軸在沿牆方向的負（-1）或正（+1）端。</summary>
        public static Case01SwingDoor Build(Transform parent, string id, string name, Vector3 center, Vector3 normal, float width, int hingeSide, Material leafMat, Material knobMat, float inset = 0.04f, float leafH = 2.15f)
        {
            var d = new Case01SwingDoor { Id = id, Name = name, Center = center, Normal = normal.normalized, Width = width };
            d.Along = Vector3.Cross(Vector3.up, d.Normal).normalized;
            var hingePos = center + d.Along * (hingeSide * (width * 0.5f - inset));
            var hinge = ArenaArt.Group("Door_" + name, parent, hingePos);
            hinge.rotation = Quaternion.LookRotation(d.Normal, Vector3.up);
            d.Hinge = hinge;
            float leafW = width - 2f * inset;
            // 門板本地座標：x＝沿牆（從門軸往門洞中心＝-hingeSide）。
            float dir = -hingeSide;
            d.dirSign = dir;
            // Phase 1.9：門板高度與寬度依門洞（貼合，縫約 2 cm），不再一律縮小。
            var leaf = ArenaArt.Prim(PrimitiveType.Cube, hinge, new Vector3(dir * leafW * 0.5f, leafH * 0.5f + 0.01f, 0f), new Vector3(leafW, leafH, 0.045f), leafMat, null, "DoorLeaf", true, true);
            d.Leaf = leaf.GetComponent<BoxCollider>();
            foreach (var s in new[] { -1f, 1f })
            {
                ArenaArt.Prim(PrimitiveType.Cylinder, hinge, new Vector3(dir * (leafW - 0.07f), 1.0f, s * 0.045f), new Vector3(0.055f, 0.012f, 0.055f), knobMat, new Vector3(90f, 0f, 0f), "Knob", false, false);
                ArenaArt.Prim(PrimitiveType.Cube, hinge, new Vector3(dir * (leafW - 0.07f), 1.0f, s * 0.03f), new Vector3(0.06f, 0.16f, 0.01f), knobMat, null, "KnobPlate", false, false);
            }
            // 熱區：比門板厚，兩面都瞄得到；跟著門板轉。
            var hot = new GameObject("Interact_" + id);
            hot.transform.SetParent(hinge, false);
            hot.transform.localPosition = new Vector3(dir * leafW * 0.5f, 1.05f, 0f);
            var bc = hot.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(leafW, 1.9f, 0.32f);
            d.Hotspot = hot.AddComponent<Case01Interactable>();
            d.Hotspot.Id = id;
            d.Hotspot.Prompt = "開門";
            var au = new GameObject("DoorAudio");
            au.transform.SetParent(parent, false);
            au.transform.position = center + Vector3.up * 1.1f;
            d.Audio = au.AddComponent<AudioSource>();
            d.Audio.playOnAwake = false;
            d.Audio.spatialBlend = 1f;
            d.Audio.rolloffMode = AudioRolloffMode.Linear;
            d.Audio.minDistance = 1f;
            d.Audio.maxDistance = 14f;
            return d;
        }

        public void SetLocked(string text)
        {
            State = Case01DoorState.Locked;
            LockedText = text;
            Hotspot.Prompt = "門（鎖住）";
        }

        /// <summary>直接設定角度（初始半掩、QA「全部打開」）；angle 的正負＝往法線正／反向；路徑上有東西就停在最大可行角度。</summary>
        public void SetAngle(float angle, FirstPersonController player)
        {
            if (State == Case01DoorState.Locked) return;
            angle *= dirSign;
            float safe = 0f, step = Mathf.Sign(angle) * 4f;
            for (float a = step; Mathf.Abs(a) <= Mathf.Abs(angle) + 0.01f; a += step)
            {
                if (!Free(a, player)) break;
                safe = a;
            }
            Angle = safe;
            TargetAngle = safe;
            Apply();
            State = Mathf.Abs(Angle) < 0.5f ? Case01DoorState.Closed : Case01DoorState.Open;
            Hotspot.Prompt = State == Case01DoorState.Closed ? "開門" : "關門";
        }

        /// <summary>按 E：開或關。回傳要顯示的短文字（鎖住時）。</summary>
        public string Toggle(Vector3 playerPos, FirstPersonController player)
        {
            if (State == Case01DoorState.Locked)
            {
                Play("door_locked", 0.8f);
                return LockedText;
            }
            // 半掩的門按 E＝推開到底（沿原本的方向）；全開才是關門。
            bool ajar = State == Case01DoorState.Open && !IsOpenEnough;
            bool opening = State == Case01DoorState.Closed || State == Case01DoorState.Closing || ajar;
            if (opening)
            {
                int side = ajar ? SwingSide : OnlySide != 0 ? OnlySide : (Vector3.Dot(playerPos - Center, Normal) > 0f ? -1 : 1);
                TargetAngle = side * dirSign * OpenAngle;
                State = Case01DoorState.Opening;
                Play(CreakClip, 0.55f);
                Hotspot.Prompt = "關門";
            }
            else
            {
                TargetAngle = 0f;
                State = Case01DoorState.Closing;
                Hotspot.Prompt = "開門";
            }
            return null;
        }

        public void Tick(float dt, FirstPersonController player)
        {
            if (State != Case01DoorState.Opening && State != Case01DoorState.Closing) return;
            float next = Mathf.MoveTowards(Angle, TargetAngle, Speed * dt);
            if (!Free(next, player))
            {
                // 路徑上有人或家具：停在這裡（開的停在半開；關的不夾人，停住等玩家離開）。
                LastBlocked++;
                if (State == Case01DoorState.Opening)
                {
                    TargetAngle = Angle;
                    State = Mathf.Abs(Angle) < 0.5f ? Case01DoorState.Closed : Case01DoorState.Open;
                    if (State == Case01DoorState.Closed) Hotspot.Prompt = "開門";
                    Play("touch", 0.5f);
                }
                return;
            }
            Angle = next;
            Apply();
            if (Mathf.Abs(Angle - TargetAngle) < 0.01f)
            {
                if (State == Case01DoorState.Closing)
                {
                    State = Case01DoorState.Closed;
                    Play("door_close", 0.75f);
                }
                else State = Case01DoorState.Open;
            }
        }

        private void Apply()
        {
            Hinge.rotation = Quaternion.LookRotation(Normal, Vector3.up) * Quaternion.Euler(0f, -Angle, 0f);
        }

        /// <summary>門板在 angle 時會不會撞到玩家或其他碰撞體（不含自己、牆在門軸處的接縫）。</summary>
        public bool Free(float angle, FirstPersonController player)
        {
            var rot = Quaternion.LookRotation(Normal, Vector3.up) * Quaternion.Euler(0f, -angle, 0f);
            var local = Leaf.transform.localPosition;
            var center = Hinge.position + rot * local;
            var half = Vector3.Scale(Leaf.transform.localScale, Vector3.one * 0.5f) - new Vector3(0.06f, 0.06f, 0f);
            half.z = 0.015f;
            bool was = Leaf.enabled;
            Leaf.enabled = false;
            var hits = Physics.OverlapBox(center, half, rot, ~((1 << Case01HouseBuilder.OutdoorLayer) | (1 << Case01HouseBuilder.AnomalyLayer)), QueryTriggerInteraction.Ignore);
            Leaf.enabled = was;
            foreach (var h in hits)
            {
                if (h == null || !h.enabled) continue;
                // 門洞兩側的牆段只在門軸附近擦到（門板內縮 0.06 m 後不會），其餘都算阻擋。
                return false;
            }
            if (player != null)
            {
                // 玩家膠囊（CharacterController 也算在 OverlapBox 裡，這裡再保險檢查一次水平距離）。
                var p = player.transform.position;
                var toLeaf = Quaternion.Inverse(rot) * (p - center);
                if (Mathf.Abs(toLeaf.x) < half.x + FirstPersonController.Radius && Mathf.Abs(toLeaf.z) < half.z + FirstPersonController.Radius && toLeaf.y > -2.2f && toLeaf.y < 1.2f)
                    return false;
            }
            return true;
        }

        public void Play(string clip, float volume)
        {
            if (Audio == null || !Audio.enabled) return;
            var c = Case01Sfx.Get(clip);
            if (c != null) Audio.PlayOneShot(c, volume);
        }
    }
}
