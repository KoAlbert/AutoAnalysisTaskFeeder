using System.Windows;
using AutoAnalysisTaskFeeder.Services;
using AutoAnalysisTaskFeeder.ViewModels;

namespace AutoAnalysisTaskFeeder
{
    public partial class App : Application
    {
        public static MainViewModel? MainViewModelInstance { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize services
            var logService = new LogService();
            var iniService = new IniService();
            var folderScanService = new FolderScanService(iniService, logService);
            var processRunner = new ProcessRunner();

            // Create MainViewModel and store for use by MainWindow
            MainViewModelInstance = new MainViewModel(folderScanService, iniService, processRunner, logService);
        }
    }
}
