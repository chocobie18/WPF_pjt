using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AMPManager.Model;
using System.Diagnostics;

namespace AMPManager.Core
{
    public class ApiService
    {
        private readonly HttpClient _client;

        // ★ 서버 IP 주소가 맞는지 다시 확인해주세요! (http:// 포함)
        private const string BaseUrl = "http://192.168.0.7:8000";

        public ApiService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(3);
        }

        // [기능 1] 서버 데이터 가져오기
        public async Task<ServerData?> GetStatusAsync()
        {
            try
            {
                var response = await _client.GetStringAsync($"{BaseUrl}/api/status");
                return JsonConvert.DeserializeObject<ServerData>(response);
            }
            catch
            {
                return null;
            }
        }

        // [기능 2] 이미지 전송하기 (이제 캡처는 ViewModel에서 해서 넘겨줍니다)
        public async Task SendImageAsync(byte[] imageBytes)
        {
            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                    content.Add(imageContent, "image", "screen.jpg");

                    await _client.PostAsync($"{BaseUrl}/api/upload_frame", content);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"전송 실패: {ex.Message}");
            }
        }
    }
}   