using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AMPManager.Core;
using AMPManager.Model;
using AMPManager.View;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        private ApiService _apiService = new ApiService();

        private List<LogEntry> _allLogs = new List<LogEntry>();
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 1. 검색 조건
        private string _searchDate = DateTime.Now.ToString("yyyy-MM-dd");
        public string SearchDate { get => _searchDate; set => SetProperty(ref _searchDate, value); }

        private bool _isCheckedNormal = true;
        public bool IsCheckedNormal { get => _isCheckedNormal; set { SetProperty(ref _isCheckedNormal, value); FilterLogs(); } }

        private bool _isCheckedDefect = true;
        public bool IsCheckedDefect { get => _isCheckedDefect; set { SetProperty(ref _isCheckedDefect, value); FilterLogs(); } }

        public ICommand SearchCommand { get; }
        public ICommand OpenDetailCommand { get; }

        public LogViewModel()
        {
            SearchCommand = new RelayCommand(o => LoadData());
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);

            // 초기 로드 (필요시 주석 해제)
            // LoadData(); 
        }

        private async void LoadData()
        {
            _allLogs.Clear();

            // ★★★ [날짜 포맷 변환] 점(.)을 하이픈(-)으로 변경하여 서버 전송 ★★★
            string formattedDate = SearchDate.Replace('.', '-');

            // 서버 API 호출
            var logs = await _apiService.GetLogsAsync(formattedDate);

            // 결과 처리
            if (logs == null)
            {
                System.Windows.MessageBox.Show("서버 연결 실패! (Python 서버가 켜져 있는지 확인하세요)", "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            if (logs.Count == 0)
            {
                System.Windows.MessageBox.Show($"'{formattedDate}' 날짜의 데이터가 없습니다.", "알림");
            }

            foreach (var log in logs)
            {
                // 불량 사유는 임시 데이터
                log.DefectReason = (log.Status == "불량") ? "치수 오차 초과" : "-";
                _allLogs.Add(log);
            }
            FilterLogs();
        }

        private void FilterLogs()
        {
            LogData.Clear();
            var filtered = _allLogs.Where(x =>
                (IsCheckedNormal && x.Status == "정상") ||
                (IsCheckedDefect && x.Status == "불량")
            );

            foreach (var item in filtered) LogData.Add(item);
        }

        // [상세 보기] 버튼 클릭 시 실행 -> 사진 가져오기
        private async void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // 서버에서 이미지 데이터 가져오기
                var (imgBytes1, imgBytes2) = await _apiService.GetLogImagesAsync(log.Id);

                // 이미지 변환
                log.Img1 = ByteToImage(imgBytes1);
                log.Img2 = ByteToImage(imgBytes2);

                // 상세 창 띄우기
                var window = new LogDetailWindow(log);
                window.Owner = System.Windows.Application.Current.MainWindow;
                window.ShowDialog();
            }
        }

        private ImageSource? ByteToImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            try
            {
                var image = new BitmapImage();
                using (var mem = new MemoryStream(bytes))
                {
                    mem.Position = 0;
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = mem;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch { return null; }
        }
    }
}