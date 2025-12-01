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
        private DatabaseManager _dbManager = new DatabaseManager();

        // 입력된 아이디
        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        // 로그인 결과 (성공한 유저 정보)
        public User? LoggedInUser { get; private set; }

        // 로그인 버튼 명령
        public ICommand LoginCommand { get; }

        // 로그인 성공 시 창을 닫기 위한 Action
        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            // PasswordBox는 보안상 바인딩이 안 되므로, 파라미터로 직접 받습니다.
            LoginCommand = new RelayCommand(o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";

                // 1. 로그인 시도
                var user = _dbManager.Login(InputId, pw);

                if (user != null)
                {
                    // 성공
                    LoggedInUser = user;
                    CloseAction?.Invoke(); // 창 닫기 (-> 메인화면으로 이동)
                }
                else
                {
                    // 실패
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호가 틀렸습니다.", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}