using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoAnalysisTaskFeeder.Models;
using TaskStatusEnum = AutoAnalysisTaskFeeder.Models.TaskStatus;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// 資料夾掃描與解析服務實現
    /// </summary>
    public class FolderScanService : IFolderScanService
    {
        private readonly IIniService _iniService;
        private readonly ILogService _logService;

        public FolderScanService(IIniService iniService, ILogService logService)
        {
            _iniService = iniService ?? throw new ArgumentNullException(nameof(iniService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// 非同步掃描實驗資料夾並產生 TaskItem 集合
        /// </summary>
        public async Task<List<TaskItem>> ScanFoldersAsync(
            IEnumerable<string> folderPaths,
            IProgress<string>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<TaskItem>();
            int itemIndex = 1;

            _logService.LogInfo("掃描資料夾開始");

            foreach (var folderPath in folderPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                onProgress?.Report($"掃描中: {Path.GetFileName(folderPath)}");

                try
                {
                    var task = await ScanSingleFolderAsync(folderPath);
                    if (task != null)
                    {
                        task.Item = itemIndex++;
                        task.Status = TaskStatusEnum.Pending;
                        tasks.Add(task);
                        _logService.LogInfo($"掃描完成: {folderPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"掃描失敗: {folderPath} - {ex.Message}");
                }

                // 模擬非同步處理
                await Task.Delay(10, cancellationToken);
            }

            _logService.LogInfo($"掃描完畢: 成功 {tasks.Count}，耗時");

            return tasks;
        }

        /// <summary>
        /// 掃描單一資料夾
        /// </summary>
        private async Task<TaskItem?> ScanSingleFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                _logService.LogError($"資料夾不存在: {folderPath}");
                return null;
            }

            var task = new TaskItem
            {
                FolderName = Path.GetFileName(folderPath),
                FolderPath = folderPath
            };

            // 掃描 JSON 檔案
            var jsonFile = FindLatestFile(folderPath, "*_Note.json");
            if (jsonFile != null)
            {
                ParseJsonFile(jsonFile, task);
            }

            // 掃描 INI 檔案
            var iniFile = FindLatestFile(folderPath, "PROG_*.ini");
            if (iniFile != null)
            {
                ParseIniFile(iniFile, task);
            }

            return task;
        }

        /// <summary>
        /// 找尋最新的符合模式的檔案
        /// </summary>
        private string? FindLatestFile(string directory, string pattern)
        {
            try
            {
                var files = Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length == 0)
                    return null;

                if (files.Length > 1)
                {
                    _logService.LogWarn($"多筆 {pattern}，採用最新版本");
                }

                return files
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logService.LogError($"搜尋檔案失敗 ({pattern}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 JSON 檔案
        /// </summary>
        private void ParseJsonFile(string filePath, TaskItem task)
        {
            try
            {
                string jsonText = File.ReadAllText(filePath);
                using (JsonDocument doc = JsonDocument.Parse(jsonText))
                {
                    var root = doc.RootElement;

                    // 解析 Machine Code
                    if (root.TryGetProperty("Machine Code", out var machineCode))
                    {
                        task.Machine = machineCode.GetString() ?? "N/A";
                    }
                    else
                    {
                        task.Machine = "N/A";
                        _logService.LogWarn("無法讀取 Machine Code，使用預設值 'N/A'");
                    }

                    // 解析 Program Name
                    if (root.TryGetProperty("Program Name", out var programName))
                    {
                        task.App = programName.GetString() ?? "N/A";
                    }
                    else
                    {
                        task.App = "N/A";
                    }

                    // 解析 Software Version
                    if (root.TryGetProperty("Software Version", out var version))
                    {
                        task.SoftwareVersion = version.GetString() ?? "N/A";
                    }
                    else
                    {
                        task.SoftwareVersion = "N/A";
                    }

                    // 解析 User Name
                    if (root.TryGetProperty("User Name", out var userName))
                    {
                        task.UserName = userName.GetString() ?? "Unknown";
                    }
                    else
                    {
                        task.UserName = "Unknown";
                        _logService.LogWarn("無法讀取 User Name，使用預設值 'Unknown'");
                    }

                    // 解析 Filter Selection
                    if (root.TryGetProperty("Filter Selection", out var filterArray))
                    {
                        task.TotalChip = filterArray.GetArrayLength();

                        if (task.TotalChip > 0 && filterArray[0].ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            string? rawFilter = filterArray[0].GetString();
                            task.Filter = _iniService.NormalizeFilter(rawFilter);
                        }
                        else
                        {
                            task.Filter = "";
                            _logService.LogWarn("Filter Selection 陣列為空或格式錯誤");
                        }
                    }
                    else
                    {
                        _logService.LogError("JSON 缺少 Filter Selection");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"JSON 解析失敗: {filePath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 解析 INI 檔案
        /// </summary>
        private void ParseIniFile(string filePath, TaskItem task)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                bool inQPCRSection = false;

                foreach (var line in lines)
                {
                    if (line.Trim() == "[qPCRSetting]")
                    {
                        inQPCRSection = true;
                        continue;
                    }

                    if (line.StartsWith("[") && inQPCRSection)
                    {
                        break;
                    }

                    if (inQPCRSection && line.Contains("="))
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            if (key == "Cycle" && int.TryParse(value, out int cycle))
                            {
                                task.TotalCycle = cycle;
                            }
                        }
                    }
                }

                if (task.TotalCycle == 0)
                {
                    _logService.LogWarn("無法讀取 Cycle 值，使用預設值 0");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"INI 解析失敗: {filePath} - {ex.Message}");
            }
        }
    }
}
