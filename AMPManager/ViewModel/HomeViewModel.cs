using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AMPManager.Core;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OxyPlot;        // [추가]
using OxyPlot.Series; // [추가]
using OxyPlot.Axes;   // [추가]

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;
        private ApiService _apiService = new ApiService();
        private bool _isCameraRunning = false;

        // --- [추가] OxyPlot 그래프 모델 ---
        public PlotModel MyPlotModel { get; private set; }

        // --- 카메라 객체 ---
        private VideoCapture? _capture1;
        private VideoCapture? _capture2;

        private ImageSource? _cameraImage1;
        public ImageSource? CameraImage1 { get => _cameraImage1; set => SetProperty(ref _cameraImage1, value); }

        private ImageSource? _cameraImage2;
        public ImageSource? CameraImage2 { get => _cameraImage2; set => SetProperty(ref _cameraImage2, value); }

        // --- 데이터 ---
        private int _allocationCount = 1000;
        private int _currentComplete = 0;
        private double _defectRate = 0;

        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set => SetProperty(ref _currentComplete, value); }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public ObservableCollection<string> TimestampList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DefectMessageList { get; } = new ObservableCollection<string>();

        public HomeViewModel()
        {
            // 1. 그래프 초기화 (다크 테마 적용)
            MyPlotModel = CreatePlotModel();

            // 2. 타이머 설정
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            InitializeCamerasAsync();
        }

        // [추가] 디자인에 맞춘 그래프 생성 함수
        private PlotModel CreatePlotModel()
        {
            var model = new PlotModel();

            // 다크 테마 색상 정의
            var accentColor = OxyColor.Parse("#00C1D4");
            var textColor = OxyColor.Parse("#E0E0E0");
            var subtleTextColor = OxyColor.Parse("#B0B0B0");
            var borderColor = OxyColor.Parse("#4A4A5A");
            var panelBackground = OxyColor.Parse("#2F2F3D");

            model.Background = OxyColors.Transparent;
            model.TextColor = textColor;
            model.PlotAreaBorderColor = OxyColors.Transparent;
            model.PlotMargins = new OxyThickness(40, 10, 20, 30); // 여백 조정

            // 차트 시리즈 (영역 채우기 효과)
            var areaSeries = new AreaSeries
            {
                Color = accentColor,
                StrokeThickness = 3,
                Fill = OxyColor.FromAColor(50, accentColor), // 반투명 채우기
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = panelBackground,
                MarkerStroke = accentColor,
                MarkerStrokeThickness = 2
            };

            // 테스트 데이터
            areaSeries.Points.Add(new DataPoint(0, 10));
            areaSeries.Points.Add(new DataPoint(1, 40));
            areaSeries.Points.Add(new DataPoint(2, 35));
            areaSeries.Points.Add(new DataPoint(3, 70));
            areaSeries.Points.Add(new DataPoint(4, 50));
            areaSeries.Points.Add(new DataPoint(5, 80));

            model.Series.Add(areaSeries);

            // X축
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "시간(Time)",
                TextColor = subtleTextColor,
                AxislineColor = borderColor,
                TicklineColor = borderColor,
                MajorGridlineColor = borderColor,
                MajorGridlineStyle = LineStyle.Dot
            });

            // Y축
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "값(Value)",
                TextColor = subtleTextColor,
                AxislineColor = borderColor,
                TicklineColor = borderColor,
                MajorGridlineColor = borderColor,
                MajorGridlineStyle = LineStyle.Dot
            });

            return model;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // ... (기존 서버 통신 및 캡처 코드 유지 - 생략 없이 그대로 사용) ...
            // 기존 코드를 그대로 두시면 됩니다.
            // (여기에 원래 있던 _apiService.GetStatusAsync() 및 캡처 로직 유지)

            // 예시로 짧게 표현하자면:
            var data = await _apiService.GetStatusAsync();
            if (data != null)
            {
                AllocationCount = data.AllocationCount;
                CurrentComplete = data.CurrentComplete;
                DefectRate = data.DefectRate;
                // 로그 업데이트 등...
            }
            // 캡처 로직...
        }

        public void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
                _isCameraRunning = true;
                RunCameraLoop(_capture1, () => CameraImage1, img => CameraImage1 = img);
                RunCameraLoop(_capture2, () => CameraImage2, img => CameraImage2 = img);
            }
        }

        public void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _isCameraRunning = false;
            }
        }

        private async void InitializeCamerasAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _capture1 = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                    _capture2 = new VideoCapture(1, VideoCaptureAPIs.DSHOW);
                }
                catch { }
            });
        }

        private async void RunCameraLoop(VideoCapture? capture, Func<ImageSource?> getImage, Action<ImageSource?> updateImage)
        {
            // ... (기존 카메라 루프 코드 유지) ...
            // (메모리 최적화 로직 등 기존에 작성된 코드 그대로 사용)

            if (capture == null || !capture.IsOpened()) return;

            await Task.Run(() =>
            {
                using var frame = new Mat();
                while (_isCameraRunning)
                {
                    try
                    {
                        capture.Read(frame);
                        if (!frame.Empty())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                var writeableBitmap = getImage() as WriteableBitmap;
                                if (writeableBitmap == null || writeableBitmap.PixelWidth != frame.Width || writeableBitmap.PixelHeight != frame.Height)
                                {
                                    writeableBitmap = frame.ToWriteableBitmap();
                                    updateImage(writeableBitmap);
                                }
                                else
                                {
                                    WriteableBitmapConverter.ToWriteableBitmap(frame, writeableBitmap);
                                }
                            });
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(33);
                }
            });
            System.Windows.Application.Current.Dispatcher.Invoke(() => updateImage(null));
        }
    }
}