using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AMPManager.Core;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OxyPlot;        // [필수] PlotModel, OxyColor 사용
using OxyPlot.Series; // [필수] PieSeries, PieSlice 사용

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;
        private ApiService _apiService = new ApiService();
        private bool _isCameraRunning = false;

        // --- 원형 그래프 모델 2개 ---
        public PlotModel WorkPieModel { get; private set; }  // 작업 진행률
        public PlotModel DefectPieModel { get; private set; } // 불량률

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

        public int AllocationPercent => _allocationCount == 0 ? 0 : (int)((double)_currentComplete / _allocationCount * 100);

        public int AllocationCount
        {
            get => _allocationCount;
            set { if (SetProperty(ref _allocationCount, value)) UpdateCharts(); }
        }

        public int CurrentComplete
        {
            get => _currentComplete;
            set
            {
                if (SetProperty(ref _currentComplete, value))
                {
                    OnPropertyChanged(nameof(AllocationPercent));
                    UpdateCharts();
                }
            }
        }

        public double DefectRate
        {
            get => _defectRate;
            set { if (SetProperty(ref _defectRate, value)) UpdateCharts(); }
        }

        public ObservableCollection<string> TimestampList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DefectMessageList { get; } = new ObservableCollection<string>();

        public HomeViewModel()
        {
            // 1. 차트 초기화 (도넛 모양)
            WorkPieModel = CreateDonutModel();
            DefectPieModel = CreateDonutModel();

            // 2. 초기 데이터 설정
            UpdateCharts();

            // 3. 타이머 설정
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            InitializeCamerasAsync();
        }

        // 도넛 차트 기본 설정 함수
        private PlotModel CreateDonutModel()
        {
            var model = new PlotModel { Title = null };
            model.Background = OxyColors.Transparent;
            model.PlotAreaBorderColor = OxyColors.Transparent;

            var series = new PieSeries
            {
                StrokeThickness = 0,
                AngleSpan = 360,
                StartAngle = -90,
                    InnerRadius = 0.6, // 도넛 모양 (0.0 ~ 1.0)
                OutsideLabelFormat = null, // 바깥 라벨 숨김
                InsideLabelFormat = null,  // 안쪽 라벨 숨김
                TickHorizontalLength = 0,
                TickRadialLength = 0
            };

            model.Series.Add(series);
            return model;
        }

        // 데이터가 변경될 때 차트 갱신
        private void UpdateCharts()
        {
            // 1. 작업 진행률 갱신
            if (WorkPieModel.Series.Count > 0 && WorkPieModel.Series[0] is PieSeries workSeries)
            {
                workSeries.Slices.Clear();
                double remaining = Math.Max(0, AllocationCount - CurrentComplete);

                // 완료 (민트색)
                workSeries.Slices.Add(new PieSlice("완료", CurrentComplete) { Fill = OxyColor.Parse("#00C1D4") });
                // 잔여 (어두운 회색)
                workSeries.Slices.Add(new PieSlice("잔여", remaining) { Fill = OxyColor.Parse("#404050") });

                WorkPieModel.InvalidatePlot(true);
            }

            // 2. 불량률 갱신
            if (DefectPieModel.Series.Count > 0 && DefectPieModel.Series[0] is PieSeries defectSeries)
            {
                defectSeries.Slices.Clear();

                // 불량 (빨간색)
                defectSeries.Slices.Add(new PieSlice("불량", DefectRate) { Fill = OxyColor.Parse("#FF5252") });
                // 정상 (배경색과 비슷한 어두운 색)
                defectSeries.Slices.Add(new PieSlice("정상", 100.0 - DefectRate) { Fill = OxyColor.Parse("#2F2F3D") });

                DefectPieModel.InvalidatePlot(true);
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            var data = await _apiService.GetStatusAsync();
            if (data != null)
            {
                AllocationCount = data.AllocationCount;
                CurrentComplete = data.CurrentComplete;
                DefectRate = data.DefectRate;

                TimestampList.Clear();
                foreach (var log in data.Logs) TimestampList.Add(log);
            }

            // 캡처 로직
            try
            {
                var window = System.Windows.Application.Current.MainWindow;
                if (window != null && window.ActualWidth > 0 && window.ActualHeight > 0)
                {
                    int w = (int)window.ActualWidth;
                    int h = (int)window.ActualHeight;
                    RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(window);
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        encoder.Save(ms);
                        await _apiService.SendImageAsync(ms.ToArray());
                    }
                }
            }
            catch { }
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