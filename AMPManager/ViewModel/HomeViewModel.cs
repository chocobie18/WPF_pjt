using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AMPManager.Core;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OxyPlot;
using OxyPlot.Series;

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;
        private ApiService _apiService = new ApiService();
        private bool _isCameraRunning = false;

        // --- 원형 그래프 모델 ---
        public PlotModel WorkPieModel { get; private set; }
        public PlotModel DefectPieModel { get; private set; }

        // --- 카메라 객체 ---
        private VideoCapture? _capture1;
        private VideoCapture? _capture2;
        private ImageSource? _cameraImage1;
        private ImageSource? _cameraImage2;

        public ImageSource? CameraImage1 { get => _cameraImage1; set => SetProperty(ref _cameraImage1, value); }
        public ImageSource? CameraImage2 { get => _cameraImage2; set => SetProperty(ref _cameraImage2, value); }

        // --- 데이터 ---
        private int _allocationCount = 1000;
        private int _currentComplete = 0;
        private double _defectRate = 0;

        public double AllocationPercent => _allocationCount == 0 ? 0.0 : (double)_currentComplete / _allocationCount * 100.0;

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
            // 1. 차트 초기화
            WorkPieModel = CreateDonutModel();
            DefectPieModel = CreateDonutModel();

            // 2. 초기 데이터 설정
            UpdateCharts();

            // 3. 타이머
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            InitializeCamerasAsync();
        }

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
                InnerDiameter = 0.6,
                OutsideLabelFormat = null,
                InsideLabelFormat = null,
                TickHorizontalLength = 0,
                TickRadialLength = 0
            };

            model.Series.Add(series);
            return model;
        }

        private void UpdateCharts()
        {
            // 1. 작업 진행률 갱신
            if (WorkPieModel.Series.Count > 0 && WorkPieModel.Series[0] is PieSeries workSeries)
            {
                workSeries.Slices.Clear();
                double remaining = Math.Max(0, AllocationCount - CurrentComplete);

                // 완료 (민트색), 잔여 (회색)
                workSeries.Slices.Add(new PieSlice("완료", CurrentComplete) { Fill = OxyColor.Parse("#00C1D4") });
                workSeries.Slices.Add(new PieSlice("잔여", remaining) { Fill = OxyColor.Parse("#404050") });

                WorkPieModel.InvalidatePlot(true);
            }

            // 2. 불량률 갱신 (여기를 수정했습니다!)
            if (DefectPieModel.Series.Count > 0 && DefectPieModel.Series[0] is PieSeries defectSeries)
            {
                defectSeries.Slices.Clear();

                // 0~100 사이로 값 제한
                double safeDefectRate = Math.Max(0.0, Math.Min(100.0, DefectRate));
                double normalRate = 100.0 - safeDefectRate;

                // 불량 (빨간색)
                defectSeries.Slices.Add(new PieSlice("불량", safeDefectRate) { Fill = OxyColor.Parse("#FF5252") });

                // [수정됨] 정상 부분을 배경색(#2F2F3D)에서 -> 작업 진행률과 똑같은 회색(#404050)으로 변경!
                // 이제 불량률 그래프도 회색 베이스 원이 보일 겁니다.
                defectSeries.Slices.Add(new PieSlice("정상", normalRate) { Fill = OxyColor.Parse("#404050") });

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
                    int w = (int)window.ActualWidth; int h = (int)window.ActualHeight;
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

        public void StartSimulation() { if (!_timer.IsEnabled) { _timer.Start(); _isCameraRunning = true; RunCameraLoop(_capture1, () => CameraImage1, img => CameraImage1 = img); RunCameraLoop(_capture2, () => CameraImage2, img => CameraImage2 = img); } }
        public void StopSimulation() { if (_timer.IsEnabled) { _timer.Stop(); _isCameraRunning = false; } }

        private async void InitializeCamerasAsync() { await Task.Run(() => { try { _capture1 = new VideoCapture(0, VideoCaptureAPIs.DSHOW); _capture2 = new VideoCapture(1, VideoCaptureAPIs.DSHOW); } catch { } }); }

        private async void RunCameraLoop(VideoCapture? capture, Func<ImageSource?> getImage, Action<ImageSource?> updateImage)
        {
            if (capture == null || !capture.IsOpened()) return;
            await Task.Run(() => {
                using var frame = new Mat();
                while (_isCameraRunning)
                {
                    try
                    {
                        capture.Read(frame);
                        if (!frame.Empty())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                                var wb = getImage() as WriteableBitmap;
                                if (wb == null || wb.PixelWidth != frame.Width || wb.PixelHeight != frame.Height) updateImage(frame.ToWriteableBitmap());
                                else WriteableBitmapConverter.ToWriteableBitmap(frame, wb);
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