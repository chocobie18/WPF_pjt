using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AMPManager.Model;
using System.Diagnostics;

namespace AMPManager.Core
{
    public class ApiService
    {
        private readonly HttpClient _client;

        // ★ 서버 IP와 포트를 환경에 맞게 설정하세요 (http:// 필수)
        private const string BaseUrl = "http://192.168.0.7:8000";

        public ApiService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(5); // 5초 타임아웃
        }

        // [페이지 2] 로그인
        // URL: /api/login
        // 요청: { "ID": "...", "Password": "..." }
        public async Task<bool> LoginAsync(string id, string pw)
        {
            try
            {
                var payload = new { ID = id, Password = pw };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/login", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Login Error] {ex.Message}");
                return false;
            }
        }

        // [페이지 3] 시작
        // URL: /api/start
        // 요청: { "deviceId": "..." }
        public async Task<bool> StartSystemAsync(string deviceId = "1")
        {
            return await SendCommandAsync("/api/start", deviceId);
        }

        // [페이지 4] 재가동
        // URL: /api/restart
        public async Task<bool> RestartSystemAsync(string deviceId = "1")
        {
            return await SendCommandAsync("/api/restart", deviceId);
        }

        // [페이지 5] 정지
        // URL: /api/stop
        public async Task<bool> StopSystemAsync(string deviceId = "1")
        {
            return await SendCommandAsync("/api/stop", deviceId);
        }

        // 공통 명령 전송 헬퍼
        private async Task<bool> SendCommandAsync(string endpoint, string deviceId)
        {
            try
            {
                var payload = new { deviceId = deviceId };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(BaseUrl + endpoint, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Command Error] {endpoint}: {ex.Message}");
                return false;
            }
        }

        // [페이지 6] CCTV 제어
        // URL: /api/CCTV
        // 요청: { "action": "1" } (1: 전송요청, 0: 전송중지)
        public async Task<bool> ControlCctvAsync(string action)
        {
            try
            {
                var payload = new { action = action };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/CCTV", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CCTV Error] {ex.Message}");
                return false;
            }
        }

        // [페이지 7] 로그 조회
        // URL: /api/logs
        // 요청: { "startDate": "YYYYMMDD" }
        public async Task<List<LogEntry>> GetLogsAsync(string startDate)
        {
            try
            {
                var payload = new { startDate = startDate };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/logs", content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    // 서버 응답 JSON을 LogEntry 리스트로 변환
                    return JsonConvert.DeserializeObject<List<LogEntry>>(json) ?? new List<LogEntry>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logs Error] {ex.Message}");
            }
            return new List<LogEntry>();
        }

        // [페이지 9] 통계 조회
        // URL: /api/Statistics
        // 요청: { "startDate": "...", "endDate": "..." }
        public async Task<ServerData?> GetStatisticsAsync(string start, string end)
        {
            try
            {
                var payload = new { startDate = start, endDate = end };
                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{BaseUrl}/api/Statistics", content);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<ServerData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Stats Error] {ex.Message}");
            }
            return null;
        }

        // [추가] 실시간 상태 조회 (기존 HomeViewModel용)
        // 프로토콜엔 없지만 메인화면 갱신을 위해 유지하거나 /api/Statistics로 대체 가능
        public async Task<ServerData?> GetStatusAsync()
        {
            try
            {
                // 편의상 GET으로 유지하거나 프로토콜에 맞춰 통계 API 사용
                var response = await _client.GetStringAsync($"{BaseUrl}/api/status");
                return JsonConvert.DeserializeObject<ServerData>(response);
            }
            catch { return null; }
        }
    }
}