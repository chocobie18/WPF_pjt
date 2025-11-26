using System.Windows.Media;

namespace AMPManager.Model
{
    public class LogEntry
    {
        // 시간, 속성명, 상태를 저장하는 데이터 그릇
        public int Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DefectReason { get; set; } = "-";

        // [추가] 상세화면으로 넘겨줄 이미지 데이터 (임시 저장용)
        public ImageSource? Img1 { get; set; }
        public ImageSource? Img2 { get; set; }
    }
}