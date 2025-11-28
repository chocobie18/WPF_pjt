using System;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace AMPManager.Core
{
    public class MqttService
    {
        private IMqttClient _mqttClient;
        private MqttFactory _factory;

        // [에러 해결 1] 메시지가 들어오면 ViewModel에게 알려줄 이벤트
        public event Action<string> MessageReceived;

        // ★ 브로커 주소 (라즈베리파이 IP나 localhost)
        private const string BrokerIp = "192.168.0.31";
        private const int BrokerPort = 1883;

        // ★ 토픽 정의
        private const string TopicControl = "factory/control"; // 시작/정지 명령용
        private const string TopicData = "factory/data";       // 계측 데이터 수신용

        public MqttService()
        {
            _factory = new MqttFactory();
            _mqttClient = _factory.CreateMqttClient();

            // 메시지 수신 핸들러 연결
            _mqttClient.ApplicationMessageReceivedAsync += HandleMessageAsync;
        }

        // [에러 해결 2] 연결하기 함수
        public async Task ConnectAsync()
        {
            if (_mqttClient.IsConnected) return;

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(BrokerIp, BrokerPort)
                .WithClientId("WPF_Monitor_App")
                .Build();

            try
            {
                await _mqttClient.ConnectAsync(options);

                // 연결되면 바로 데이터 토픽 구독(Listen) 시작
                await _mqttClient.SubscribeAsync(TopicData);
                System.Diagnostics.Debug.WriteLine("MQTT 연결 및 구독 성공!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT 연결 실패: {ex.Message}");
            }
        }

        // [에러 해결 3] 명령 보내기 함수 (START / STOP)
        public async Task SendCommandAsync(string command)
        {
            if (!_mqttClient.IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(TopicControl)
                .WithPayload(command)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);
            System.Diagnostics.Debug.WriteLine($"명령 전송: {command}");
        }

        public async Task SendTestSignal()
        {
            if (!_mqttClient.IsConnected) return;

            // 기존 SendCommandAsync를 재활용해서 "1"을 보냅니다.
            await SendCommandAsync("1");
            System.Diagnostics.Debug.WriteLine(">>> [테스트] 신호 '1' 전송함");
        }

        // 4. 메시지 받았을 때 처리 (내부용)
        private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            string topic = e.ApplicationMessage.Topic;

            // 데이터 토픽에서 온 메시지만 처리
            if (topic == TopicData)
            {
                // UI 스레드로 이벤트 전달
                MessageReceived?.Invoke(payload);
            }
            return Task.CompletedTask;
        }

        // 5. 연결 끊기
        public async Task DisconnectAsync()
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
        }
    }

}