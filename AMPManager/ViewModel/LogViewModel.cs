using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AMPManager.Core;   // RelayCommand, ObservableObject 사용을 위해 필요
using AMPManager.Model;  // LogEntry 모델 사용을 위해 필요

namespace AMPManager.ViewModel
{
    public class LogViewModel : BaseViewModel
    {
        // 1. 검색 날짜 (화면의 TextBox와 연결)
        public string SearchDate { get; set; } = DateTime.Now.ToString("yyyy.MM.dd");

        // 2. 로그 데이터 리스트 (화면의 DataGrid와 연결)
        // ObservableCollection은 리스트가 변하면 화면도 자동으로 업데이트해줍니다.
        public ObservableCollection<LogEntry> LogData { get; } = new ObservableCollection<LogEntry>();

        // 3. 검색 버튼 명령 (화면의 버튼과 연결)
        public ICommand SearchCommand { get; }

        // 생성자: 클래스가 만들어질 때 실행되는 부분
        public LogViewModel()
        {
            // 화면에 보여줄 가짜(더미) 데이터 20개 생성
            for (int i = 0; i < 20; i++)
            {
                LogData.Add(new LogEntry
                {
                    Timestamp = $"2025.01.01 10:{i:D2}",
                    PropertyName = $"데이터_속성_{i}",
                    Status = i % 5 == 0 ? "불량" : "정상"
                });
            }

            // 검색 버튼을 누르면 실행할 동작 연결
            SearchCommand = new RelayCommand(o =>
            {
                System.Diagnostics.Debug.WriteLine($"[검색 실행] 날짜: {SearchDate}");
            });
        }
    }
}