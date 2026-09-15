# Shepherd Unity Port

此資料夾保留給《牧羊人：荒野之歌》的正式 Unity 版本。

## 現階段原則

- Web 版繼續作為規則 Prototype、互動驗證與測試來源。
- Unity 版將採原生 C# / Unity UI，不直接內嵌目前網頁版。
- 在共用遊戲架構與神蹟／災難模組尚未穩定前，不開始大量逐卡移植。
- 現有 Web tests 將作為未來 Unity EditMode / PlayMode tests 的移植規格。
- 神蹟／災難美術之後更新時，Unity 素材將同步維護，但目前先不複製一份造成雙重來源。

## 預定結構

```text
unity/
├─ Assets/
│  ├─ Scripts/
│  │  ├─ Core/
│  │  ├─ Cards/
│  │  ├─ Effects/
│  │  ├─ AI/
│  │  └─ UI/
│  ├─ Art/
│  └─ Tests/
├─ ProjectSettings/        # 建立正式 Unity 專案後由 Unity 產生
└─ Packages/               # 建立正式 Unity 專案後由 Unity 產生
```

## 預定第一批 C# 核心

- GameState
- PlayerState
- CardDefinition / CardInstance
- GameAction
- GameEngine / ActionResolver
- LegalActionService
- Deck / Discard / PlayedArea / EffectZone
- Effect pipeline
- Target selection
- Pending choice
- Reaction timing
- Threshold prevention
- AI legal-action consumer
- InteractionState（Normal / SelectingTarget / SelectingCards / ResolvingReaction / InspectingCard）

目前只建立移植位置與規格，不開始 C# 實作。
