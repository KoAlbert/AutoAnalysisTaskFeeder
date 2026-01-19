using System;
using System.IO;
using System.Text;
using AutoAnalysisTaskFeeder.Models;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// INI 檔案服務實現
    /// </summary>
    public class IniService : IIniService
    {
        /// <summary>
        /// 根據 TaskItem 產生 NewAnalysis.ini 內容
        /// </summary>
        public string GenerateIniContent(TaskItem task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            var sb = new StringBuilder();

            // 產生 INI 內容
            sb.AppendLine("[Information]");
            sb.AppendLine("Enabled=1");
            sb.AppendLine($"TotalCycle={task.TotalCycle}");
            sb.AppendLine("Flag=0");
            sb.AppendLine($"TotalChip={task.TotalChip}");
            sb.AppendLine($"Path={task.FolderPath}");
            sb.AppendLine($"User={GetSafeString(task.UserName)}");
            sb.AppendLine($"Filter={GetSafeString(task.Filter)}");

            return sb.ToString();
        }

        /// <summary>
        /// 將 INI 內容寫入檔案
        /// </summary>
        public void WriteIniFile(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("檔案路徑不能為空", nameof(filePath));

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            try
            {
                // 確保目錄存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // 使用 UTF-8 編碼寫入
                File.WriteAllText(filePath, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new IOException($"寫入 INI 檔案失敗: {filePath}", ex);
            }
        }

        /// <summary>
        /// 標準化篩選器字串（移除尾端多餘冒號）
        /// </summary>
        public string NormalizeFilter(string rawFilter)
        {
            if (string.IsNullOrEmpty(rawFilter))
                return "";

            string result = rawFilter.TrimEnd();

            // 移除尾端連續的冒號，保留最後一個
            while (result.EndsWith("::"))
            {
                result = result.Substring(0, result.Length - 1);
            }

            return result;
        }

        private string GetSafeString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value;
        }
    }
}
