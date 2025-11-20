using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        public string SearchDate { get; set; } = DateTime.Now.ToString("yyyy.MM.dd");
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();
        public ICommand SearchCommand { get; }

        // [추가] 선택된 로그 항목
        private LogEntry? _selectedLog;
        public LogEntry? SelectedLog
        {
            get => _selectedLog;
            set
            {
                if (SetProperty(ref _selectedLog, value))
                {
                    UpdateGraphData(); // 선택 변경 시 그래프 갱신 함수 호출
                }
            }
        }

        // [추가] 동적 그래프 데이터 (상단 그래프 1, 2 / 하단 그래프 1)
        private string _graphPathTop1 = "M 0,80 L 50,40 L 100,60 L 150,20 L 200,50 L 250,30 L 300,60";
        public string GraphPathTop1 { get => _graphPathTop1; set => SetProperty(ref _graphPathTop1, value); }

        private string _graphPathTop2 = "M 0,50 L 50,60 L 100,30 L 150,50 L 200,20 L 250,40 L 300,20";
        public string GraphPathTop2 { get => _graphPathTop2; set => SetProperty(ref _graphPathTop2, value); }

        private string _graphPathBottom = "M 0,40 L 50,50 L 100,20 L 150,60 L 200,30 L 250,50 L 300,40";
        public string GraphPathBottom { get => _graphPathBottom; set => SetProperty(ref _graphPathBottom, value); }


        public LogViewModel()
        {
            for (int i = 0; i < 20; i++)
            {
                LogData.Add(new LogEntry
                {
                    Timestamp = $"2025.01.01 10:{i:D2}",
                    PropertyName = $"데이터_속성_{i}",
                    Status = i % 5 == 0 ? "불량" : "정상"
                });
            }

            SearchCommand = new RelayCommand(o =>
            {
                System.Diagnostics.Debug.WriteLine($"[검색 실행] 날짜: {SearchDate}");
            });
        }

        // [추가] 선택된 항목에 따라 랜덤하게 그래프 모양을 변경하는 함수
        private void UpdateGraphData()
        {
            if (SelectedLog == null) return;

            var rand = new Random();

            // 불량이면 좀 더 튀는 그래프, 정상이면 완만한 그래프를 생성하는 시늉
            int variance = SelectedLog.Status == "불량" ? 90 : 40;
            int baseLine = 50;

            GraphPathTop1 = GenerateRandomPath(rand, baseLine, variance);
            GraphPathTop2 = GenerateRandomPath(rand, baseLine - 10, variance);
            GraphPathBottom = GenerateRandomPath(rand, baseLine - 20, variance);
        }

        private string GenerateRandomPath(Random r, int baseY, int variance)
        {
            // 0부터 300까지 50단위로 점을 찍어 Path Data 문자열 생성
            string path = $"M 0,{Clamp(baseY + r.Next(-variance, variance))}";
            for (int x = 50; x <= 300; x += 50)
            {
                path += $" L {x},{Clamp(baseY + r.Next(-variance, variance))}";
            }
            return path;
        }

        private int Clamp(int value) => Math.Max(0, Math.Min(100, value)); // 0~100 사이로 제한
    }
}