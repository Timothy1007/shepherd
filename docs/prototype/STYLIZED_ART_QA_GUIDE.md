# 《牧羊人》正式 3D 美術風格可行性測試（縫紉工作區）

> 2026-11-16。美術技術驗證，不是正式場景替換。沒有改服飾店、廚房、餐廳、移動、提燈、謎題，也沒有改專案的全域光照、材質或攝影機設定。

## 啟動與操作

1. Unity 2022.3.18f1（URP 14、Forward+）打開 `unity/`。
2. 選單 **Shepherd → Prototype → 正式美術風格 QA（縫紉工作區）**：開啟 `Assets/Exploration/Scenes/StylizedArtQA.unity` 並 Play。
3. 點一下 Game 畫面鎖定滑鼠。

| 按鍵 | 功能 |
|---|---|
| WASD／Shift／滑鼠 | 第一人稱走動（正式移動參數：步行 1.5、快走 2.4） |
| **T** | 暖光（午後窗光＋檯燈）／冷光（陰天藍光） |
| **M** | 材質：手繪風格 shader → URP Lit（同一張貼圖）→ 灰盒 |
| **G** | 線框疊圖（顯示真正的多邊形結構） |
| **1～6** | 正面／側面／背面／45 度（環繞視角：牆改成只投影）／第一人稱正常距離／第一人稱近距離 |
| Esc／按住 Alt／U | 放開滑鼠／點按鈕／隱藏標籤 |

重建場景：**Shepherd → Prototype → 正式美術風格 QA 重建場景**。自動截圖：`-executeMethod Shepherd.Exploration.Editor.StylizedArtQAEditor.CaptureFromCommandLine -captureDir <資料夾>`。

## 製作流程（實際使用的方法）

1. **建模（Blender 5.2，`tools/blender/stylized_qa/build.py`，全部程式化可重跑）**：用 bmesh 建立真正的立體零件：
   - 倒角方塊（單段倒角＝一條切面）
   - 錐形桌腳
   - 超橢圓斷面放樣：縫紉機機臂的弧線、椅背的彎板、後腳
   - 車削：手輪、線軸
   - 布料：交錯三角化的網格，越往下摺痕越深，下擺做成鋸齒，對應參考圖 A 披風的撕裂邊

   全部平面著色（flat shading），讓每個面成為一塊色面。沒有任何貼片、看板或圖片平面。
2. **貼圖**：每件資產先 Smart UV，再用 Cycles 的 Emission 烘焙，把下面幾層烘成一張手繪底色：
   - 固有色
   - 色塊筆觸（雜訊做色階化）
   - 上亮下暗的大漸層
   - 凹處混入偏冷的暗色（AO）
   - 邊緣提亮（Bevel 法線差）

   光照方向**不**烘進去。
3. **匯出**：FBX（套用變換、Unity 軸向），貼圖用 PNG。Blender 原始檔是 `tools/blender/stylized_qa/stylized_qa_assets.blend`（貼圖已打包）。
4. **Unity shader `Shepherd/PainterlyLit`**（`Assets/Exploration/StyleQA/Shaders`）：
   - 主光源的 N·L 分成暗／中／亮三段，段與段之間只留很窄的過渡。
   - 接收到的陰影一律落在暗段。
   - 暗面＝壓平過的環境光 × 偏冷偏紫的色調（不是單純變黑）。
   - 點光也分段。
   - 金屬用硬邊高光塊。
   - 沒有描邊。
5. **QA 場景**：`StylizedArtQAEditor` 負責擺放資產、建立材質、加上碰撞、燈光與 6 個觀察點。`StylizedArtQA` 負責走動和切換。

## 資產

| 資產 | 三角形 | 貼圖 |
|---|---|---|
| 縫紉工作桌：桌面、左側兩抽屜櫃、中間淺抽屜、黃銅把手、錐形腳、牙板、橫撐 | 1,140 | 2048 |
| 老式縫紉機：木底座、黑漆機床、立柱、弧形機臂、機頭、針桿／壓腳／針、輻條手輪、線軸、金色飾紋 | 1,500 | 1024 |
| 木椅：後仰一體後腳、兩條彎曲靠背板、外八前腳、H 形橫撐 | 700 | 1024 |
| 布料：桌邊垂布、玫瑰色布、椅背上的布、三色布疊 | 60～132 | 512～1024 |
| 線軸 × 5（其中一個倒下） | 960 | 512 |
| 展示房間：木地板、護牆板、窗框（環境用，不是正式場景） | 4,440 | 2048 |

