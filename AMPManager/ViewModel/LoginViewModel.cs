using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading.Tasks;
using AMPManager.Core;
using AMPManager.Model;

namespace AMPManager.ViewModel
{
    public class LoginViewModel : ObservableObject
    {
        private ApiService _apiService = new ApiService();

        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        public User? LoggedInUser { get; private set; }
        public ICommand LoginCommand { get; }
        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                bool isSuccess = false;

                // ★★★ [추가된 부분] 임시 로그인 정보 체크 ★★★
                if (InputId == "ID1234" && pw == "PW1234")
                {
                    isSuccess = true;
                }
                else
                {
                    // 1. 임시 계정이 아니면 서버 API를 통한 로그인 시도
                    isSuccess = await _apiService.LoginAsync(InputId, pw);
                }
                // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

                if (isSuccess)
                {
                    // 로그인 성공 시 처리
                    // (임시 계정은 admin 권한을 부여합니다.)
                    int roleId = (InputId.ToLower() == "admin" || InputId == "ID1234") ? 1 : 2;
                    LoggedInUser = new User("사용자", InputId, roleId);

                    CloseAction?.Invoke(); // 메인 화면으로 이동
                }
                else
                {
                    // 로그인 실패 시 메시지 출력
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호가 틀렸습니다.\n(서버 연결 상태를 확인하세요)",
                                    "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}