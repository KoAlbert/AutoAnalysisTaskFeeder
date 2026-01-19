using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// 外部程式執行與監控服務實現
    /// </summary>
    public class ProcessRunner : IProcessRunner
    {
        private Process _currentProcess;

        /// <summary>
        /// 啟動外部程式（繼承父程序的管理員權限）
        /// </summary>
        public int StartProcess(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                throw new ArgumentException("可執行檔路徑不能為空", nameof(exePath));

            if (!File.Exists(exePath))
                throw new FileNotFoundException($"檔案不存在: {exePath}");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false // 改為 false 以繼承父程序權限
                };

                _currentProcess = Process.Start(startInfo);
                return _currentProcess?.Id ?? -1;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"啟動程式失敗: {exePath}", ex);
            }
        }

        /// <summary>
        /// 檢查進程是否仍在執行
        /// </summary>
        public bool IsProcessRunning(int processId)
        {
            if (processId <= 0)
                return false;

            try
            {
                Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 非同步監控檔案完成
        /// 輪詢檢查 completeDir 中是否出現 CompleteAnalysis.ini
        /// </summary>
        public async Task<bool> MonitorCompletionAsync(
            string completeDir,
            int timeoutSeconds,
            IProgress<int> onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(completeDir))
                throw new ArgumentException("Complete 目錄不能為空", nameof(completeDir));

            if (!Directory.Exists(completeDir))
                throw new DirectoryNotFoundException($"目錄不存在: {completeDir}");

            string targetFile = Path.Combine(completeDir, "CompleteAnalysis.ini");
            int elapsedSeconds = 0;
            int pollIntervalMs = 500;
            int maxPolls = (timeoutSeconds * 1000) / pollIntervalMs;

            for (int i = 0; i < maxPolls; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(targetFile))
                {
                    // 檢查檔案是否寫入完成（修改時間連續 1 秒無變化）
                    var firstCheck = new FileInfo(targetFile).LastWriteTime;
                    await Task.Delay(1000, cancellationToken);

                    if (File.Exists(targetFile))
                    {
                        var secondCheck = new FileInfo(targetFile).LastWriteTime;
                        if (firstCheck == secondCheck)
                        {
                            return true; // 檔案完成寫入
                        }
                    }
                }

                elapsedSeconds = (i + 1) * pollIntervalMs / 1000;
                onProgress?.Report(elapsedSeconds);

                await Task.Delay(pollIntervalMs, cancellationToken);
            }

            return false; // 超時
        }

        /// <summary>
        /// 終止進程
        /// </summary>
        public void KillProcess(int processId, int timeoutMs = 5000)
        {
            if (processId <= 0)
                return;

            try
            {
                Process process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        // 強制終止（若上面的 Kill 無效）
                        process.Kill(true);
                    }
                }
            }
            catch (Exception ex)
            {
                // 進程可能已終止，忽略異常
                Debug.WriteLine($"終止進程失敗 ({processId}): {ex.Message}");
            }
        }

        /// <summary>
        /// 獲取進程的結束代碼
        /// </summary>
        public int GetProcessExitCode(int processId)
        {
            if (processId <= 0)
                return -1;

            try
            {
                Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return process.ExitCode;
                }
                return -1; // 進程仍在執行
            }
            catch
            {
                return -1;
            }
        }
    }
}
