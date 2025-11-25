using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        private DatabaseManager _dbManager = new DatabaseManager();

        // 1. 검색 날짜
        public string SearchDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

        // 2. 로그 데이터 리스트
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // [추가] 선택된 로그 항목 (LogView.xaml의 DataGrid와 바인딩됨)
        private LogEntry? _selectedLog;
        public LogEntry? SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        // [추가] 화면 오른쪽 그래프 바인딩용 속성 (LogView.xaml 오류 해결)
        // 화면이 깨지지 않도록 기본 경로(Path Data)를 넣어줍니다.
        public string GraphPathTop1 { get; } = "M 0,80 L 50,40 L 100,60 L 150,20 L 200,50 L 250,30 L 300,60";
        public string GraphPathTop2 { get; } = "M 0,50 L 50,60 L 100,30 L 150,50 L 200,20 L 250,40 L 300,20";
        public string GraphPathBottom { get; } = "M 0,40 L 50,50 L 100,20 L 150,60 L 200,30 L 250,50 L 300,40";

        // 3. 검색 버튼 명령
        public ICommand SearchCommand { get; }

        public LogViewModel()
        {
            LoadData(); // 시작할 때 데이터 불러오기

            SearchCommand = new RelayCommand(o => LoadData());
        }

        private void LoadData()
        {
            LogData.Clear();

            // DB 매니저를 통해 데이터 가져오기
            var logs = _dbManager.GetLogs();

            foreach (var log in logs)
            {
                LogData.Add(log);
            }
        }
    }
}