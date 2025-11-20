using AMPManager.Core;
using System.Collections.Generic;
using System.Windows.Input;

namespace AMPManager.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private BaseViewModel? _currentViewModel;
        private readonly Dictionary<string, BaseViewModel> _viewModels;

        public ICommand NavigateCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public MainViewModel()
        {
            // 1. 화면들 미리 생성
            var homeVM = new HomeViewModel();
            var logVM = new LogViewModel();
            var settingsVM = new SettingsViewModel();

            _viewModels = new Dictionary<string, BaseViewModel>
            {
                { "Main", homeVM }, { "Log", logVM }, { "Settings", settingsVM }
            };

            // 2. 네비게이션
            NavigateCommand = new RelayCommand(o =>
            {
                if (o is string p && _viewModels.ContainsKey(p)) CurrentViewModel = _viewModels[p];
            });

            // 3. 시작 버튼 (Home화면 타이머 시작)
            StartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home) { home.StartSimulation(); CurrentViewModel = home; }
            });

            // 4. 정지 버튼
            StopCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home) home.StopSimulation();
            });

            CurrentViewModel = _viewModels["Main"];
        }
    }
}