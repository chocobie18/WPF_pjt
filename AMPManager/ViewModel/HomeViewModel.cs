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
        private ApiService _apiService = new ApiService();
        private DatabaseManager _dbManager = new DatabaseManager();
        private MqttService _mqttService = new MqttService();

        // 웹소켓 서비스 (카메라 2대용)
        private WebSocketImageService _wsService1 = new WebSocketImageService();
        private WebSocketImageService _wsService2 = new WebSocketImageService();

        private bool _isCameraRunning = false;

        public PlotModel CombinedChartModel { get; private set; }

        private VideoCapture? _capture1;
        private VideoCapture? _capture2;
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

            // 로컬 카메라는 사용 안 함
            InitializeCamerasAsync();

            _mqttService.MessageReceived += OnMqttDataReceived;

            _wsService1.OnImageReceived += HandleImage1;
            _wsService2.OnImageReceived += HandleImage2;

            TestCommand = new RelayCommand(async o =>
            {
                await _mqttService.ConnectAsync();
                await _mqttService.SendTestSignal();
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

                    byte[]? img1Data = ImageToByte(CameraImage1);
                    byte[]? img2Data = ImageToByte(CameraImage2);

                    _dbManager.InsertMeasurement(pid, nowTime, isDefect, img1Data, img2Data);

                    CurrentComplete++;
                    if (isDefect) DefectCount++;
                    if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;
                }
                catch { }
            });
        }

        // [수정] FastAPI 서버 연결 로직 적용
        public async void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                // 1. MQTT 연결 (브로커 주소는 MqttService.cs 설정을 따름)
                await _mqttService.ConnectAsync();
                await _mqttService.SendCommandAsync("START");

                // 2. [수정] FastAPI 웹소켓 영상 연결
                string fastApiIp = "192.168.0.7";
                int fastApiPort = 8000;

                // FastAPI 경로에 맞춰서 연결
                await _wsService1.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/source/1");
                await _wsService2.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/source/2");

                // 3. 타이머 시작
                _timer.Start();

                // 즉시 갱신
                Timer_Tick(null, EventArgs.Empty);
            }
        }

        public async void RestartSimulation()
        {
            await _mqttService.ConnectAsync();
            await _mqttService.SendCommandAsync("RESET");

            // 재가동 시에도 FastAPI 연결 확인
            string fastApiIp = "192.168.0.7";
            int fastApiPort = 8000;
            await _wsService1.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/source/1");
            await _wsService2.ConnectAsync($"ws://{fastApiIp}:{fastApiPort}/api/source/2");

            CurrentComplete = 0;
            DefectCount = 0;
            DefectRate = 0;

            if (!_timer.IsEnabled)
            {
                _timer.Start();
                Timer_Tick(null, EventArgs.Empty);
            }
        }

        public async void StopSimulation()
        {
            if (_timer.IsEnabled)
            {
                await _mqttService.SendCommandAsync("STOP");
                await _wsService1.DisconnectAsync();
                await _wsService2.DisconnectAsync();
                _timer.Stop();
            }
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // [테스트용] 더미 데이터 (실제 장비 연결 시 삭제)
            if (true)
            {
                bool isBad = new Random().Next(0, 10) < 2;
                int randomPid = new Random().Next(1, 4);
                string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                byte[]? img1Data = ImageToByte(CameraImage1);
                byte[]? img2Data = ImageToByte(CameraImage2);

                _dbManager.InsertMeasurement(randomPid, nowTime, isBad, img1Data, img2Data);

                CurrentComplete++;
                if (isBad) DefectCount++;
                if (CurrentComplete > 0) DefectRate = (double)DefectCount / CurrentComplete * 100.0;
            }
            UpdateChartData();
        }

        private async void InitializeCamerasAsync() { await Task.Run(() => { try { _capture1 = new VideoCapture(0, VideoCaptureAPIs.DSHOW); _capture2 = new VideoCapture(1, VideoCaptureAPIs.DSHOW); } catch { } }); }

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