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
            // [수정] SettingsViewModel -> StatisticsViewModel로 변경
            var homeVM = new HomeViewModel();
            var logVM = new LogViewModel();
            var statVM = new StatisticsViewModel(); // 여기!

            _viewModels = new Dictionary<string, BaseViewModel>
            {
                { "Main", homeVM },
                { "Log", logVM },
                { "Statistics", statVM } // 키 값을 "Settings"에서 "Statistics"로 변경
            };

            // 네비게이션 커맨드
            NavigateCommand = new RelayCommand(o =>
            {
                if (o is string p && _viewModels.ContainsKey(p)) CurrentViewModel = _viewModels[p];
            });

            // ... (StartCommand, StopCommand 기존 코드 유지) ...
            StartCommand = new RelayCommand(o => { if (_viewModels["Main"] is HomeViewModel home) { home.StartSimulation(); CurrentViewModel = home; } });
            StopCommand = new RelayCommand(o => { if (_viewModels["Main"] is HomeViewModel home) home.StopSimulation(); });

            CurrentViewModel = _viewModels["Main"];
        }
    }
}