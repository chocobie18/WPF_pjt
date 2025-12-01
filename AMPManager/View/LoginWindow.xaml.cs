using System.Windows;
using AMPManager.ViewModel;

namespace AMPManager.View
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            var vm = new LoginViewModel();

            // 뷰모델의 "닫기" 신호가 오면 실제로 창을 닫고 DialogResult를 true로 설정
            vm.CloseAction = () => { this.DialogResult = true; this.Close(); };

            this.DataContext = vm;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}