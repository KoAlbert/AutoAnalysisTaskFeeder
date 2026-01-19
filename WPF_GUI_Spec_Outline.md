# AutoAnalysisTaskFeeder（WPF）規格書章節大綱（精簡版）

> 目的：建立自動化解析實驗資料後生成Analysis程式執行所需的ini檔案,並能自動啟動分析程式並監視分析結束狀態持續匯入下一筆ini檔,直到所有實驗資料分析完成。  
> 框架假設：.NET（建議 6/8）+ WPF + MVVM（CommunityToolkit.Mvvm 或 Prism 皆可）。

---

## 1. 文件資訊
- 版本紀錄: 

  ❇️Version : Ver0.1  
  ❇️Date : 2025/12/30  
  ❇️Author : Albert Ke

- 名詞定義及說明
  1. **AnalysisTask目錄** :   
    目錄位置為TextBoxAnalysisTaskPath內所顯示的字串，字串可由使用者按下"btnAnalysisTaskPath"後選取的目錄名稱貼上或是由使用者自行key in。  
    目錄架構如下 :   
    AnalysisTask/  
    ├─ Complete/  
    ├─ History/  
    └─ New/ 

    - New : 將NewAnalysis.ini放入此目錄後會觸發分析程式"QKBqPCRAnalysis.exe"對實驗資料進行分析。
    - Complete : 對實驗資料分析完成後，分析程式會將NewAnalysis.ini搬移至此目錄，等約3分鐘後再移至History目錄。
    - History : 此目錄用來記錄已完成的實驗分析目錄資料，本程式不會用到此目錄。        
 
  2. **QKBqPCRAnalysis.exe** : 
    - 實驗資料分析所需的程式 :  
    目錄位置為TextBoxPCRAnalysisPath內所顯示的字串，字串可由使用者按下"btnPCRAnalysisPath"後選取的目錄名稱貼上或是由使用者自行key in。  
    首次執行後會清除New及Complete資料夾內的檔案後監控New目錄下是否有新的NewAnalysis.ini存在。

  3. **NewAnalysis.ini** : 
    - 檔案內容如下 :  
      ```ini
      [Information]
      Enabled=1
      TotalCycle=40
      Flag=0
      TotalChip=6
      Path=R:\EDD_Software\SPR01v1\data ReDoAnalyze\202512191409_QSR2005_Interfere Hemo+EtOH
      User=Admin
      Filter=FAM::ROX: 
      ```
      **需要動態修改的欄位如下** : 
    - TotalCycle :  
      填入由實驗目錄下名稱格式為PROG_xxxxx.ini (xxx為變動欄位)檔案的內的[qPCRSetting]下的Cycle數值

      ```ini
      [qPCRSetting]
      DelayTimeHotStart = 180000
      DelayTimeCycle = 22000
      Cycle = 40
      ChipType = 0
      HotStart = 1
      RTqPCR=0
      ```
    - TotalChip :  
      填入由實驗目錄下名稱格式為xxx_Note.json (xxx為變動欄位)檔案的內的Prpoerty Name "Filter Selection" 下的Value個數
      - `TotalChip = length($["Filter Selection"])`

      ```json
        "Filter Selection":[
          "FAM::ROX::",
          "FAM::ROX::",
          "FAM::ROX::",
          "FAM::ROX::",
          "FAM::ROX::",
          "FAM::ROX::"
        ],
        ```
    - Filter :  
      填入由實驗目錄下名稱格式為xxx_Note.json (xxx為變動欄位)檔案的內的Prpoerty Name "Filter Selection" 下的第一個value的字串
      - `Filter = $["Filter Selection"][0]`   

    - Path :  
      由使用者按下"測試資料目錄選取"鍵後所選取的實驗資料目錄名稱(可複選)。  
      解析每個實驗資料目錄後都會產生一個對應的NewAnalysis.ini檔。

  4. **ProcessingLog** :   
    用來顯示程式執行過程中的log訊息,以便除錯。
    
---

## 2. 系統範圍與成功標準
- 範圍：掃描資料目錄 → 更新目錄列表及 → 建立任務清單 → 產生 `NewAnalysis.ini` → 啟動分析 → 顯示進度與 Log
- 不在範圍：分析演算法本體（僅負責觸發/監控）
- 驗收：主流程可完成、UI 不凍結、錯誤可追蹤（Log + 提示）

---

## 3. 技術與架構（WPF 最小集合）
- 模式：MVVM
  - View：MainWindow.xaml
  - ViewModel：MainViewModel
  - Service：FolderScanService / IniService / ProcessRunner / LogService
