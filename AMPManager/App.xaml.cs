using System.Windows;

namespace AMPManager
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    // [중요] 'System.Windows.Application' 이라고 풀네임 사용 (모호한 참조 에러 해결)
    public partial class App : System.Windows.Application
    {
        // [중요] 이곳은 비워두는 게 맞습니다.
        // InitializeComponent() 나 Main() 함수가 보이면 무조건 지우세요!
    }
}