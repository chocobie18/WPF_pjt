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
        // =========================================================
        // [변수 선언부]
        // =========================================================

        // [STEP 1: 나중에 서버 연결 시] 아래 줄의 주석(//)을 해제하세요.
        // private ApiService _apiService = new ApiService();

        // [STEP 1: 나중에 서버 연결 시] 아래 줄을 주석(//) 처리하세요. (로컬 테스트용)
        private DatabaseManager _dbManager = new DatabaseManager();


        private string _inputId = "";
        public string InputId { get => _inputId; set => SetProperty(ref _inputId, value); }

        public User? LoggedInUser { get; private set; }
        public ICommand LoginCommand { get; }
        public Action? CloseAction { get; set; }

        public LoginViewModel()
        {
            // [STEP 2: 나중에 서버 연결 시] 'async' 키워드를 추가해야 합니다.
            // 변경 전: LoginCommand = new RelayCommand(o =>
            // 변경 후: LoginCommand = new RelayCommand(async o =>
            LoginCommand = new RelayCommand(o =>
            {
                var passwordBox = o as PasswordBox;
                string pw = passwordBox != null ? passwordBox.Password : "";


                // =========================================================
                // [STEP 3: 실제 서버 연동 코드 (나중에 주석 해제)]
                // =========================================================
                /*
                // (1) 서버에 로그인 요청
                bool isSuccess = await _apiService.LoginAsync(InputId, pw);

                if (isSuccess)
                {
                    // (2) 로그인 성공 처리
                    // 관리자(admin)인지 일반 유저인지 구분 (서버 응답에 따라 수정 가능)
                    int roleId = (InputId.ToLower() == "admin") ? 1 : 2;
                    
                    LoggedInUser = new User("사용자", InputId, roleId);
                    CloseAction?.Invoke();
                }
                else
                {
                    // (3) 실패 처리
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호가 일치하지 않습니다.", "로그인 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                */


                // =========================================================
                // [STEP 3: 로컬 테스트용 코드 (나중에 주석 처리)]
                // =========================================================
                User? user = _dbManager.Login(InputId, pw);

                if (user != null)
                {
                    LoggedInUser = user;
                    CloseAction?.Invoke();
                }
                else
                {
                    System.Windows.MessageBox.Show("아이디 또는 비밀번호를 확인해주세요.\n(admin/1234 또는 worker/1234)",
                                    "로그인 실패(로컬)", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
    }
}