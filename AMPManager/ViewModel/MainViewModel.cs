using AMPManager.Core;
using System.Collections.Generic;
using System.Windows.Input;

namespace AMPManager.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private BaseViewModel? _currentViewModel;

        // [추가] 현재 선택된 뷰의 이름 (Main, Log, Settings)
        private string _currentViewName = "Main";

        private readonly Dictionary<string, BaseViewModel> _viewModels;

        public ICommand NavigateCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        // [추가] XAML에서 버튼 스타일을 바꾸기 위해 이 속성을 바라봅니다.
        public string CurrentViewName
        {
            get => _currentViewName;
            set => SetProperty(ref _currentViewName, value);
        }

        public MainViewModel()
        {
            var homeVM = new HomeViewModel();
            var logVM = new LogViewModel();
            var settingsVM = new SettingsViewModel();

            _viewModels = new Dictionary<string, BaseViewModel>
            {
                { "Main", homeVM }, { "Log", logVM }, { "Settings", settingsVM }
            };

            NavigateCommand = new RelayCommand(o =>
            {
                if (o is string p && _viewModels.ContainsKey(p))
                {
                    CurrentViewModel = _viewModels[p];
                    CurrentViewName = p; // [추가] 탭 변경 시 이름도 업데이트
                }
            });

            StartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home) { home.StartSimulation(); CurrentViewModel = home; CurrentViewName = "Main"; }
            });

            StopCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home) home.StopSimulation();
            });

            CurrentViewModel = _viewModels["Main"];
        }
    }
}