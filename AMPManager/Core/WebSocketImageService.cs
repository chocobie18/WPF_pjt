using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace AMPManager.Core
{
    public class WebSocketImageService
    {
        private ClientWebSocket _ws = new ClientWebSocket();

        // 이미지가 한 장 들어올 때마다 알림 (byte[] 데이터 전달)
        public event Action<byte[]> OnImageReceived;

        public async Task ConnectAsync(string url)
        {
            if (_ws.State == WebSocketState.Open) return;

            _ws = new ClientWebSocket(); // 재연결을 위해 새로 생성
            try
            {
                await _ws.ConnectAsync(new Uri(url), CancellationToken.None);
                System.Diagnostics.Debug.WriteLine("웹소켓 영상 서버 연결 성공!");

                // 백그라운드에서 수신 루프 시작
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"웹소켓 연결 실패: {ex.Message}");
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024 * 100]; // 100KB 버퍼 (프레임 하나 받을 정도)

            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    // 데이터 조각 모으기용 스트림
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            // 데이터 받기
                            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage); // 메시지 끝(이미지 한 장 완료)까지 계속 받기

                        // 다 받았으면 이벤트를 통해 ViewModel로 전송
                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            OnImageReceived?.Invoke(ms.ToArray());
                        }
                    }
                }
            }
            catch { }
        }

        public async Task DisconnectAsync()
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close", CancellationToken.None);
        }
    }
}