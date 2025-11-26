using System;
using System.Collections.ObjectModel;
using System.IO; // [필수] MemoryStream 사용
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

        // [수정] 웹소켓 서비스 2개 생성 (카메라 2대용)
        private WebSocketImageService _wsService1 = new WebSocketImageService();
        private WebSocketImageService _wsService2 = new WebSocketImageService();

        private bool _isCameraRunning = false;

        // --- 통합 그래프 모델 ---
        public PlotModel CombinedChartModel { get; private set; }

        // --- 카메라 객체 ---
        // (로컬 카메라는 웹소켓 사용 시 안 쓰지만 변수는 남겨둠)
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

        public int DefectCount { get => _defectCount; set => SetProperty(ref _defectCount, value); }
        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set => SetProperty(ref _currentComplete, value); }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public HomeViewModel()
        {
            // 1. 차트 초기화
            InitializeCombinedChart();

            // 2. 타이머 설정
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            InitializeCamerasAsync(); // (로컬 카메라 초기화 - 필요 없으면 주석 가능)

            // MQTT 수신 연결
            _mqttService.MessageReceived += OnMqttDataReceived;

            // [수정] 웹소켓 영상 수신 연결 (각각 다른 함수 연결)
            _wsService1.OnImageReceived += HandleImage1;
            _wsService2.OnImageReceived += HandleImage2;
        }

        // [추가] CAM 1 처리 함수
        private void HandleImage1(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CameraImage1 = ByteToBitmapImage(data);
            });
        }

        // [추가] CAM 2 처리 함수
        private void HandleImage2(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                CameraImage2 = ByteToBitmapImage(data);
            });
        }

        // (공통) 바이트 배열 -> 이미지 변환 헬퍼
        private BitmapImage? ByteToBitmapImage(byte[] data)
        {
            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    ms.Position = 0;
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch { return null; }
        }

        // (공통) 이미지 -> 바이트 배열 변환 헬퍼 (DB 저장용)
        private byte[]? ImageToByte(ImageSource? img)
        {
            if (img is BitmapImage bi) // 웹소켓 이미지는 BitmapImage
            {
                // BitmapImage는 원본 스트림이 닫혀있을 수 있어 다시 인코딩 필요
                // 하지만 성능상 받은 byte[]를 그대로 쓰는게 좋음. 
                // 여기선 편의상 화면 캡처 방식 대신 MQTT 수신 시점의 이미지를 쓴다고 가정.
                // (간단하게 구현하기 위해 아래 방식 사용)
                return null; // 실제 구현 시엔 원본 byte[]를 캐싱해두는 게 좋음
            }
            if (img is WriteableBitmap wb) // 로컬 카메라는 WriteableBitmap
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

        // 그래프 초기화 (기존 코드 유지)
        private void InitializeCombinedChart()
        {
            var textColor = OxyColor.Parse("#E0E0E0");
            var gridColor = OxyColor.Parse("#4A4A5A");

            CombinedChartModel = new PlotModel { Title = "" };
            CombinedChartModel.Background = OxyColors.Transparent;
            CombinedChartModel.PlotAreaBorderColor = OxyColors.Transparent;
            CombinedChartModel.TextColor = textColor;

            CombinedChartModel.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm:ss", AxislineColor = gridColor, TicklineColor = gridColor, TextColor = textColor, MajorGridlineStyle = LineStyle.Dot, MajorGridlineColor = gridColor });
            CombinedChartModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Key = "CountAxis", Title = "검사량", AxislineColor = OxyColor.Parse("#00C1D4"), TextColor = OxyColor.Parse("#00C1D4"), Minimum = 0 });
            CombinedChartModel.Axes.Add(new LinearAxis { Position = AxisPosition.Right, Key = "DefectAxis", Title = "불량 개수", AxislineColor = OxyColor.Parse("#FF5252"), TextColor = OxyColor.Parse("#FF5252"), Minimum = 0 });

            CombinedChartModel.Series.Add(new LineSeries { Title = "검사량", Color = OxyColor.Parse("#00C1D4"), StrokeThickness = 2, YAxisKey = "CountAxis" });
            CombinedChartModel.Series.Add(new LineSeries { Title = "불량 개수", Color = OxyColor.Parse("#FF5252"), StrokeThickness = 2, YAxisKey = "DefectAxis" });
        }

        private void UpdateChartData()
        {
            DateTime now = DateTime.Now;
            if (CombinedChartModel.Series[0] is LineSeries countSeries)
            {
                countSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), CurrentComplete));
                if (countSeries.Points.Count > 50) countSeries.Points.RemoveAt(0);
            }
            if (CombinedChartModel.Series[1] is LineSeries defectSeries)
            {
                defectSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(now), DefectCount));
                if (defectSeries.Points.Count > 50) defectSeries.Points.RemoveAt(0);
            }
            CombinedChartModel.InvalidatePlot(true);
        }

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

                    // (주의) 웹소켓 이미지를 다시 바이트로 바꾸는건 비효율적일 수 있어
                    // 일단은 null로 저장하거나, 별도 변수에 저장해둔 최신 이미지를 써야 합니다.
                    // 여기선 편의상 생략 (null 저장)
                    byte[]? img1Data = null;
                    byte[]? img2Data = null;

                    _dbManager.InsertMeasurement(pid, nowTime, isDefect, img1Data, img2Data);

                    CurrentComplete++;
                    if (isDefect) DefectCount++;
                    if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;
                }
                catch { }
            });
        }

        public async void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                await _mqttService.ConnectAsync();
                await _mqttService.SendCommandAsync("START");

                // [수정] 메인 PC IP 주소로 2개 연결 (8765, 8766)
                // ★ 여기에 메인 PC IP를 꼭 적으세요! (예: 192.168.0.10)
                string mainPcIp = "192.168.0.88";

                await _wsService1.ConnectAsync($"ws://{mainPcIp}:8765");
                await _wsService2.ConnectAsync($"ws://{mainPcIp}:8766");

                _timer.Start();
            }
        }

        public async void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                await _mqttService.SendCommandAsync("STOP");

                // [수정] 둘 다 연결 종료
                await _wsService1.DisconnectAsync();
                await _wsService2.DisconnectAsync();

                _timer.Stop();
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // [테스트용 더미 데이터]
            if (true)
            {
                bool isBad = new Random().Next(0, 10) < 2;
                int randomPid = new Random().Next(1, 4);
                string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                _dbManager.InsertMeasurement(randomPid, nowTime, isBad, null, null);

                CurrentComplete++;
                if (isBad) DefectCount++;
                if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;
            }

            UpdateChartData();
        }

        private async void InitializeCamerasAsync() { await Task.Run(() => { try { _capture1 = new VideoCapture(0, VideoCaptureAPIs.DSHOW); _capture2 = new VideoCapture(1, VideoCaptureAPIs.DSHOW); } catch { } }); }

        private async void RunCameraLoop(VideoCapture? capture, Func<ImageSource?> getImage, Action<ImageSource?> updateImage)
        {
            // 로컬 카메라 로직 (웹소켓 사용 시 작동 안 함)
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