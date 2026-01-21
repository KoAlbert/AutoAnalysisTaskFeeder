using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoAnalysisTaskFeeder.Models;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// 資料夾掃描與解析服務介面
    /// </summary>
    public interface IFolderScanService
    {
        /// <summary>
        /// 非同步掃描實驗資料夾並產生 TaskItem 集合
        /// </summary>
        /// <param name="folderPaths">待掃描的資料夾路徑集合</param>
        /// <param name="onProgress">進度回調</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>TaskItem 集合</returns>
        Task<List<TaskItem>> ScanFoldersAsync(IEnumerable<string> folderPaths, IProgress<string>? onProgress = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// INI 檔案服務介面
    /// </summary>
    public interface IIniService
    {
        /// <summary>
        /// 根據 TaskItem 產生 NewAnalysis.ini 內容
        /// </summary>
        /// <param name="task">任務項目</param>
        /// <returns>INI 檔案內容</returns>
        string GenerateIniContent(TaskItem task);

        /// <summary>
        /// 將 INI 內容寫入檔案
        /// </summary>
        /// <param name="filePath">目標檔案路徑</param>
        /// <param name="content">INI 內容</param>
        void WriteIniFile(string filePath, string content);

        /// <summary>
        /// 標準化篩選器字串（移除尾端多餘冒號）
        /// </summary>
        /// <param name="rawFilter">原始篩選器字串</param>
        /// <returns>標準化後的篩選器字串</returns>
        string NormalizeFilter(string? rawFilter);
    }

    /// <summary>
    /// 外部程式執行與監控介面
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// 啟動外部程式
        /// </summary>
        /// <param name="exePath">可執行檔路徑</param>
        /// <returns>程式進程 ID，失敗返回 -1</returns>
        int StartProcess(string exePath);

        /// <summary>
        /// 檢查進程是否仍在執行
        /// </summary>
        /// <param name="processId">進程 ID</param>
        /// <returns>true 表示進程執行中</returns>
        bool IsProcessRunning(int processId);

        /// <summary>
        /// 非同步監控檔案完成
        /// </summary>
        /// <param name="completeDir">Complete 目錄路徑</param>
        /// <param name="timeoutSeconds">超時時間（秒）</param>
        /// <param name="onProgress">進度回調</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>true 表示成功完成，false 表示超時</returns>
        Task<bool> MonitorCompletionAsync(string completeDir, int timeoutSeconds, IProgress<int>? onProgress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 終止進程
        /// </summary>
        /// <param name="processId">進程 ID</param>
        /// <param name="timeoutMs">等待終止的時間（毫秒）</param>
        void KillProcess(int processId, int timeoutMs = 5000);

        /// <summary>
        /// 獲取進程的結束代碼
        /// </summary>
        /// <param name="processId">進程 ID</param>
        /// <returns>結束代碼，若進程仍執行則返回 -1</returns>
        int GetProcessExitCode(int processId);
    }

    /// <summary>
    /// 日誌服務介面
    /// </summary>
    public interface ILogService
    {
        /// <summary>
        /// 記錄資訊級日誌
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// 記錄警告級日誌
        /// </summary>
        void LogWarn(string message);

        /// <summary>
        /// 記錄錯誤級日誌
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// 獲取所有日誌文本
        /// </summary>
        /// <returns>日誌文本</returns>
        string GetAllLogs();

        /// <summary>
        /// 清空日誌
        /// </summary>
        void ClearLogs();

        /// <summary>
        /// 日誌變更事件
        /// </summary>
        event LogChangedEventHandler LogChanged;
    }

    /// <summary>
    /// 日誌變更事件委派
    /// </summary>
    public delegate void LogChangedEventHandler(string newLog);
}
