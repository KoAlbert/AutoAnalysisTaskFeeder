using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoAnalysisTaskFeeder.Models
{
    /// <summary>
    /// 代表單一實驗任務項目
    /// </summary>
    public class TaskItem : INotifyPropertyChanged
    {
        private int _item;
        private string _folderName;
        private string _folderPath;
        private string _machine;
        private string _app;
        private string _softwareVersion;
        private string _userName;
        private int _totalCycle;
        private int _totalChip;
        private string _filter;
        
        // 內部狀態欄位
        private TaskStatus _status;
        private string _errorMessage;
        private string _iniFilePath;
        private DateTime _generatedTime;
        private DateTime _completedTime;
        private int _processId;

        public event PropertyChangedEventHandler PropertyChanged;

        // 顯示於 UI 的欄位

        /// <summary>項目序號</summary>
        public int Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        /// <summary>實驗資料夾名稱</summary>
        public string FolderName
        {
            get => _folderName;
            set => SetProperty(ref _folderName, value);
        }

        /// <summary>實驗資料夾完整路徑</summary>
        public string FolderPath
        {
            get => _folderPath;
            set => SetProperty(ref _folderPath, value);
        }

        /// <summary>機器代碼</summary>
        public string Machine
        {
            get => _machine;
            set => SetProperty(ref _machine, value);
        }

        /// <summary>應用程式名稱</summary>
        public string App
        {
            get => _app;
            set => SetProperty(ref _app, value);
        }

        /// <summary>軟體版本</summary>
        public string SoftwareVersion
        {
            get => _softwareVersion;
            set => SetProperty(ref _softwareVersion, value);
        }

        /// <summary>使用者名稱</summary>
        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        /// <summary>總循環數</summary>
        public int TotalCycle
        {
            get => _totalCycle;
            set => SetProperty(ref _totalCycle, value);
        }

        /// <summary>總晶片數</summary>
        public int TotalChip
        {
            get => _totalChip;
            set => SetProperty(ref _totalChip, value);
        }

        /// <summary>篩選器</summary>
        public string Filter
        {
            get => _filter;
            set => SetProperty(ref _filter, value);
        }

        // 內部狀態欄位（不顯示於 UI）

        /// <summary>任務狀態</summary>
        public TaskStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>錯誤訊息</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>INI 檔案路徑</summary>
        public string IniFilePath
        {
            get => _iniFilePath;
            set => SetProperty(ref _iniFilePath, value);
        }

        /// <summary>INI 產生時間</summary>
        public DateTime GeneratedTime
        {
            get => _generatedTime;
            set => SetProperty(ref _generatedTime, value);
        }

        /// <summary>任務完成時間</summary>
        public DateTime CompletedTime
        {
            get => _completedTime;
            set => SetProperty(ref _completedTime, value);
        }

        /// <summary>外部程式進程 ID</summary>
        public int ProcessId
        {
            get => _processId;
            set => SetProperty(ref _processId, value);
        }

        protected void SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(propertyName);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