- 執行緒：長任務（掃描/分析）使用 Task + IProgress 或 Dispatcher 更新 UI
- 資料繫結：ObservableCollection + ICommand（RelayCommand）

---

## 4. UI 佈局規格（對應圖面）
### 4.1 版面結構（MainWindow）
- 上方：CommandButtonsPane（三顆按鈕）
- 左側：TaskPane（DataGrid）
- 右側：LogPane（ProcessingLog）
- 底部：ProgressPane（ProgressBar + X/N）

```mermaid
flowchart TB
  A[MainWindow]
  A --> B[Top: CommandButtonsPane]
  A --> C[Center: Split Left/Right]
  C --> D[Left: TaskPane (DataGrid)]
  C --> E[Right: LogPane (ProcessingLog)]
  A --> F[Bottom: ProgressPane (ProgressBar + X/N)]
```

### 4.2 控制項命名（建議固定，便於 AI 生成）
- Buttons
  - `btnSelectFolder`：測試資料目錄選取
  - `btnGenIni`：產生 NewAnalysis.ini
  - `btnStart`：啟動自動分析
- DataGrid
  - `dgTasks`：目錄列表（任務清單）
- Progress
  - `pbProgress`：進度條
  - `lblCount`：X/N
- Log
  - `tbLog`：ProcessingLog（TextBox 或 RichTextBox）

---

## 5. DataGrid 資料模型（TaskItem）
### 5.1 欄位（對應 GUI 欄位）
- `Index`（Item）
- `FolderName`
- `Machine`
- `App`
- `SoftwareVersion`
- `TotalCycle`（int?）
- `TotalChip`（int?）
- `Filter`（string/bool：是否納入或過濾原因）
- `Status`（Pending/IniGenerated/Running/Success/Failed/Skipped）

### 5.2 繫結
- `ObservableCollection<TaskItem> Tasks`
- `TaskItem? SelectedTask` 或 `IList SelectedTasks`

---

## 6. 命令與互動規格（ICommand）
### 6.1 命令清單
- `SelectFolderCommand`
- `GenerateIniCommand`
- `StartAnalysisCommand`

### 6.2 主流程（Happy Path）
1) SelectFolder → 掃描/解析 → 填入 DataGrid → N 設定  
2) GenerateIni → 依選取列（或全部）產生 ini → 更新 Status  
3) StartAnalysis → 逐筆執行 → 更新 Progress + Log → 完成後彙總

---

## 7. 狀態與按鈕可用性（簡化規則）
- 初始：只允許 `btnSelectFolder`
- 有任務後：允許 `btnGenIni`
- 至少一筆 ini 產生成功：允許 `btnStart`
- 執行中：三按鈕全部 Disabled（或只保留取消按鈕，若未做取消功能則全鎖）

（可用 `bool IsBusy` + `CanExecute` 統一控管）

---

## 8. Log 規格（ProcessingLog）
- 格式：`[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] message`
- 等級：INFO / WARN / ERROR
- 行為：只讀、可複製、可選擇是否自動捲到底
- 容量：上限行數（例如 5000），超過採 FIFO 刪除（避免卡頓）

---

## 9. 檔案/路徑與外部程式（最小必要）
- 掃描根目錄：子資料夾判定規則（文件化）
- INI
  - 範本路徑：`Templates/NewAnalysis.ini.template`（示例）
  - 輸出規則：每個 Task 產生一份或集中一份（擇一寫死）
  - 覆寫策略：禁止覆寫 / 自動加尾碼 / 先詢問（擇一）
- 外部分析程式
  - 可設定路徑（appsettings.json 或 UI 設定）
  - 啟動參數格式與成功判定（ExitCode / 產出檔案存在）

---

## 10. 非功能性（精簡）
- UI 不凍結：長任務必須 async
- 錯誤可追：所有例外必須寫入 Log，並以 MessageBox 提示摘要
- 相容：中文路徑、長路徑、無權限時的處理

---

## 11. 測試檢核表（可直接轉工單）
- UI：Resize、Split、DataGrid 捲動、Log 自動捲動
- 流程：選目錄→產生 ini→啟動分析
- 錯誤：空目錄/無權限/INI 寫入失敗/外部程式不存在
- 壓力：大量任務（例如 500~5000）、長 Log

---

## 附錄 A：最小 ViewModel 欄位清單（便於 AI 生成）
- `ObservableCollection<TaskItem> Tasks`
- `TaskItem? SelectedTask`
- `string LogText`
- `int TotalCount`
- `int ProcessedCount`
- `double ProgressValue`（0~100 或 0~N）
- `bool IsBusy`
- Commands：`SelectFolderCommand / GenerateIniCommand / StartAnalysisCommand`
