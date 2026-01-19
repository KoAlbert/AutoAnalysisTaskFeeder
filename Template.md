# WinForms 程式規格說明書範本（Template）

> 用途：用於在開發前先定義 WinForms 應用程式的 UI、事件、狀態、資料流程與非功能需求。  
> 建議用法：把此檔複製為 `Spec.md`，逐欄填寫完成後，再交給 AI 產生程式骨架（Form + UserControl + Services）。

---

## 0. 文件資訊
| 項目 | 填寫 |
|---|---|
| 文件版本 | v0.1 |
| 建立日期 | YYYY-MM-DD |
| 作者 |  |
| 審閱者 |  |
| 專案/Repo |  |

---

## 1. 專案與架構規格
| 項目 | 填寫 | 備註 |
|---|---|---|
| 應用名稱（App） |  | 例：AutoAnalysisTaskFeeder |
| 主視窗（Form）名稱 |  | 例：MainForm |
| 其他視窗/對話框 |  | 例：SettingsForm / AboutDialog |
| .NET 版本 |  | .NET 8 / .NET Framework 4.8 |
| UI 技術 | WinForms | 固定：WinForms |
| 架構風格 |  | MVP / MVVM-like（Binding）/ 傳統事件驅動 |
| DI/容器 |  | 無 / Microsoft.Extensions.DependencyInjection |
| 記錄（Logging） |  | NLog / Serilog / 自製 |
| 設定儲存 |  | appsettings.json / user.config / registry |
| i18n 多語系 |  | 是/否；資源檔命名規則 |
| 目標平台 |  | x64 / AnyCPU；Windows 版本要求 |

---

## 2. UI 版面概述（ASCII Wireframe）
> 用文字簡圖描述布局，並標註主要容器（Panel / SplitContainer / TableLayoutPanel）。

```
[MainForm]  W=1000 H=650  Min=900x600  Resize=Yes

┌──────────────────────────────────────────────────────────────┐
│ MenuStrip / ToolStrip                                         │
├──────────────────────────────────────────────────────────────┤
│ (Row0) Filters                                                │
│  Source: [______________] [Browse]   Date: [From] [To] [Apply]│
│  Output: [______________] [Browse]   Mode: (A)(B)(C)          │
├───────────────────────────────┬──────────────────────────────┤
│ (Row1) Left: Task List        │ (Row1) Right: Details         │
│  [DataGridView Tasks]         │  [Read-only detail fields]    │
│  [Start] [Stop] [RunOnce]     │  [OpenResult] [ExportLog]     │
├──────────────────────────────────────────────────────────────┤
│ (Row2) Log                                                    │
│  [TextBox Log (read-only, scroll)]  [Clear] [Copy]            │
└──────────────────────────────────────────────────────────────┘
```

---

## 3. Form 屬性與容器（Layout）規格
| 項目 | 填寫 | 備註 |
|---|---|---|
| Text（標題） |  | 可動態更新：例如顯示版本 |
| ClientSize |  | 例：1000×650 |
| MinimumSize |  | 例：900×600 |
| StartPosition |  | CenterScreen |
| FormBorderStyle |  | Sizable / FixedDialog |
| Icon |  | 檔名或來源 |
| 主容器 |  | TableLayoutPanel / SplitContainer / Panels |
| Dock/Anchor 策略 |  | 例如：所有主要區塊 Dock=Fill |
| High DPI |  | 是否支援；AutoScaleMode |

---

## 4. 全域狀態旗標與顯示規則
> WinForms 通常用狀態欄（StatusStrip）+ 控制項 Enable/Visible 控制。  
> 若有背景作業，請明確定義：Running/Busy/Error/Progress。

| 狀態變數（建議） | 型別 | 預設值 | 用途/規則 |
|---|---|---|---|
| IsBusy | bool | false | 顯示忙碌、禁用部分操作 |
| IsRunning | bool | false | Start/Stop 切換 |
| StatusMessage | string | "" | 狀態列訊息 |
| StatusLevel | enum | Info | Info/Warning/Error |
| ProgressValue | int? | null | null=不確定；0~100=確定進度 |
| CanStart | bool |  | 由驗證與狀態推導 |

