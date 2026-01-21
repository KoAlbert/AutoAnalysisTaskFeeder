using System;
using System.IO;
using System.Text.Json;

namespace AutoAnalysisTaskFeeder.Services
{
    /// <summary>
    /// 使用者設定儲存與載入
    /// </summary>
    public class UserSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoAnalysisTaskFeeder",
            "user_settings.json"
        );

        /// <summary>
        /// 上次選擇的 AnalysisTask 目錄
        /// </summary>
        public string LastAnalysisTaskPath { get; set; } = "";

        /// <summary>
        /// 上次選擇的 QKBqPCRAnalysis.exe 檔案路徑
        /// </summary>
        public string LastPcrAnalysisExePath { get; set; } = "";

        /// <summary>
        /// 上次選擇的實驗資料目錄
        /// </summary>
        public string LastExperimentDataPath { get; set; } = "";

        /// <summary>
        /// 載入使用者設定
        /// </summary>
        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    return settings ?? new UserSettings();
                }
            }
            catch
            {
                // 讀取失敗時返回預設設定
            }

            return new UserSettings();
        }

        /// <summary>
        /// 儲存使用者設定
        /// </summary>
        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // 儲存失敗時忽略（不影響主要功能）
            }
        }
    }
}
