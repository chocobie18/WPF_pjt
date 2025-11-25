using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media; // ImageSource
using System.Windows.Media.Imaging; // BitmapImage
using System.IO; // MemoryStream
using AMPManager.Core;
using AMPManager.Model;
using AMPManager.View; // 상세창 띄우기 위해 필요

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        // 원본 전체 데이터 (필터링 전)
        private List<LogEntry> _allLogs = new List<LogEntry>();

        // 화면에 표시되는 데이터 (필터링 후)
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 1. 검색 조건
        private string _searchDate = System.DateTime.Now.ToString("yyyy-MM-dd");
        public string SearchDate { get => _searchDate; set => SetProperty(ref _searchDate, value); }

        private bool _isCheckedNormal = true;
        public bool IsCheckedNormal { get => _isCheckedNormal; set { SetProperty(ref _isCheckedNormal, value); FilterLogs(); } }

        private bool _isCheckedDefect = true;
        public bool IsCheckedDefect { get => _isCheckedDefect; set { SetProperty(ref _isCheckedDefect, value); FilterLogs(); } }

        // 2. 명령어
        public ICommand SearchCommand { get; }
        public ICommand OpenDetailCommand { get; } // 상세화면 버튼 명령

        public LogViewModel()
        {
            // 조회 버튼 클릭
            SearchCommand = new RelayCommand(o => LoadData());

            // 상세 버튼 클릭
            OpenDetailCommand = new RelayCommand(OpenDetailWindow);

            LoadData();
        }

        private void LoadData()
        {
            _allLogs.Clear();
            var logs = _dbManager.GetLogs(); // 전체 가져오기 (날짜 검색 로직 추가 가능)

            foreach (var log in logs)
            {
                // 불량 사유 가짜로 채우기 (나중엔 DB에서 가져와야 함)
                log.DefectReason = (log.Status == "불량") ? "치수 오차 초과 (Width < 5.0)" : "-";
                _allLogs.Add(log);
            }
            FilterLogs(); // 필터 적용해서 화면에 표시
        }

        // [필터링 로직] 체크박스 상태에 따라 리스트 걸러내기
        private void FilterLogs()
        {
            LogData.Clear();
            var filtered = _allLogs.Where(x =>
                (IsCheckedNormal && x.Status == "정상") ||
                (IsCheckedDefect && x.Status == "불량")
            );

            foreach (var item in filtered) LogData.Add(item);
        }

        // [상세창 열기]
        private void OpenDetailWindow(object? parameter)
        {
            if (parameter is LogEntry log)
            {
                // DB에서 해당 로그의 이미지를 가져옴
                var (b1, b2) = _dbManager.GetLogImages(log.Id);
                log.Img1 = ByteToImage(b1);
                log.Img2 = ByteToImage(b2);

                // 새 창 띄우기
                var window = new LogDetailWindow(log);
                window.Owner = System.Windows.Application.Current.MainWindow; // 부모 창 설정
                window.ShowDialog(); // 모달 창으로 열기 (뒤에 거 클릭 안됨)
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