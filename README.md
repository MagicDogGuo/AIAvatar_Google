# AI Avatar - 智慧語音虛擬角色

一個整合 Google AI 與 Meta 技術的 Unity 專案，打造具備自然對話、語音互動與精準嘴型同步的 AI 虛擬角色。

![Unity](https://img.shields.io/badge/Unity-6000.0-000000?logo=unity)
![License](https://img.shields.io/badge/License-MIT-blue)

---

## 專案概述

本專案在 Unity 中實現完整的語音對話流程：使用者透過麥克風說話 → 語音轉文字 (STT) → AI 理解並生成回覆 (Gemini) → 文字轉語音 (TTS) → 虛擬角色發聲並同步嘴型 (Lip Sync)。

---

## 技術架構 (Key Tech Stack)

| 模組 | 技術 | 說明 |
|------|------|------|
| **Intelligence** | Gemini API | 使用 Google Gemini 2.5 Flash 模型，實現自然語言對話與上下文理解 |
| **Communication** | Google STT & TTS | 整合 Google Cloud Speech-to-Text 語音辨識與 Text-to-Speech 語音合成，完成雙向語音互動 |
| **Realism** | Meta Person Lip Sync | 採用 Meta (Oculus) Lip Sync 技術，根據音訊驅動精準的嘴型動作 |

---

## 系統流程

```
使用者語音 (麥克風)
    ↓
Google Speech-to-Text (語音辨識)
    ↓
Gemini API (AI 對話生成)
    ↓
Google Text-to-Speech (語音合成)
    ↓
AudioSource 播放 + Meta Lip Sync (嘴型同步)
    ↓
虛擬角色自然發聲
```

---

## 環境需求

- **Unity**: 6000.0.58f2 (Unity 6) 或相容版本
- **作業系統**: Windows 10/11
- **必要權限**: 麥克風存取權限

---

## 依賴套件

- **Meta Person Loader** (`com.avatarsdk.metaperson.loader`) - [avatarsdk/metaperson-loader-unity](https://github.com/avatarsdk/metaperson-loader-unity)

- `com.unity.render-pipelines.universal` - URP 渲染管線
- Google Cloud API (需自行申請)

---

## 專案來源

本專案由 [Gemini-Unity-Google-Cloud](https://github.com/UnityGameStudio/Gemini-Unity-Google-Cloud)（基於 Ready Player Me 開發）修改而成：

- **原始專案**：使用 Ready Player Me 虛擬角色
- **本專案修改**：將角色替換為 **Meta Person** 人物，並整合 Meta Lip Sync 嘴型同步

### 參考資源

| 模組 | 來源 |
|------|------|
| **Gemini + STT 架構** | [UnityGameStudio/Gemini-Unity-Google-Cloud](https://github.com/UnityGameStudio/Gemini-Unity-Google-Cloud) |
| **TTS 串接** | [anomalisfree/Unity-Text-to-Speech-using-Google-Cloud](https://github.com/anomalisfree/Unity-Text-to-Speech-using-Google-Cloud) |
| **Meta Person Loader** | [avatarsdk/metaperson-loader-unity](https://github.com/avatarsdk/metaperson-loader-unity) |
| **Meta Person + Lip Sync 範例** | [avatarsdk/metaperson-oculus-unity-sample](https://github.com/avatarsdk/metaperson-oculus-unity-sample) |

---

## API 申請方法

### Google TTS & STT API（語音辨識與合成）

1. 前往 [Google Cloud Console - API 憑證](https://console.cloud.google.com/apis/credentials)
2. 建立或選擇專案
3. 啟用以下 API：
   - **Cloud Speech-to-Text API**（語音辨識）
   - **Cloud Text-to-Speech API**（語音合成）
4. 建立 **API 金鑰**：API 和服務 → 憑證 → 建立憑證 → API 金鑰
5. 將金鑰填入 `Assets/Resources/Config/ApiKeysConfig.asset` 的 `googleCloudApiKey` 欄位

### Gemini API（AI 對話）

1. 前往 [Google AI Studio - API 金鑰](https://aistudio.google.com/api-keys)
2. 登入 Google 帳號後，點選「建立 API 金鑰」
3. 將金鑰填入 `Assets/Resources/Config/ApiKeysConfig.asset` 的 `geminiApiKey` 欄位

---

## 快速開始

### 1. API 金鑰設定（集中管理）

所有 API 金鑰統一在 **`Assets/Resources/Config/ApiKeysConfig.asset`** 設定：

| 服務 | 用途 | 設定欄位 |
|------|------|----------|
| **Gemini API** | AI 對話 | `geminiApiKey` |
| **Google Cloud API** | STT + TTS 語音辨識與合成 | `googleCloudApiKey` |

- 首次開啟專案時，Editor 會自動建立 `ApiKeysConfig.asset`（若不存在）
- 此檔案已加入 `.gitignore`，**不會被提交到 Git**，可安心填入金鑰
- 若未自動建立，可透過選單 **Config → 建立 API 金鑰設定** 手動建立

> 若尚未申請 API，請參考上方 [API 申請方法](#api-申請方法)。

### 2. 開啟主場景

開啟 `Assets/Scenes/Meta+GoogleTTS+Germini.unity` 進行體驗。

### 3. 操作方式

- **按住 Space 鍵**：開始錄音
- **放開 Space 鍵**：結束錄音並送出至 Gemini，等待 AI 回覆與語音播放

---

## 專案結構

```
Assets/
├── Config/                  # API 金鑰集中設定
│   ├── ApiKeysConfig.cs         # ScriptableObject 定義
│   └── Editor/                  # 自動建立 config 的 Editor 腳本
├── GeminiManager/           # AI 對話與語音流程核心
│   ├── UnityAndGeminiV3.cs      # Gemini API 整合、對話邏輯、TTS 串接
│   ├── GoogleCloudSpeechToText.cs   # Google STT API 呼叫
│   ├── SpeechToTextManager.cs       # 麥克風錄音、STT 觸發
│   └── TextToSpeechManager.cs      # TTS 播放管理
├── GoogleTextToSpeech/      # Google Cloud TTS 套件
│   ├── Scripts/TextToSpeech.cs
│   └── Voices/              # 多語言語音設定 (zh-TW, en-US 等)
├── MetaLipAvator/           # Meta 嘴型同步
│   ├── AvatarSDK/MetaPerson/   # Meta Person 角色模型
│   └── Oculus/LipSync/         # OVRLipSync 嘴型驅動
├── Scenes/
│   └── Meta+GoogleTTS+Germini.unity   # 主場景
└── _Script/
    └── EyeLookAtCamera.cs   # 眼球跟隨攝影機
```

---

## 主要功能

- **多輪對話**：支援上下文記憶，可連續對話
- **多語言**：STT 支援 zh-TW、zh-CN、en-US 等；TTS 支援多國語音
- **TTS 文字淨化**：自動過濾 Markdown、emoji，確保語音朗讀順暢
- **嘴型同步**：TTS 音訊驅動 Meta Lip Sync，嘴型與發音一致

---

## 授權與參考

- **Meta Lip Sync**: Oculus Audio SDK License
- **Google APIs**: 依 Google Cloud 服務條款
- **Gemini API**: 依 Google AI 使用條款
- **原始專案**: [Gemini-Unity-Google-Cloud](https://github.com/UnityGameStudio/Gemini-Unity-Google-Cloud) (Ready Player Me)
- **TTS 套件**: [Unity-Text-to-Speech-using-Google-Cloud](https://github.com/anomalisfree/Unity-Text-to-Speech-using-Google-Cloud)
- **Meta Person 範例**: [avatarsdk/metaperson-oculus-unity-sample](https://github.com/avatarsdk/metaperson-oculus-unity-sample)（Meta Person + Oculus Lip Sync 官方範例）

---

## 版本資訊

- Unity 6 (6000.0.58f2)
- Gemini 2.5 Flash
- Meta Person Avatar SDK
