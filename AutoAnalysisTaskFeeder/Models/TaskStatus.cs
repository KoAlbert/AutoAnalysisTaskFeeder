namespace AutoAnalysisTaskFeeder.Models
{
    /// <summary>
    /// 任務狀態列舉
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>待處理</summary>
        Pending = 0,

        /// <summary>正在產生 INI</summary>
        Generating = 1,

        /// <summary>INI 已產生</summary>
        IniGenerated = 2,

        /// <summary>正在執行分析</summary>
        Running = 3,

        /// <summary>分析完成</summary>
        Completed = 4,

        /// <summary>分析失敗</summary>
        Failed = 5,

        /// <summary>使用者取消</summary>
        Cancelled = 6
    }
}
