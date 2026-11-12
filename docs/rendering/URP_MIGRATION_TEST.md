# URP 轉換驗證與外部素材候選清單（2026-11-12）

> 狀態：**2026-11-12 已正式切換到 URP**（使用者同意後，於正式專案執行 `Shepherd/Rendering/套用 URP 設定`）。以下是切換前在實驗副本的驗證紀錄。
> 正式切換時，使用者本機未提交的 `Assets/Exploration/Art/` 材質刻意**不轉換**。

## 1. 做了什麼

驗證在獨立副本 `S:\urp` 進行，`S:\hc`（Built-in 測試副本）與 repo 的專案設定都沒有切換。

- **安裝 URP。** 使用 Unity 2022.3.18 本機內建的 `com.unity.render-pipelines.universal` 14.0.10，不需要另外下載。
- **管線資產** `Assets/Settings/Rendering/ShepherdURP.asset`：
  - Forward+，沒有每個物件 8 盞燈的上限
  - HDR
  - 主光與附加光柔和陰影（陰影貼圖 4096）
  - 反射探針混合與盒投影
  - 燈光 cookie
  - SSAO（螢幕空間環境遮蔽，深度加法線）
- **色彩空間** 從 Gamma 改為 Linear。
- **材質轉換。** 33 個 Standard 材質資產自動轉成 URP Lit。39 個 Phase 1.x Triplanar 材質沿用原 shader，另外補了 URP 版 SubShader。
- **程式改成雙管線相容。** 新增 `ShepherdShading`，所有程式建立的材質（光滑度、金屬光滑度貼圖、法線、自發光、透明、鏤空）都透過它設定，同一份程式在 Built-in 和 URP 都正確。
- **新客廳 QA 的後製。** 在 URP 下改用只屬於該鏡頭的 Volume：ACES、曝光、分離色調、Bloom、暗角、顆粒，以及 SMAA 抗鋸齒。

## 2. 測試結果

| 副本 | EditMode | PlayMode |
|---|---|---|
| `S:\urp`（URP + Linear） | 416/416 | 19/19 |
| `S:\hc`（Built-in，套用雙管線程式後的回歸測試） | 416/416 | 19/19 |

為了讓測試不綁死某條管線的屬性名稱，修改了兩個測試：

- `Case01RebuildTests`：金屬光滑度貼圖的檢查改用 `ShepherdShading.HasMetallicGlossMap`。
- `Case01ResidentialV05Tests`：玻璃光滑度的檢查改用 `ShepherdShading.GetSmoothness`。

戰鬥數值、卡牌規則和 golden cases 都沒有改。

## 3. 肉眼檢查（URP 截圖，`urp_test/`）

- `living_builtin_vs_urp.jpg`：新客廳同機位對照。URP 下牆角、踢腳板、家具與地面交界有了接觸陰影；壁爐火光和燈泡有光暈；整體層次比較深。
- `lamps_off_builtin_vs_urp.jpg`、`flash_on_builtin_vs_urp.jpg`：關燈和手電筒都正常。
- `combat_urp.png`：3D 戰鬥場景、UI、裂隙特效正常，沒有洋紅色破圖。
- `phase19_urp.png`：Phase 1.9 舊住宅的 Triplanar 材質正常。
- 正式 Demo 的戰鬥截圖（21 張）、Phase 1.9 截圖（64 張）都已產生，也沒有破圖。

**已知待調整：**

- 壁爐架上的合照在房間裡看起來偏白（近看檢視正常），要調整燈光和材質。
- 改成 Linear 之後，所有場景的自發光和燈光強度都會略微變化，各場景需要逐一微調。
- 現有 Triplanar 的 URP SubShader 必須在專案已安裝 URP 時才能編譯，所以**正式切換必須一次完成**（裝套件、設定、shader 同一個 commit），不能先合併一半。

## 4. 建議

