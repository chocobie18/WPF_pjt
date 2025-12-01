using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
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

        // [수정] API 서비스 사용
        private ApiService _apiService = new ApiService();
        private DatabaseManager _dbManager = new DatabaseManager();
        private MqttService _mqttService = new MqttService(); // 데이터 수신용(Listening)으로 유지

        private WebSocketImageService _wsService1 = new WebSocketImageService();
        private WebSocketImageService _wsService2 = new WebSocketImageService();

        private bool _isCameraRunning = false;

        public PlotModel CombinedChartModel { get; private set; }

        private ImageSource? _cameraImage1;
        private ImageSource? _cameraImage2;

        public ImageSource? CameraImage1 { get => _cameraImage1; set => SetProperty(ref _cameraImage1, value); }
        public ImageSource? CameraImage2 { get => _cameraImage2; set => SetProperty(ref _cameraImage2, value); }

        private int _allocationCount = 1000;
        private int _currentComplete = 0;
        private double _defectRate = 0;
        private int _defectCount = 0;

        public int DefectCount { get => _defectCount; set => SetProperty(ref _defectCount, value); }
        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set => SetProperty(ref _currentComplete, value); }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public ICommand TestCommand { get; }

        public HomeViewModel()
        {
            InitializeCombinedChart();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;

            // 로컬 카메라 초기화 코드 삭제 (서버 영상 사용)

            // 실시간 판정 결과 수신 (Server -> MQTT -> WPF)
            _mqttService.MessageReceived += OnMqttDataReceived;
            // MQTT 연결은 데이터 수신을 위해 미리 수행
            _ = _mqttService.ConnectAsync();

            _wsService1.OnImageReceived += HandleImage1;
            _wsService2.OnImageReceived += HandleImage2;

            TestCommand = new RelayCommand(async o =>
            {
                // 테스트용
                await _mqttService.ConnectAsync();
            });
        }

        private void HandleImage1(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => CameraImage1 = ByteToBitmapImage(data));
        }

        private void HandleImage2(byte[] data)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => CameraImage2 = ByteToBitmapImage(data));
        }

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

        private byte[]? ImageToByte(ImageSource? img)
        {
            if (img is WriteableBitmap wb)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(wb));
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            return null;
        }

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

        // [MQTT 수신] 서버가 보내준 판정 결과 처리
        private void OnMqttDataReceived(string jsonPayload)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 서버 규격에 맞춰 파싱
                    dynamic data = JsonConvert.DeserializeObject(jsonPayload);
                    if (data == null) return;

                    int pid = (data.pid != null) ? (int)data.pid : 0;
                    string resultStr = (string)data.result; // "OK", "NG"
                    bool isDefect = (resultStr == "NG" || resultStr == "DEFECTIVE");
                    string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    byte[]? img1Data = ImageToByte(CameraImage1);
                    byte[]? img2Data = ImageToByte(CameraImage2);

                    _dbManager.InsertMeasurement(pid, nowTime, isDefect, img1Data, img2Data);

                    CurrentComplete++;
                    if (isDefect) DefectCount++;
                    if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;

                    UpdateChartData();
                }
                catch { }
            });
        }

        // [수정] START: API 호출 방식
        public async void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                // 1. API 호출: /api/start
                bool success = await _apiService.StartSystemAsync("1"); // DeviceID=1 가정

                if (success)
                {
                    // 2. CCTV 켜기: /api/CCTV (action=1)
                    await _apiService.ControlCctvAsync("1");

                    // 3. 웹소켓 연결 (View 모드)
                    string fastApiIp = "192.168.0.7";
                    int fastApiPort = 8000;
                    string url1 = $"ws://{fastApiIp}:{fastApiPort}/api/view/1";
                    string url2 = $"ws://{fastApiIp}:{fastApiPort}/api/view/2";

                    await _wsService1.ConnectAsync(url1);
                    await _wsService2.ConnectAsync(url2);

                    _timer.Start();
                    Timer_Tick(null, EventArgs.Empty);
                }
                else
                {
                    System.Windows.MessageBox.Show("시스템 시작 실패 (서버 응답 없음)");
                }
            }
        }

        // [수정] RESTART: API 호출 방식
        public async void RestartSimulation()
        {
            bool success = await _apiService.RestartSystemAsync("1");

            if (success)
            {
                string fastApiIp = "192.168.0.7";
                int fastApiPort = 8000;
                await _wsService1.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/view/1");
                await _wsService2.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/view/2");

                CurrentComplete = 0;
                DefectCount = 0;
                DefectRate = 0;

                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                    Timer_Tick(null, EventArgs.Empty);
                }
            }
        }

        // [수정] STOP: API 호출 방식
        public async void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                await _apiService.StopSystemAsync("1");
                await _apiService.ControlCctvAsync("0"); // CCTV 끄기

                await _wsService1.DisconnectAsync();
                await _wsService2.DisconnectAsync();
                _timer.Stop();
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // 실시간 상태 갱신 (선택사항: /api/status 혹은 계산된 값 사용)
            var status = await _apiService.GetStatusAsync();
            if (status != null)
            {
                // 서버와 수량 동기화가 필요하다면 여기서 갱신
                // AllocationCount = status.AllocationCount;
            }
            UpdateChartData();
        }
    }
}