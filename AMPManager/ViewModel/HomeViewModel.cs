using System;
using System.Collections.ObjectModel;
using System.IO; // [필수] MemoryStream 사용을 위해 추가
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AMPManager.Core;
using AMPManager.Model;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;
        private ApiService _apiService = new ApiService();
        private DatabaseManager _dbManager = new DatabaseManager();
        private MqttService _mqttService = new MqttService();
        private bool _isCameraRunning = false;

        // --- 통합 그래프 모델 ---
        public PlotModel CombinedChartModel { get; private set; }

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
        private int _defectCount = 0;

        // 불량 개수 (화면 표시용)
        public int DefectCount { get => _defectCount; set => SetProperty(ref _defectCount, value); }
        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set => SetProperty(ref _currentComplete, value); }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public HomeViewModel()
        {
            // 1. 통합 그래프 초기화
            InitializeCombinedChart();

            // 2. 타이머 설정 (0.5초 간격)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            InitializeCamerasAsync();

            // MQTT 메시지 수신 이벤트 연결
            _mqttService.MessageReceived += OnMqttDataReceived;
        }

        private void InitializeCombinedChart()
        {
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            CombinedChartModel = new PlotModel { Title = "" };
            CombinedChartModel.Background = OxyColors.Transparent;
            CombinedChartModel.PlotAreaBorderColor = OxyColors.Transparent;
            CombinedChartModel.TextColor = textColor;

            // X축 (시간)
            CombinedChartModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                AxislineColor = gridColor,
                TicklineColor = gridColor,
                TextColor = textColor,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = gridColor
            });

            // Y축 1 (왼쪽): 검사량 (Count)
            CombinedChartModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "CountAxis", // 왼쪽 축 식별자
                Title = "검사량",
                AxislineColor = OxyColor.Parse("#00C1D4"), // 민트색
                TextColor = OxyColor.Parse("#00C1D4"),
                Minimum = 0
            });

            // Y축 2 (오른쪽): 불량 개수 (Count)
            CombinedChartModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "DefectAxis", // 오른쪽 축 식별자
                Title = "불량 개수",
                AxislineColor = OxyColor.Parse("#FF5252"), // 빨간색
                TextColor = OxyColor.Parse("#FF5252"),
                Minimum = 0
            });

            // 시리즈 1: 검사량 (민트색 선, 왼쪽 축 사용)
            CombinedChartModel.Series.Add(new LineSeries
            {
                Title = "검사량",
                Color = OxyColor.Parse("#00C1D4"),
                StrokeThickness = 2,
                YAxisKey = "CountAxis"
            });

            // 시리즈 2: 불량 개수 (빨간색 선, 오른쪽 축 사용)
            CombinedChartModel.Series.Add(new LineSeries
            {
                Title = "불량 개수",
                Color = OxyColor.Parse("#FF5252"),
                StrokeThickness = 2,
                YAxisKey = "DefectAxis"
            });
        }

        private void UpdateChartData()
        {
            DateTime now = DateTime.Now;

            // 1. 검사량 그래프 갱신
            if (CombinedChartModel.Series[0] is LineSeries countSeries)
            {
                countSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), CurrentComplete));
                // 데이터가 너무 많아지지 않게 최근 50개만 유지
                if (countSeries.Points.Count > 50) countSeries.Points.RemoveAt(0);
            }

            // 2. 불량 개수 그래프 갱신
            if (CombinedChartModel.Series[1] is LineSeries defectSeries)
            {
                defectSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), DefectCount));
                if (defectSeries.Points.Count > 50) defectSeries.Points.RemoveAt(0);
            }

            CombinedChartModel.InvalidatePlot(true);
        }

        // [추가] 이미지 소스를 바이트 배열로 변환하는 함수
        private byte[]? ImageToByte(ImageSource? img)
        {
            if (img is WriteableBitmap wb)
            {
                try
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(wb));
                        encoder.Save(ms);
                        return ms.ToArray();
                    }
                }
                catch { return null; }
            }
            return null;
        }

        // 실제 MQTT 데이터 수신 시 처리
        private void OnMqttDataReceived(string jsonPayload)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    dynamic data = JsonConvert.DeserializeObject(jsonPayload);
                    int pid = data.pid;
                    string resultStr = data.result;
                    bool isDefect = (resultStr == "NG");

                    string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    // [수정] 현재 카메라 화면을 캡처해서 저장
                    byte[]? img1Data = ImageToByte(CameraImage1);
                    byte[]? img2Data = ImageToByte(CameraImage2);

                    // DB 저장 (이미지 포함)
                    _dbManager.InsertMeasurement(pid, nowTime, isDefect, img1Data, img2Data);

                    // 화면 수치 갱신
                    CurrentComplete++;
                    if (isDefect) DefectCount++;

                    // 불량률 계산
                    if (CurrentComplete > 0)
                    {
                        DefectRate = (double)DefectCount / CurrentComplete * 100.0;
                    }
                }
                catch { }
            });
        }

        public async void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                // MQTT 연결 및 시작 명령
                await _mqttService.ConnectAsync();
                await _mqttService.SendCommandAsync("START");

                // 타이머 및 카메라 시작
                _timer.Start();
                _isCameraRunning = true;
                RunCameraLoop(_capture1, () => CameraImage1, img => CameraImage1 = img);
                RunCameraLoop(_capture2, () => CameraImage2, img => CameraImage2 = img);
            }
        }

        public async void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                await _mqttService.SendCommandAsync("STOP");
                _timer.Stop();
                _isCameraRunning = false;
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // [테스트용] 더미 데이터 생성 (나중에 실제 장비 연결 시 이 if문 블록 삭제)
            if (true)
            {
                // 1. 랜덤 불량 여부 (20% 확률)
                bool isBad = new Random().Next(0, 10) < 2;
                int randomPid = new Random().Next(1, 4);

                // 2. DB 저장 [수정됨: 이미지 포함]
                string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // 현재 카메라 화면 캡처
                byte[]? img1Data = ImageToByte(CameraImage1);
                byte[]? img2Data = ImageToByte(CameraImage2);

                _dbManager.InsertMeasurement(randomPid, nowTime, isBad, img1Data, img2Data);

                // 3. 화면 값 갱신
                CurrentComplete++;
                if (isBad) DefectCount++;

                if (CurrentComplete > 0)
                {
                    DefectRate = (double)DefectCount / CurrentComplete * 100.0;
                }
            }

            // 그래프 업데이트
            UpdateChartData();

            // 카메라 캡처 및 전송 로직 (기존 유지)
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