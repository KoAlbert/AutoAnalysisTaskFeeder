# AutoAnalysisTaskFeeder (WPF)

自動化解析實驗資料並啟動分析程式的 WPF 應用程式

## 專案概述

此應用程式用於：
1. 掃描實驗資料夾並提取相關資訊
2. 根據資訊產生分析程式所需的 INI 配置檔案
3. 自動啟動外部分析程式（QKBqPCRAnalysis.exe）
4. 監控分析進度並報告結果

## 技術堆疊

- **框架**：.NET 8.0 + WPF
- **設計模式**：MVVM（Model-View-ViewModel）
- **相依性注入**：服務介面模式
- **測試框架**：xUnit

## 專案結構

```
AutoAnalysisTaskFeeder/
├── Models/
│   ├── TaskItem.cs          - 任務資料模型
│   └── TaskStatus.cs        - 任務狀態列舉
├── ViewModels/
│   ├── ViewModelBase.cs     - ViewModel 基類
│   └── MainViewModel.cs     - 主 ViewModel
├── Views/
│   ├── MainWindow.xaml      - 主視窗 XAML
│   └── MainWindow.xaml.cs   - 主視窗後端程式碼
├── Services/
│   ├── ServiceInterfaces.cs - 服務介面定義
│   ├── LogService.cs        - 日誌服務
│   ├── IniService.cs        - INI 檔案服務
│   ├── FolderScanService.cs - 資料夾掃描服務
│   └── ProcessRunner.cs     - 進程執行服務
├── Utilities/
│   └── RelayCommand.cs      - 命令實現
├── App.xaml                 - 應用程式資源
├── App.xaml.cs              - 應用程式啟動
├── Program.cs               - 入口點
└── AutoAnalysisTaskFeeder.csproj - 專案檔

AutoAnalysisTaskFeeder.Tests/
├── ServiceTests.cs          - 服務單元測試
└── AutoAnalysisTaskFeeder.Tests.csproj
```

## 編譯與執行

### 前提條件

- Visual Studio 2022 或更新版本
- .NET 8.0 SDK

### 編譯

```bash
dotnet build AutoAnalysisTaskFeeder.sln
```

### 執行

```bash
dotnet run --project AutoAnalysisTaskFeeder\AutoAnalysisTaskFeeder.csproj
```

### 執行測試

```bash
dotnet test AutoAnalysisTaskFeeder.Tests
```

## 主要功能

### 1. 資料夾掃描
- 支援多個資料夾同時掃描
- 自動解析 JSON 和 INI 檔案
- 提取機器代碼、應用名稱、軟體版本、循環數、晶片數、篩選器等資訊

### 2. INI 檔案產生
- 根據任務資訊產生標準格式的 `NewAnalysis.ini` 檔案
- 支援篩選器字串規範化
- UTF-8 編碼

### 3. 分析程式控制
- 自動啟動 QKBqPCRAnalysis.exe
- 監控分析完成狀態
- 支援超時檢測（15 分鐘）
- 自動終止進程

### 4. 進度監控
- 實時顯示掃描進度
- 產生 INI 進度跟蹤
- 分析進度條
- 詳細的執行日誌

## API 參考

### MainViewModel 命令

| 命令 | 說明 |
|------|------|
| SelectFolderCommand | 選取實驗資料夾 |
| GenerateIniCommand | 產生 INI 檔案 |
| StartAnalysisCommand | 啟動分析程式 |
| SelectAnalysisTaskPathCommand | 選取分析任務路徑 |
| SelectPcrAnalysisPathCommand | 選取 PCR 分析程式 |

### 服務介面

#### IFolderScanService
- `ScanFoldersAsync(folderPaths, onProgress, cancellationToken)` - 掃描資料夾

#### IIniService
- `GenerateIniContent(task)` - 產生 INI 內容
- `WriteIniFile(filePath, content)` - 寫入 INI 檔案
- `NormalizeFilter(rawFilter)` - 規範化篩選器

#### IProcessRunner
- `StartProcess(exePath)` - 啟動程式
- `IsProcessRunning(processId)` - 檢查進程
- `MonitorCompletionAsync(completeDir, timeoutSeconds, onProgress, cancellationToken)` - 監控完成
- `KillProcess(processId, timeoutMs)` - 終止進程
- `GetProcessExitCode(processId)` - 獲取結束代碼

#### ILogService
- `LogInfo(message)` - 記錄資訊
- `LogWarn(message)` - 記錄警告
- `LogError(message)` - 記錄錯誤
- `GetAllLogs()` - 獲取全部日誌
- `ClearLogs()` - 清空日誌

## 配置

### 路徑設定

應用程式需要配置以下路徑：

1. **AnalysisTaskPath**：分析任務工作目錄
   - 必須包含 `New`、`Complete`、`History` 子目錄
   - 若不存在會自動建立

2. **PCRAnalysisExePath**：QKBqPCRAnalysis.exe 的完整路徑
   - 檔案必須存在
   - 必須是 `.exe` 檔案

## 錯誤處理

應用程式採用分層錯誤處理：

1. **服務層**：捕獲檔案 I/O 和系統錯誤，記錄詳細日誌
2. **ViewModel 層**：統一處理命令執行異常
3. **UI 層**：顯示使用者友善的錯誤訊息

所有錯誤都會記錄至日誌窗格供除錯使用。

## 日誌格式

```
[HH:mm:ss.fff] [LEVEL] message
```

- `INFO`：正常流程進度
- `WARN`：非致命警告
- `ERROR`：致命錯誤

日誌容量上限為 5000 行，超出時自動刪除最舊的 500 行。

## 規格書

詳細的功能規格請參考 [WPF_GUI_Spec_Outline_v0.3.md](../WPF_GUI_Spec_Outline_v0.3.md)

## 版本歷史

- **v0.4.0** (2026-01-14)
  - 初始實現
  - 完整的 MVVM 架構
  - 所有核心服務實現
  - 基本 UI 介面

## 授權

私有專案

## 作者

Albert Ke
