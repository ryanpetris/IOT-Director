using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private IMqttClient Client { get; }
        private AppSettings Settings { get; }
        private CancellationToken CancellationToken { get; }
        
        public HaMqttClient(AppSettings settings, CancellationToken cancellationToken)
        {
            Settings = settings;
            CancellationToken = cancellationToken;
            Client = Connect();

            InitConfiguration();
            InitMessages();
            InitPinStates();
            
            MonitorCancellation();
        }

        partial void InitConfiguration();
        partial void InitMessages();
        partial void InitPinStates();
        
        private IMqttClient Connect()
        {
            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            client.UseConnectedHandler(OnConnect);
            client.UseDisconnectedHandler(OnDisconnect);
            client.UseApplicationMessageReceivedHandler(OnMessage);

            #pragma warning disable 4014
            OnDisconnect(null);
            #pragma warning restore 4014
            
            return client;
        }

        private async void MonitorCancellation()
        {
            await Task.Yield();

            try
            {
                while (true)
                {
                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        await Task.Delay(-1, CancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await Client.DisconnectAsync();
                
                Console.WriteLine("MQTT Client shutdown.");
            }
        }

        private async Task OnConnect(MqttClientConnectedEventArgs args)
        {
            await Task.Yield();

            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseDiscoveryTopic}/status").Build());
            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseControlTopic}/+/set").Build());
            
            await PublishConfiguration();
        }

        private async Task OnDisconnect(MqttClientDisconnectedEventArgs args)
        {
            await Task.Yield();
            
            if (CancellationToken.IsCancellationRequested)
                return;
            
            Console.WriteLine("Connecting to MQTT host...");
                
            await Task.Delay(TimeSpan.FromSeconds(1));
                
            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(Settings.MqttHost, Settings.MqttPort)
                    .WithClientId(Settings.MqttClientId)
                    .WithCleanSession()
                    .Build();
            
                await Client.ConnectAsync(options);
                
                Console.WriteLine("Connected to MQTT host.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Encountered error connecting to MQTT host: {e.Message}");
            }
        }

        private async Task OnMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            await Task.Yield();

            var message = args.ApplicationMessage;

            if (message.Topic == $"{Settings.MqttBaseDiscoveryTopic}/status")
            {
                await PublishConfiguration();
                return;
            }

            var topicParts = message.Topic.Split("/");

            if (topicParts.Length != 3 || topicParts[0] != Settings.MqttBaseControlTopic || topicParts[2] != "set")
                return;

            var sensor = Settings.Sensors.FirstOrDefault(s => s.Id == topicParts[1]);

            if (sensor == null)
                return;

            var connection = GetConnection(sensor.DeviceId);

            if (connection == null)
                return;
            
            var newState = Encoding.ASCII.GetString(message.Payload) == OnState;
            
            connection.SetSwitchState(sensor.Id, newState);
        }
    }
}