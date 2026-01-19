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

        // Commands
        public ICommand SelectFolderCommand { get; }
        public ICommand GenerateIniCommand { get; }
        public ICommand StartAnalysisCommand { get; }
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

            // 訂閱日誌變更事件
            _logService.LogChanged += OnLogChanged;

            // Setup commands
            SelectFolderCommand = new AsyncRelayCommand(OnSelectFolder);
            GenerateIniCommand = new AsyncRelayCommand(OnGenerateIni);
            StartAnalysisCommand = new AsyncRelayCommand(OnStartAnalysis);
            SelectAnalysisTaskPathCommand = new RelayCommand(OnSelectAnalysisTaskPath);
            SelectPcrAnalysisPathCommand = new RelayCommand(OnSelectPcrAnalysisPath);

            // 初始化日誌
            _logService.LogInfo("應用程式已啟動");
        }

        private void OnLogChanged(string newLog)
        {
            // 更新 UI 上的 LogText
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
                IsBusy = true;
                StatusMessage = "正在掃描資料夾...";
                _logService.LogInfo("開始選擇資料夾");

                // 使用 FolderBrowserDialog 選擇資料夾
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "請選擇實驗資料夾",
                    ShowNewFolderButton = false
                };

                var result = dialog.ShowDialog();
                if (result != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    StatusMessage = "已取消選擇";
                    return;
                }

                var folderPath = dialog.SelectedPath;
                _logService.LogInfo($"已選擇資料夾: {folderPath}");

                Tasks.Clear();
                TotalCount = 0;
                ProcessedCount = 0;
                ProgressValue = 0;

                var scannedTasks = await _folderScanService.ScanFoldersAsync(new[] { folderPath });

                foreach (var task in scannedTasks)
                {
                    Tasks.Add(task);
                }

                TotalCount = Tasks.Count;
                StatusMessage = $"掃描完成，找到 {TotalCount} 個任務";
                _logService.LogInfo($"掃描完成: {TotalCount} 個任務");
            }
            catch (Exception ex)
            {
                StatusMessage = "掃描失敗";
                _logService.LogError($"掃描錯誤: {ex.Message}");
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

                foreach (var task in tasksToProcess)
                {
                    task.Status = TaskStatusEnum.Generating;

                    try
                    {
                        var iniContent = _iniService.GenerateIniContent(task);
                        var savePath = Path.Combine(AnalysisTaskPath, "New", "NewAnalysis.ini");

                        _iniService.WriteIniFile(savePath, iniContent);
                        task.Status = TaskStatusEnum.IniGenerated;
                        ProcessedCount++;
                        ProgressValue = (ProcessedCount * 100.0) / tasksToProcess.Count;
                    }
                    catch (Exception ex)
                    {
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = ex.Message;
                        _logService.LogError($"INI generation failed for {task.FolderName}: {ex.Message}");
                    }
                }

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
                StatusMessage = "Starting analysis...";
                ProcessedCount = 0;

                var tasksToRun = Tasks.Where(t => t.Status == TaskStatusEnum.IniGenerated).ToList();

                foreach (var task in tasksToRun)
                {
                    try
                    {
                        task.Status = TaskStatusEnum.Running;

                        // Start process
                        var processId = _processRunner.StartProcess(PcrAnalysisExePath);
                        if (processId < 0)
                        {
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "Failed to start process";
                            continue;
                        }

                        // Monitor completion
                        var completeDir = Path.Combine(AnalysisTaskPath, "Complete");
                        var completed = await _processRunner.MonitorCompletionAsync(
                            completeDir,
                            900); // 15 minutes timeout

                        if (completed)
                        {
                            task.Status = TaskStatusEnum.Completed;
                            ProcessedCount++;
                        }
                        else
                        {
                            task.Status = TaskStatusEnum.Failed;
                            task.ErrorMessage = "Process timeout";
                            _processRunner.KillProcess(processId);
                        }
                    }
                    catch (Exception ex)
                    {
                        task.Status = TaskStatusEnum.Failed;
                        task.ErrorMessage = ex.Message;
                        _logService.LogError($"Analysis failed for {task.FolderName}: {ex.Message}");
                    }

                    ProgressValue = (ProcessedCount * 100.0) / tasksToRun.Count;
                }

                StatusMessage = "Analysis complete";
            }
            finally
            {
                IsBusy = false;
                _executionLock.Release();
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
            }
            else
            {
                _logService.LogInfo("已取消選擇 PCR 分析程式");
            }
        }
    }
}
