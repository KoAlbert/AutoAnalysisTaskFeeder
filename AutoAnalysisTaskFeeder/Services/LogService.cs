using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// 日誌服務實現
    /// </summary>
    public class LogService : ILogService
    {
        private readonly List<string> _logs = new List<string>();
        private readonly int _maxLines = 5000;
        private readonly int _trimLines = 500;

        public event LogChangedEventHandler? LogChanged;

        /// <summary>
        /// 記錄資訊級日誌
        /// </summary>
        public void LogInfo(string message)
        {
            AppendLog("INFO", message);
        }

        /// <summary>
        /// 記錄警告級日誌
        /// </summary>
        public void LogWarn(string message)
        {
            AppendLog("WARN", message);
        }

        /// <summary>
        /// 記錄錯誤級日誌
        /// </summary>
        public void LogError(string message)
        {
            AppendLog("ERROR", message);
        }

        /// <summary>
        /// 獲取所有日誌文本
        /// </summary>
        public string GetAllLogs()
        {
            lock (_logs)
            {
                return string.Join(Environment.NewLine, _logs);
            }
        }

        /// <summary>
        /// 清空日誌
        /// </summary>
        public void ClearLogs()
        {
            lock (_logs)
            {
                _logs.Clear();
                LogChanged?.Invoke(GetAllLogs());
            }
        }

        private void AppendLog(string level, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] [{level}] {message}";

            lock (_logs)
            {
                _logs.Add(logEntry);

                // 如果超過容量上限，刪除最舊的 500 行
                if (_logs.Count > _maxLines)
                {
                    _logs.RemoveRange(0, _trimLines);
                    string truncateMsg = DateTime.Now.ToString("HH:mm:ss.fff");
                    _logs.Insert(0, $"[{truncateMsg}] [INFO] [Log truncated: oldest {_trimLines} lines removed]");
                }

                LogChanged?.Invoke(GetAllLogs());
            }
        }
    }
}
