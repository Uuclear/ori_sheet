using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using RingKnifeDetector.Models;
using RingKnifeDetector.Services;

namespace RingKnifeDetector.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CalculationService _calculationService;
        private readonly LimisService _limisService;
        private readonly ExcelExportService _excelExportService;
        private readonly DraftService _draftService;
        private readonly SettingsService _settingsService;

        private string _currentView = "Tasks";
        private string _statusText = "就绪";

        public MainViewModel()
        {
            _calculationService = new CalculationService();
            _limisService = new LimisService();
            _excelExportService = new ExcelExportService();
            _draftService = new DraftService();
            _settingsService = new SettingsService();

            Tasks = new ObservableCollection<TaskItem>();
            Samples = new ObservableCollection<RingKnifeSample>();
        }

        public string CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ObservableCollection<TaskItem> Tasks { get; }
        public ObservableCollection<RingKnifeSample> Samples { get; }
    }
}