### 4.1 全域互動規則（範例）
- `IsRunning=true`：
  - Filters 區塊禁用（避免路徑/條件變更）
  - Start disabled、Stop enabled
  - 顯示 ProgressBar（不確定或依進度）
- Error：
  - StatusStrip 顯示錯誤訊息
  - Log 照常 append，必要時彈出 MessageBox

---

## 5. 控制項規格表（核心）
> 建議每個控制項都填：Name / Type / Parent / DataBinding / Events / Validation / Enabled / Visible / Notes  
> 備註：WinForms 的資料繫結可用 `BindingSource`；若不用，請在 Notes 註明由程式碼更新位置。

| Region（區塊） | Control Name | 類型 | Parent/Container | 文字/內容 | DataBinding（來源/屬性） | Event（Handler） | Validation（規則/訊息） | Enabled 規則 | Visible 規則 | Notes（UI/其他） |
|---|---|---|---|---|---|---|---|---|---|---|
| Filters | txtSourcePath | TextBox | pnlFilters |  | `SourcePath` ←→ Text（Binding） | TextChanged / Validating | 必填、路徑存在 | `!IsRunning` |  | 支援拖放；Placeholder 由提示 Label |
| Filters | btnBrowseSource | Button | pnlFilters | Browse |  | Click=btnBrowseSource_Click |  | `!IsRunning` |  | 使用 FolderBrowserDialog / IFileDialogService |
| Filters | dtpFrom | DateTimePicker | pnlFilters |  | `FromDate` ←→ Value | ValueChanged | From ≤ To | `!IsRunning` |  | Format/CustomFormat |
| Filters | dtpTo | DateTimePicker | pnlFilters |  | `ToDate` ←→ Value | ValueChanged | To ≥ From | `!IsRunning` |  |  |
| Main | dgvTasks | DataGridView | splitMain.Panel1 |  | `Tasks`（BindingSource） | SelectionChanged |  | True |  | 設定 VirtualMode（如大量資料） |
| Main | btnStart | Button | pnlActions | Start |  | Click=btnStart_Click |  | `CanStart && !IsRunning` |  | 可加快捷鍵（如 Ctrl+R） |
| Main | btnStop | Button | pnlActions | Stop |  | Click=btnStop_Click |  | `IsRunning` |  | 取消背景作業（CancellationToken） |
| Main | btnRunOnce | Button | pnlActions | Run Once |  | Click=btnRunOnce_Click |  | `!IsRunning` |  |  |
| Details | txtDetail | TextBox | pnlDetails |  | `SelectedTask.*` |  |  | `SelectedTask!=null` |  | ReadOnly=True，多欄位可拆開 |
| Log | txtLog | TextBox | pnlLog |  | `LogText`（可 OneWay） |  |  | True |  | Multiline, ReadOnly, ScrollBars=Vertical |
| Log | btnClearLog | Button | pnlLogActions | Clear |  | Click=btnClearLog_Click |  | True |  |  |
| Status | statusStrip | StatusStrip | MainForm |  |  |  |  |  |  | 含 ToolStripStatusLabel + ToolStripProgressBar |

> 需要更多列請自行複製。

---

## 6. DataGridView 欄位規格表（若有）
| Grid Name | Column Name | HeaderText | DataPropertyName | 型別/格式 | 寬度 | Sort | ReadOnly | Notes |
|---|---|---|---|---|---|---|---|---|
| dgvTasks | colId | ID | Id | string | Auto | Yes | Yes |  |
| dgvTasks | colStatus | Status | Status | string/enum | 120 | Yes | Yes | 顯示文字映射 |
| dgvTasks | colCreated | Created | CreatedAt | DateTime（yyyy-MM-dd HH:mm） | 180 | Yes | Yes |  |

---

## 7. 事件與流程規格（Event/Handler）
> WinForms 以事件為主，建議用「事件→前置檢查→狀態更新→背景作業→UI 回寫」格式描述。

