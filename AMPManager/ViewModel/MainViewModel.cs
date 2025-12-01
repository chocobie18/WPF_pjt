using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private BaseViewModel? _currentViewModel;
        private readonly Dictionary<string, BaseViewModel> _viewModels;

        // --- 가동 시간 타이머 ---
        private DispatcherTimer _opTimer;
        private TimeSpan _opDuration;
        private string _operationTimeDisplay = "00:00:00";

        public string OperationTimeDisplay
        {
            get => _operationTimeDisplay;
            set => SetProperty(ref _operationTimeDisplay, value);
        }

        // --- 접속자 정보 ---
        public User CurrentUser { get; }
        public string UserRoleDisplay => CurrentUser.IsAdmin ? "👤 관리자 (Admin)" : "👤 일반 사원 (User)";
        public Visibility StatTabVisibility => CurrentUser.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        // --- 커맨드 ---
        public ICommand NavigateCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }

        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public MainViewModel(User user)
        {
            CurrentUser = user;

            // 1. 타이머 초기화 (1초마다 실행)
            _opTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _opTimer.Tick += (s, e) =>
            {
                _opDuration = _opDuration.Add(TimeSpan.FromSeconds(1));
                OperationTimeDisplay = _opDuration.ToString(@"hh\:mm\:ss");
            };

            // 2. 뷰모델 생성
            var homeVM = new HomeViewModel();
            var logVM = new LogViewModel();
            var statVM = new StatisticsViewModel();

            _viewModels = new Dictionary<string, BaseViewModel>
            {
                { "Main", homeVM },
                { "Log", logVM },
                { "Statistics", statVM }
            };

            // 3. 네비게이션
            NavigateCommand = new RelayCommand(o =>
            {
                if (o is string p && _viewModels.ContainsKey(p)) CurrentViewModel = _viewModels[p];
            });

            // 4. [시스템 시작] 
            StartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.StartSimulation();
                    CurrentViewModel = home;

                    // 타이머 시작 (멈춰있을 때만)
                    if (!_opTimer.IsEnabled) _opTimer.Start();
                }
            });

            // 5. [재가동] (수정됨: 타이머 초기화 로직 삭제 -> 이어서 가동)
            RestartCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.RestartSimulation();

                    // [수정] 시간을 0으로 만드는 코드를 지웠습니다. 
                    // 멈춘 시간부터 이어서 다시 시작합니다.
                    if (!_opTimer.IsEnabled) _opTimer.Start();
                }
            });

            // 6. [정지] 
            StopCommand = new RelayCommand(o =>
            {
                if (_viewModels["Main"] is HomeViewModel home)
                {
                    home.StopSimulation();

                    // 타이머 멈춤
                    if (_opTimer.IsEnabled) _opTimer.Stop();
                }
            });

            CurrentViewModel = _viewModels["Main"];
        }
    }
}