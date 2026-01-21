using System;
using System.Windows;
using AutoAnalysisTaskFeeder.ViewModels;

namespace AutoAnalysisTaskFeeder.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Get MainViewModel from App static instance
            DataContext = App.MainViewModelInstance ?? throw new InvalidOperationException("MainViewModel¥¼ªì©l¤Æ");
        }
    }
}
