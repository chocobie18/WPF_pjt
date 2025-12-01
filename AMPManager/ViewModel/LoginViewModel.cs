using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

                // [수정] API를 통한 로그인
                bool isSuccess = await _apiService.LoginAsync(InputId, pw);

                if (isSuccess)
                {
                    // 로그인 성공 (권한 등은 서버 응답에 따라 처리하거나 임시 설정)
                    // 관리자는 id가 admin일 때 1, 아니면 2로 가정
                    int roleId = (InputId.ToLower() == "admin") ? 1 : 2;
                    LoggedInUser = new User("사용자", InputId, roleId);

                    CloseAction?.Invoke();
                }
                else
                {
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호가 틀렸습니다.", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}