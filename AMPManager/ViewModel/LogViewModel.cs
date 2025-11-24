using System.Collections.ObjectModel;
using System.Windows.Input;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        // [수정] DB 매니저 사용
        private DatabaseManager _dbManager = new DatabaseManager();

        public string SearchDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();
        public ICommand SearchCommand { get; }

        public LogViewModel()
        {
            LoadData(); // 시작할 때 불러오기

            SearchCommand = new RelayCommand(o => LoadData());
        }

        private void LoadData()
        {
            LogData.Clear();

            // [수정] 매니저를 통해 깔끔하게 데이터 가져오기
            var logs = _dbManager.GetLogs();

            foreach (var log in logs)
            {
                LogData.Add(log);
            }
        }
    }
}