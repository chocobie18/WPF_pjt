using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging; // [필수] WPF 이미지 처리용
using System.Windows.Threading;
using AMPManager.Core;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions; // [필수] WriteableBitmapConverter 사용
using System.Linq;

namespace AMPManager.ViewModel
{
    public class HomeViewModel : BaseViewModel
    {
        private DispatcherTimer _timer;
        private ApiService _apiService = new ApiService();
        private bool _isCameraRunning = false;

        // --- 카메라 객체 ---
        private VideoCapture? _capture1;
        private VideoCapture? _capture2;

        // UI 바인딩용 속성 (내부적으로는 WriteableBitmap으로 관리되지만 외부엔 ImageSource로 노출)
        private ImageSource? _cameraImage1;
        public ImageSource? CameraImage1 { get => _cameraImage1; set => SetProperty(ref _cameraImage1, value); }

        private ImageSource? _cameraImage2;
        public ImageSource? CameraImage2 { get => _cameraImage2; set => SetProperty(ref _cameraImage2, value); }

        // --- 데이터 ---
        private int _allocationCount = 1000;
        private int _currentComplete = 0;
        private double _defectRate = 0;

        public int AllocationPercent => _allocationCount == 0 ? 0 : (int)((double)_currentComplete / _allocationCount * 100);
        public int AllocationCount { get => _allocationCount; set => SetProperty(ref _allocationCount, value); }
        public int CurrentComplete { get => _currentComplete; set { if (SetProperty(ref _currentComplete, value)) OnPropertyChanged(nameof(AllocationPercent)); } }
        public double DefectRate { get => _defectRate; set => SetProperty(ref _defectRate, value); }

        public ObservableCollection<string> TimestampList { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DefectMessageList { get; } = new ObservableCollection<string>();

        public HomeViewModel()
        {
            // 전송 속도 최적화 (0.5초)
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            InitializeCamerasAsync();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // 1. 서버 데이터 가져오기
            var data = await _apiService.GetStatusAsync();
            if (data != null)
            {
                AllocationCount = data.AllocationCount;
                CurrentComplete = data.CurrentComplete;
                DefectRate = data.DefectRate;
                TimestampList.Clear();
                foreach (var log in data.Logs) TimestampList.Add(log);
            }

            // 2. WPF 화면 캡처 후 전송
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
                        byte[] imageBytes = ms.ToArray();
                        await _apiService.SendImageAsync(imageBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"캡처 오류: {ex.Message}");
            }
        }

        public void StartSimulation()
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
                _isCameraRunning = true;

                // [수정] 현재 이미지를 가져오는 함수(Getter)와 설정하는 함수(Setter)를 모두 전달
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
                    // DSHOW가 호환성이 가장 좋음
                    _capture1 = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                    _capture2 = new VideoCapture(1, VideoCaptureAPIs.DSHOW);
                }
                catch { }
            });
        }

        // [핵심 수정] WriteableBitmap을 사용하여 메모리 재사용 및 성능 최적화
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
                            // UI 스레드에서 이미지 갱신 (WriteableBitmap 사용)
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                // 현재 뷰모델에 있는 이미지를 가져와서 WriteableBitmap인지 확인
                                var writeableBitmap = getImage() as WriteableBitmap;

                                // 이미지가 없거나 해상도가 변경되었으면 새로 생성 (초기 1회 실행됨)
                                if (writeableBitmap == null ||
                                    writeableBitmap.PixelWidth != frame.Width ||
                                    writeableBitmap.PixelHeight != frame.Height)
                                {
                                    // 확장 메서드 사용: Mat -> WriteableBitmap (새로 생성)
                                    writeableBitmap = frame.ToWriteableBitmap();
                                    updateImage(writeableBitmap); // 새 비트맵을 뷰모델 프로퍼티에 할당
                                }
                                else
                                {
                                    // [오류 수정] WriteTo 대신 ToWriteableBitmap 오버로드 사용
                                    // 기존 비트맵 버퍼에 픽셀 데이터만 덮어쓰기 (메모리 할당 없음, 매우 빠름)
                                    WriteableBitmapConverter.ToWriteableBitmap(frame, writeableBitmap);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Camera Error: {ex.Message}");
                    }

                    // CPU 점유율 조절 (30FPS 근처)
                    System.Threading.Thread.Sleep(33);
                }
            });

            // 루프 종료 시 이미지 초기화
            System.Windows.Application.Current.Dispatcher.Invoke(() => updateImage(null));
        }
    }
}