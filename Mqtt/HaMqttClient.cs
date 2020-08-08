using System;
using System.Linq;
using System.Text;
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
        
        public HaMqttClient(AppSettings settings)
        {
            Settings = settings;
            Client = Connect();
            
            InitMessages();
            InitPublish();
        }

        partial void InitMessages();
        partial void InitPublish();
        
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

        private async Task OnConnect(MqttClientConnectedEventArgs args)
        {
            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseDiscoveryTopic}/status").Build());
            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseControlTopic}/+/set").Build());
            
            await PublishConfiguration();
        }

        private async Task OnDisconnect(MqttClientDisconnectedEventArgs args)
        {
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