using Newtonsoft.Json;

namespace AMPManager.Model
{
    // 1. 로그인 요청 (보낼 데이터)
    public class LoginRequest
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("pw")]
        public string Pw { get; set; } = string.Empty;
    }

    // 2. 로그인 응답 (받을 데이터)
    public class LoginResponse
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        // 필요하다면 토큰이나 유저 정보 추가
    }
}