建議正式切換到 URP。這次驗證顯示轉換風險可控：測試全過，各場景沒有破圖。之後要做的體積光、SSAO、Bloom、光照烘焙，在 URP 上都有官方支援。

## 5. 外部素材候選清單（**尚未下載任何檔案**）

| # | 素材 | 來源 | 授權 | 用途 | 備註 |
|---|---|---|---|---|---|
| 1 | 掃描 PBR 材質（木地板、灰泥、油漆、壁紙、布料、地毯） | ambientCG（ambientcg.com） | CC0，可商用，不需標註 | 地板、牆面、布料，取代程序生成貼圖 | 每組材質 1K–8K，單組約 10–100 MB。只下載用得到的 |
| 2 | 掃描材質與家具模型（Wooden Chair 01、Gothic Cabinet 01、Classic Nightstand 01、Classic Console 01 等） | Poly Haven（polyhaven.com） | CC0 | 精緻家具、小物件 | Wooden Chair 01 頁面已確認 CC0（約 2 萬三角面）；其他件下載前逐頁確認 |
| 3 | Quixel Megascans 掃描素材（老化木材、剝落灰泥、灰塵、污漬貼花、雜物） | Fab（fab.com，需 Epic 帳號，由使用者登入取得） | Fab Standard License；2026-11 起整個 Megascans 庫免費 | 老化材質、貼花、雜物，最接近參考圖質感 | 第三方引擎（Unity）可使用，但必須包在遊戲內、不能單獨散布；**取得前要確認個別素材頁的授權** |
| 4 | 第一人稱全身角色加動畫 | Mixamo（mixamo.com，需免費 Adobe 帳號，由使用者登入下載） | 免版稅，可商用；不得單獨轉售或散布原檔 | 全身角色（低頭看得到身體）加走路、待機動畫 | 服務目前處於維護模式但仍可用。由使用者下載 FBX 給我匯入 |
| 5 | 第一人稱手部（戴手套） | itch.io「First Person Hands with Gloves」（rrfreelance） | 免費（自由付費）；**授權條款下載前要確認** | 平常畫面只露手的版本 | 約 248 MB。也可以用 #4 的全身角色直接做「只看到手」，不一定需要這項 |

我的建議是：

- **第一優先：#1、#2、#4。** 全部免費且授權清楚，可以解決材質、家具和第一人稱身體三個最大的缺口。
- **第二優先：#3。** 質感最好，但要由你用 Epic 帳號取得。

付費選項（不在這次範圍）：Unity Asset Store 的手電筒手部包（約 5–10 美元）、維多利亞室內素材包，品質更完整，但需要你購買。

## 來源

- [Poly Haven — Wooden Chair 01](https://polyhaven.com/a/WoodenChair_01)、[Gothic Cabinet 01](https://polyhaven.com/a/GothicCabinet_01)、[Classic Nightstand 01](https://polyhaven.com/a/ClassicNightstand_01)、[Classic Console 01](https://polyhaven.com/a/ClassicConsole_01)
- [ambientCG 授權說明（licenseorg）](https://licenseorg.com/guide/3d-assets/ambientcg)
- [CG Channel：Megascans 再度免費](https://www.cgchannel.com/?p=179945)、[Creative Bloq：Megascans 免費](https://creativebloq.com/news/the-worlds-biggest-library-of-scanned-assets-for-3d-artists-is-now-free-and-it-seems-that-this-time-its-for-good)、[Unreal 論壇：Megascans 授權](https://forums.unrealengine.com/t/megascans-license/2081542)
- [Adobe 社群：Mixamo 授權 FAQ](https://community.adobe.com/t5/mixamo-discussions/mixamo-faq-licensing-royalties-ownership-eula-and-tos/td-p/13234775)、[Mixamo 與替代方案（2026）](https://app.cinevva.com/guides/free-character-animations-rigging)
- [itch.io：First Person Hands with Gloves](https://rrfreelance.itch.io/first-person-hands-with-gloves/purchase)
