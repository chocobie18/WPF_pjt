namespace AMPManager.Model
{
    public class LogEntry
    {
        // 시간, 속성명, 상태를 저장하는 데이터 그릇입니다.
        public string Timestamp { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}