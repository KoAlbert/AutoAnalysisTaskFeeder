using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoAnalysisTaskFeeder.Models;
using AutoAnalysisTaskFeeder.Services;
using AutoAnalysisTaskFeeder.Utilities;
using TaskStatusEnum = AutoAnalysisTaskFeeder.Models.TaskStatus;

namespace AutoAnalysisTaskFeeder.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IFolderScanService _folderScanService;
        private readonly IIniService _iniService;
        private readonly IProcessRunner _processRunner;
        private readonly ILogService _logService;
        private readonly SemaphoreSlim _executionLock = new(1, 1);
        private readonly UserSettings _userSettings;
        private CancellationTokenSource? _cancellationTokenSource;

        // Properties
        private ObservableCollection<TaskItem> _tasks = new();
        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        private string _analysisTaskPath = "";
        public string AnalysisTaskPath
        {
            get => _analysisTaskPath;
            set => SetProperty(ref _analysisTaskPath, value);
        }

        private string _pcrAnalysisExePath = "";
        public string PcrAnalysisExePath
        {
            get => _pcrAnalysisExePath;
            set => SetProperty(ref _pcrAnalysisExePath, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _processedCount;
        public int ProcessedCount
        {
            get => _processedCount;
            set => SetProperty(ref _processedCount, value);
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        private bool _isAnalysisRunning;
        public bool IsAnalysisRunning
        {
            get => _isAnalysisRunning;
            set => SetProperty(ref _isAnalysisRunning, value);
        }

        private int _currentRunningProcessId = -1;

        // Commands
        public ICommand SelectFolderCommand { get; }
        public ICommand GenerateIniCommand { get; }
        public ICommand StartAnalysisCommand { get; }
        public ICommand StopAnalysisCommand { get; }
        public ICommand SelectAnalysisTaskPathCommand { get; }
        public ICommand SelectPcrAnalysisPathCommand { get; }

        public MainViewModel(
            IFolderScanService folderScanService,
            IIniService iniService,
            IProcessRunner processRunner,
            ILogService logService)
        {
            _folderScanService = folderScanService ?? throw new ArgumentNullException(nameof(folderScanService));
            _iniService = iniService ?? throw new ArgumentNullException(nameof(iniService));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // 載入使用者設定
            _userSettings = UserSettings.Load();
            AnalysisTaskPath = _userSettings.LastAnalysisTaskPath;
            PcrAnalysisExePath = _userSettings.LastPcrAnalysisExePath;

            // 訂閱日誌變更事件
            _logService.LogChanged += OnLogChanged;

            // Setup commands
            SelectFolderCommand = new AsyncRelayCommand(OnSelectFolder);
            GenerateIniCommand = new AsyncRelayCommand(OnGenerateIni);
            StartAnalysisCommand = new AsyncRelayCommand(OnStartAnalysis);
            StopAnalysisCommand = new RelayCommand(OnStopAnalysis);
            SelectAnalysisTaskPathCommand = new RelayCommand(OnSelectAnalysisTaskPath);
            SelectPcrAnalysisPathCommand = new RelayCommand(OnSelectPcrAnalysisPath);

            // 初始化日誌
            _logService.LogInfo("應用程式已就緒");
        }

        private void OnLogChanged(string newLog)
        {
            // ��s UI �W�� LogText
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                LogText = newLog;
            });
        }

        private async Task OnSelectFolder()
        {
            await _executionLock.WaitAsync();
            try
            {
                // 檢查是否有現有任務，若有則提示確認
                if (Tasks.Count > 0)
                {
                    var confirmResult = System.Windows.MessageBox.Show(
                        $"現有列表包含 {Tasks.Count} 個任務，是否清除？",
                        "確認",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (confirmResult == System.Windows.MessageBoxResult.No)
                    {
                        return;
                    }

                    // 檢查是否有已生成 INI 的任務
                    var iniGeneratedTasks = Tasks.Where(t => t.Status == TaskStatusEnum.IniGenerated).ToList();
                    if (iniGeneratedTasks.Count > 0)
                    {
                        var deleteIniResult = System.Windows.MessageBox.Show(
                            "部分任務已生成 INI，是否同時刪除 INI 檔案？",
                            "確認",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);

                        if (deleteIniResult == System.Windows.MessageBoxResult.Yes)
                        {
                            foreach (var task in iniGeneratedTasks)
                            {
                                try
                                {
                                    if (!string.IsNullOrEmpty(task.IniFilePath) && File.Exists(task.IniFilePath))
                                    {
                                        File.Delete(task.IniFilePath);
                                        _logService.LogInfo($"已刪除 INI: {task.IniFilePath}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logService.LogWarn($"刪除 INI 失敗: {task.IniFilePath} - {ex.Message}");
                                }
                            }
                        }
                    }
                }

                IsBusy = true;
                StatusMessage = "正在選取資料夾...";
                _logService.LogInfo("開始選取資料夾");

                // 使用 Ookii.Dialogs.Wpf.VistaFolderBrowserDialog 支援多選
                var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog
                {
                    Description = "請選取實驗資料夾（可多選）",
                    UseDescriptionForTitle = true,
                    Multiselect = true
                };

                // 設定初始目錄為上次選擇的路徑
                if (!string.IsNullOrEmpty(_userSettings.LastExperimentDataPath) && 
                    Directory.Exists(_userSettings.LastExperimentDataPath))
                {
                    dialog.SelectedPath = _userSettings.LastExperimentDataPath;
                }

                var result = dialog.ShowDialog();
                if (result != true || dialog.SelectedPaths == null || dialog.SelectedPaths.Length == 0)
                {
                    StatusMessage = "已取消選取";
                    return;
                }

                var selectedFolders = dialog.SelectedPaths;
                _logService.LogInfo($"已選取 {selectedFolders.Length} 個資料夾");

                // 儲存上次選擇的目錄
                if (selectedFolders.Length > 0)
                {
                    var parentPath = Path.GetDirectoryName(selectedFolders[0]);
                    if (!string.IsNullOrEmpty(parentPath))
                    {
                        _userSettings.LastExperimentDataPath = parentPath;
                        _userSettings.Save();
                    }
                }

                Tasks.Clear();
                TotalCount = 0;
                ProcessedCount = 0;
                ProgressValue = 0;

                StatusMessage = "正在掃描資料夾...";
                var scannedTasks = await _folderScanService.ScanFoldersAsync(selectedFolders);

                int successCount = 0;
                int failCount = 0;

                foreach (var task in scannedTasks)
                {
                    Tasks.Add(task);
                    if (task.Status == TaskStatusEnum.Pending)
                        successCount++;
                    else if (task.Status == TaskStatusEnum.Failed)
                        failCount++;
                }

                TotalCount = Tasks.Count;
                
                // 顯示摘要 MessageBox
                string message;
                if (failCount == 0 && successCount > 0)
                {
                    message = $"成功載入 {successCount} 個資料夾";
                }
                else if (successCount == 0)
                {
                    message = "無法解析任何選取的資料夾。詳見 Log。";
                }
                else
                {
                    message = $"成功: {successCount}，失敗: {failCount}。詳見 Log。";
                }

                System.Windows.MessageBox.Show(
                    message,
                    "掃描完成",
                    System.Windows.MessageBoxButton.OK,
                    failCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);

                StatusMessage = $"掃描完畢，共 {TotalCount} 個任務";
                _logService.LogInfo($"掃描完畢: 成功 {successCount}，失敗 {failCount}，總計 {TotalCount} 個任務");
            }
            catch (Exception ex)
            {
                StatusMessage = "掃描失敗";
                _logService.LogError($"掃描錯誤: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"掃描失敗: {ex.Message}",
                    "錯誤",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                _executionLock.Release();
            }
        }

        private async Task OnGenerateIni()
        {
            await _executionLock.WaitAsync();
            try
            {
                IsBusy = true;
                StatusMessage = "Generating INI files...";
                ProcessedCount = 0;

                var tasksToProcess = Tasks.Where(t => t.Status == TaskStatusEnum.Pending).ToList();

                if (tasksToProcess.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "沒有待處理的任務。",
                        "產生 INI",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                int successCount = 0;
                int failCount = 0;

                foreach (var task in tasksToProcess)
                {
                    task.Status = TaskStatusEnum.Generating;

                    try
                    {
                        var iniContent = _iniService.GenerateIniContent(task);
                        
                        // 1. 保存副本至實驗資料夾
                        var backupPath = Path.Combine(task.FolderPath, "NewAnalysis.ini");
                        _iniService.WriteIniFile(backupPath, iniContent);
                        _logService.LogInfo($"INI 副本已保存: {backupPath}");
                        
                        // 2. 保存主檔至 AnalysisTask\New
                        var mainPath = Path.Combine(AnalysisTaskPath, "New", "NewAnalysis.ini");
                        _iniService.WriteIniFile(mainPath, iniContent);
                        _logService.LogInfo($"INI 已產生: {mainPath}");

                        // 3. 更新任務狀態
                        task.Status = TaskStatusEnum.IniGenerated;
                        task.IniFilePath = mainPath;
                        task.GeneratedTime = DateTime.Now;
                        
                        successCount++;
                        ProcessedCount++;
                        ProgressValue = (ProcessedCount * 100.0) / tasksToProcess.Count;
                    }
                    catch (Exception ex)
                    {
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = ex.Message;
                        _logService.LogError($"INI 產生失敗: {task.FolderName} - {ex.Message}");
                        failCount++;
                    }
                }

                // 顯示摘要 MessageBox
                string message;
                if (failCount == 0)
                {
                    message = $"已成功產生 {successCount} 份 INI";
                }
                else if (successCount == 0)
                {
                    message = "無法產生任何 INI。詳見 Log。";
                }
                else
                {
                    message = $"成功: {successCount}，失敗: {failCount}。詳見 Log。";
                }

                System.Windows.MessageBox.Show(
                    message,
                    "產生 INI 完成",
                    System.Windows.MessageBoxButton.OK,
                    failCount > 0 ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information);

                StatusMessage = "INI generation complete";
            }
            finally
            {
                IsBusy = false;
                _executionLock.Release();
            }
        }

        private async Task OnStartAnalysis()
        {
            await _executionLock.WaitAsync();
            try
            {
                IsBusy = true;
                IsAnalysisRunning = true;
                StatusMessage = "Starting analysis...";
                ProcessedCount = 0;
                ProgressValue = 0; // *** 修正：歸零進度條 ***

                var tasksToRun = Tasks.Where(t => t.Status == TaskStatusEnum.IniGenerated).ToList();

                if (tasksToRun.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "沒有待執行的任務。請先產生 INI。",
                        "啟動分析",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                // 建立 CancellationTokenSource
                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;

                int successCount = 0;
                int failCount = 0;
                var startTime = DateTime.Now;

                _logService.LogInfo($"開始分析流程，共 {tasksToRun.Count} 個任務");

                foreach (var task in tasksToRun)
                {
                    // 檢查是否已取消
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logService.LogInfo("使用者已取消分析");
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = "使用者取消";
                        break;
                    }

                    try
                    {
                        task.Status = TaskStatusEnum.Running;
                        _logService.LogInfo($"執行任務 {task.Item}/{TotalCount}: {task.FolderName}...");

                        // 啟動外部程式（繼承父程序的管理員權限）
                        var processId = _processRunner.StartProcess(PcrAnalysisExePath);
                        if (processId < 0)
                        {
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "Failed to start process";
                            _logService.LogError($"啟動程式失敗: {task.FolderName}");
                            failCount++;
                            continue;
                        }

                        task.ProcessId = processId;
                        _currentRunningProcessId = processId; // 記錄當前運行的程式ID
                        _logService.LogInfo($"已啟動 QKBqPCRAnalysis.exe (PID={processId})");

                        // 等待 2 秒讓外部程式完成初始化和目錄清空動作
                        await Task.Delay(2000, cancellationToken);
                        _logService.LogInfo("外部程式就緒，目錄已清空");

                        // 投遞 INI 檔案（從實驗資料夾副本複製至 AnalysisTask\New）
                        var sourcePath = Path.Combine(task.FolderPath, "NewAnalysis.ini");
                        var targetPath = Path.Combine(AnalysisTaskPath, "New", "NewAnalysis.ini");
                        
                        if (!File.Exists(sourcePath))
                        {
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "INI 副本不存在";
                            _logService.LogError($"INI 副本不存在: {sourcePath}");
                            _processRunner.KillProcess(processId);
                            _currentRunningProcessId = -1;
                            failCount++;
                            continue;
                        }

                        File.Copy(sourcePath, targetPath, true);
                        _logService.LogInfo($"已投遞 INI: {task.FolderName}");

                        // 監控完成
                        var completeDir = Path.Combine(AnalysisTaskPath, "Complete");
                        var completed = await _processRunner.MonitorCompletionAsync(
                            completeDir,
                            900,
                            null,
                            cancellationToken); // 傳遞 cancellationToken

                        // 檢查是否已取消
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logService.LogInfo("使用者已取消分析，正在清理...");
                            if (_processRunner.IsProcessRunning(processId))
                            {
                                _processRunner.KillProcess(processId);
                                _logService.LogInfo("已關閉 QKBqPCRAnalysis.exe");
                            }
                            _currentRunningProcessId = -1;
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "使用者取消";
                            break;
                        }

                        if (completed)
                        {
                            task.Status = TaskStatusEnum.Completed;
                            task.CompletedTime = DateTime.Now;
                            var elapsed = (task.CompletedTime - startTime).TotalSeconds;
                            _logService.LogInfo($"任務完成: {task.FolderName} (耗時 {elapsed:F1}s)");
                            successCount++;
                            ProcessedCount++;
                        }
                        else
                        {
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "監控超時，分析程式未完成";
                            _logService.LogError($"監控超時: {task.FolderName} (15 分鐘內未完成)");
                            failCount++;
                        }

                        // 關閉程式
                        if (_processRunner.IsProcessRunning(processId))
                        {
                            _processRunner.KillProcess(processId);
                            _logService.LogInfo("已關閉 QKBqPCRAnalysis.exe");
                        }
                        _currentRunningProcessId = -1;
                    }
                    catch (OperationCanceledException)
                    {
                        // 使用者取消操作
                        _logService.LogInfo("使用者已取消分析");
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = "使用者取消";
                        
                        if (_currentRunningProcessId > 0 && _processRunner.IsProcessRunning(_currentRunningProcessId))
                        {
                            _processRunner.KillProcess(_currentRunningProcessId);
                            _logService.LogInfo("已關閉 QKBqPCRAnalysis.exe");
                        }
                        _currentRunningProcessId = -1;
                        break;
                    }
                    catch (Exception ex)
                    {
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = ex.Message;
                        _logService.LogError($"Analysis failed for {task.FolderName}: {ex.Message}");
                        failCount++;
                        
                        if (_currentRunningProcessId > 0 && _processRunner.IsProcessRunning(_currentRunningProcessId))
                        {
                            _processRunner.KillProcess(_currentRunningProcessId);
                        }
                        _currentRunningProcessId = -1;
                    }

                    ProgressValue = ((successCount + failCount) * 100.0) / tasksToRun.Count;
                }

                var totalElapsed = (DateTime.Now - startTime).TotalSeconds;
                
                // 檢查是否因為取消而結束
                bool wasCancelled = cancellationToken.IsCancellationRequested;
                
                if (wasCancelled)
                {
                    _logService.LogInfo($"分析已取消: 成功 {successCount}，失敗/取消 {failCount}，耗時 {totalElapsed:F1}s");
                    StatusMessage = "Cancelled";
                }
                else
                {
                    _logService.LogInfo($"分析流程完畢: 成功 {successCount}，失敗 {failCount}，耗時 {totalElapsed:F1}s");
                }

                // 顯示摘要 MessageBox
                string message;
                System.Windows.MessageBoxImage icon;
                
                if (wasCancelled)
                {
                    message = $"分析已取消。已完成: {successCount}，失敗/取消: {failCount}。";
                    icon = System.Windows.MessageBoxImage.Warning;
                    StatusMessage = "Cancelled";
                }
                else if (failCount == 0)
                {
                    message = $"已完成分析 {successCount} 筆任務";
                    icon = System.Windows.MessageBoxImage.Information;
                    StatusMessage = "Completed";
                }
                else if (successCount == 0)
                {
                    message = "所有任務均失敗。詳見 Log。";
                    icon = System.Windows.MessageBoxImage.Error;
                    StatusMessage = "Error";
                }
                else
                {
                    message = $"成功: {successCount}，失敗: {failCount}。詳見 Log。";
                    icon = System.Windows.MessageBoxImage.Warning;
                    StatusMessage = "Completed (with errors)";
                }

                System.Windows.MessageBox.Show(
                    message,
                    wasCancelled ? "分析已取消" : "分析完成",
                    System.Windows.MessageBoxButton.OK,
                    icon);
            }
            catch (Exception ex)
            {
                _logService.LogError($"分析過程發生錯誤: {ex.Message}");
                
                // 確保清理資源
                if (_currentRunningProcessId > 0 && _processRunner.IsProcessRunning(_currentRunningProcessId))
                {
                    _processRunner.KillProcess(_currentRunningProcessId);
                    _logService.LogInfo("已關閉 QKBqPCRAnalysis.exe");
                }
                _currentRunningProcessId = -1;
                
                throw;
            }
            finally
            {
                IsBusy = false;
                IsAnalysisRunning = false;
                _currentRunningProcessId = -1;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _executionLock.Release();
            }
        }

        private void OnStopAnalysis()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                var result = System.Windows.MessageBox.Show(
                    "確定要停止分析嗎？正在執行的任務將被中斷。",
                    "停止分析",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _logService.LogInfo("使用者請求停止分析");
                    _cancellationTokenSource.Cancel();
                    StatusMessage = "Stopping...";
                }
            }
        }

        private void OnSelectAnalysisTaskPath()
        {
            _logService.LogInfo("開始選擇 AnalysisTask 路徑");

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "請選擇 AnalysisTask 資料夾",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(AnalysisTaskPath) && Directory.Exists(AnalysisTaskPath))
            {
                dialog.SelectedPath = AnalysisTaskPath;
            }

            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                AnalysisTaskPath = dialog.SelectedPath;
                _logService.LogInfo($"已設定 AnalysisTask 路徑: {AnalysisTaskPath}");
                
                // 儲存設定
                _userSettings.LastAnalysisTaskPath = AnalysisTaskPath;
                _userSettings.Save();
            }
            else
            {
                _logService.LogInfo("已取消選擇 AnalysisTask 路徑");
            }
        }

        private void OnSelectPcrAnalysisPath()
        {
            _logService.LogInfo("開始選擇 PCR 分析程式");

            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "請選擇 QKBqPCRAnalysis.exe",
                Filter = "執行檔 (*.exe)|*.exe|所有檔案 (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrEmpty(PcrAnalysisExePath) && File.Exists(PcrAnalysisExePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(PcrAnalysisExePath);
                dialog.FileName = Path.GetFileName(PcrAnalysisExePath);
            }

            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                PcrAnalysisExePath = dialog.FileName;
                _logService.LogInfo($"已設定 PCR 分析程式路徑: {PcrAnalysisExePath}");
                
                // 儲存設定
                _userSettings.LastPcrAnalysisExePath = PcrAnalysisExePath;
                _userSettings.Save();
            }
            else
            {
                _logService.LogInfo("已取消選擇 PCR 分析程式");
            }
        }
    }
}