## 截圖

[`docs/prototype/stylized_art_qa/`](stylized_art_qa/)：

- `unity_contact_sheet.jpg`：10 格總覽，包含暖光／冷光、正／側／背／45 度、第一人稱正常與近距離、線框，以及 URP Lit／灰盒對照。
- 三張第一人稱的單張截圖。

全部都是 Unity 實機的 Camera.Render 畫面。

## 與參考圖比較

**已達成**
- 真正的 3D 模型：四個方向和線框都成立，在 Unity 裡可以自由走動、轉角度。
- 大塊色面加切面的語言：桌面倒角、機臂的稜面、手輪的多邊形外緣、布料的三角切面和鋸齒下擺。
- 三段明暗。窗格的投影在桌面和地板形成清楚的光塊，最接近參考圖 B 的氛圍。
- 材質讀得出來：木頭（暖棕）、黑漆加金飾的金屬、黃銅高光、布料。
- 和灰盒對照，灰盒感已經明顯消失。
- 製作是程式化的，可以重跑、可以批量化：家具零件函式可以重複使用，烘焙和匯出一鍵完成。

**還有差距**
1. **貼圖不夠「畫」**：參考圖 A 的色面有刻意的筆觸方向與色溫變化。程式雜訊色階化只做到「斑駁」，近看像水漬（桌面最明顯），不像手繪筆觸。
2. **暗面還偏濁**：暖光下暗面接近黑棕，參考圖的暗面是有彩度的紫褐色。偏色和環境光需要逐場景再調。
3. **飽和度**：暖光照在橘棕木頭上會偏橘、偏亮。參考圖的亮面比較接近奶油色。
4. **造型設計感不足**：比例正確，但輪廓還是「寫實尺寸的家具」，缺少參考圖 A 那種刻意誇張的厚薄對比與不對稱。
5. **冷光**：冷光太暗，資產落在牆影裡時幾乎看不出材質。
6. **布料**：布料只有單面（shader 雙面渲染），近看邊緣很薄；摺痕是程式公式，不夠自然。
7. **小物件**：線軸、針板這類小物件在第一人稱距離下太小，細節看不出來。

**瓶頸在哪裡**
- **建模**：可行，不是瓶頸。硬表面家具用程式化 bmesh 很有效率。但「設計過的輪廓」需要美術先畫出造型稿或剪影，再照著建模。
- **貼圖**：這是**最大的瓶頸**。程式化烘焙可以做出底色和凹凸感，但做不出有方向、有意圖的筆觸。正式品質需要：
  - 在 Blender／Substance 裡手繪一層筆觸，或用美術畫的筆刷貼圖做投影；
  - 或者在烘焙時加入「沿著零件主軸方向的筆觸貼圖」（下一步可以先試這個）。
- **Shader**：主體可行。還缺：
  - 依物件調整的暗面色相
  - 明暗交界處的彩度提升
  - 冷暖光下自動調整環境光
- **工具能力**：Blender 加 Unity 的管線完整打通。限制在於我沒辦法真正「拿筆畫」，所以手繪層需要美術或筆刷素材庫（下載需要你同意）。

## 修改的檔案

- 新增 `tools/blender/stylized_qa/build.py`、`tools/blender/stylized_qa/stylized_qa_assets.blend`
- 新增 `unity/Assets/Exploration/StyleQA/`：
  - `Models/*.fbx`、`Models/SQA_StageFloorOnly.asset`
  - `Textures/*_Paint.png`
  - `Materials/*.mat`
  - `Shaders/ShepherdPainterlyLit.shader`、`Shaders/ShepherdStyleQAWire.shader`
- 新增 `unity/Assets/Exploration/Scripts/StyleQA/StylizedArtQA.cs`、`StyleQAWireTag.cs`
- 新增 `unity/Assets/Exploration/Editor/StylizedArtQAEditor.cs`
- 新增 `unity/Assets/Exploration/Scenes/StylizedArtQA.unity`
- 新增 `docs/prototype/STYLIZED_ART_QA_GUIDE.md`、`docs/prototype/stylized_art_qa/*.jpg`