| 事件（Handler） | 觸發控制項 | 前置條件 | 執行流程（概要） | 非同步 | 取消 | 例外處理/訊息 |
|---|---|---|---|---|---|---|
| btnBrowseSource_Click | btnBrowseSource | `!IsRunning` | 開啟資料夾選擇，寫入 SourcePath，重新驗證 | 否 | 否 | 失敗→StatusMessage=Error |
| btnStart_Click | btnStart | `CanStart && !IsRunning` | 設 IsRunning=true → 啟動背景流程 → 更新 dgv/log/progress | 是 | 是 | try/catch；必要時 MessageBox |
| btnStop_Click | btnStop | `IsRunning` | 觸發取消，等待收斂，IsRunning=false | 是 | 是 | 取消視為正常結束或提示 |
| dgvTasks_SelectionChanged | dgvTasks |  | 更新 Details 區塊（SelectedTask） | 否 | 否 |  |
| btnClearLog_Click | btnClearLog |  | 清空 Log | 否 | 否 |  |

---

## 8. 驗證規格（Validation）
| 欄位 | 規則 | 錯誤訊息 | 呈現方式 | 觸發時機 |
|---|---|---|---|---|
| SourcePath | 非空、存在 | 請選擇有效的來源資料夾 | ErrorProvider / StatusStrip | Validating / Start 前 |
| From/To | From ≤ To | 日期區間不合法 | StatusStrip 或欄位提示 | ValueChanged / Apply |
| OutputPath | 非空、可寫入 | 輸出資料夾不可用 | ErrorProvider | Start 前 |

> 建議：使用 `ErrorProvider` 做欄位層級提示；全域訊息用 `StatusStrip`。

---

## 9. 背景作業與執行緒模型
| 項目 | 填寫 | 備註 |
|---|---|---|
| 背景執行方式 |  | Task.Run + async/await（建議）/ BackgroundWorker（舊） |
| 取消機制 |  | CancellationTokenSource |
| UI 更新策略 |  | `Invoke/BeginInvoke` 或使用 `SynchronizationContext` |
| 進度回報 |  | IProgress<int> / event |
| 長時間 I/O |  | 檔案/網路存取需可取消/逾時 |

---

## 10. 對話框與系統互動（Service 介面）
> 建議將對話框、檔案系統與外部操作封裝成 service，便於測試與維護。

| 需求 | 服務類別/介面（建議） | 方法 | 備註 |
|---|---|---|---|
| 選擇資料夾 | IFileDialogService | SelectFolder() | FolderBrowserDialog 包裝 |
| 選擇檔案 | IFileDialogService | OpenFile() / SaveFile() | OpenFileDialog/SaveFileDialog |
| 訊息提示 | IDialogService | ShowInfo/ShowError/Confirm | MessageBox 包裝 |
| 開啟資料夾/檔案 | IShellService | OpenInExplorer(path) | Process.Start |
| 設定讀寫 | ISettingsService | Load/Save | JSON/user.config |

---

## 11. 非功能需求（NFR）
| 項目 | 目標 | 備註 |
|---|---|---|
| 啟動時間 |  | 例：< 2 秒 |
| 大量資料 |  | DataGridView VirtualMode / 分頁 |
| 記憶體限制 |  | Log 行數上限（例：5000） |
| 可靠性 |  | 異常處理、復原策略 |
| 可測試性 |  | 核心邏輯抽離 UI；service 可 mock |
| 佈署 |  | ClickOnce / MSIX / zip |

---

## 12. 程式碼規範（建議）
- 控制項命名：`txtXxx`, `btnXxx`, `dgvXxx`, `pnlXxx`, `lblXxx`, `cmbXxx`, `chkXxx`, `dtpXxx`
- 事件命名：`btnStart_Click`, `dgvTasks_SelectionChanged`
- UI 更新：所有跨執行緒更新必須 `InvokeRequired` 檢查
- 長流程：避免在 UI thread 做 I/O；使用 async/await + cancellation
- Log：集中一個 `ILogSink` 或 `ILogger`；UI log 建議節流更新避免卡頓

---

## 13. 附錄：待確認清單（Checklist）
- [ ] Form 版面（Dock/Anchor）在縮放與 DPI 下正常
- [ ] 所有事件的前置條件明確且有錯誤提示
- [ ] 背景作業可取消、可回報進度、可記錄 log
- [ ] 例外處理策略一致（StatusStrip + Log + 必要時對話框）
- [ ] DataGridView 欄位、排序、選取行為符合需求
- [ ] 設定讀寫位置與格式已定義

---
