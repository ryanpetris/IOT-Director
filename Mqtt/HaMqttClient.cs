using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IotDirector.Extensions;
using IotDirector.Settings;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector.Mqtt
{
    public class HaMqttClient
    {
        private const int MessageBatchSize = 10;
        private static string OnState => "on";
        private static string OffState => "off";
        private static string OnlineStatus => "online";
        private static string OfflineStatus => "offline";
        
        private IMqttClient Client { get; }
        private AppSettings Settings { get; }
        
        private ConcurrentQueue<MqttApplicationMessage> Messages { get; }
        private ConcurrentQueue<MqttApplicationMessage> PendingMessages { get; }
        private Timer PublishTimer { get; }
        

        public EventHandler OnSendPinStateEvent;
        private Timer OnSendPinStateTimer { get; }

        public EventHandler<SwitchSetEventArgs> OnSwitchSetEvent;

        public HaMqttClient(AppSettings settings)
        {
            Settings = settings;
            Client = Connect();
            PublishTimer = new Timer(PublishMessages, null, 0, 50);
            Messages = new ConcurrentQueue<MqttApplicationMessage>();
            PendingMessages = new ConcurrentQueue<MqttApplicationMessage>();
            OnSendPinStateTimer = new Timer(OnSendPinState, null, 0, 5000);
        }

        public void PublishSensorState(Sensor sensor, bool state)
        {
            var message = new MqttApplicationMessage();

            message.Topic = GetControlStateTopicName(sensor);
            message.Retain = false;
            message.Payload = Encoding.UTF8.GetBytes(state ? OnState : OffState);

            Messages.Enqueue(message);
        }

        public void PublishSensorStatus(Sensor sensor, bool status)
        {
            var message = new MqttApplicationMessage();

            message.Topic = GetControlStatusTopicName(sensor);
            message.Retain = false;
            message.Payload = Encoding.UTF8.GetBytes(status ? OnlineStatus : OfflineStatus);

            Messages.Enqueue(message);
        }

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
        
        private string GetControlSetTopicName(Sensor sensor)
        {
            return $"{Settings.MqttBaseControlTopic}/{sensor.Id}/set";
        }
        
        private string GetControlStateTopicName(Sensor sensor)
        {
            return $"{Settings.MqttBaseControlTopic}/{sensor.Id}/state";
        }

        private string GetControlStatusTopicName(Sensor sensor)
        {
            return $"{Settings.MqttBaseControlTopic}/{sensor.Id}/status";
        }

        private string GetDiscoveryTopicName(Sensor sensor)
        {
            var component = MapSensorTypeToComponent(sensor.Type);

            return $"{Settings.MqttBaseDiscoveryTopic}/{component}/sensor-{sensor.Id}/config";
        }

        private string MapDigitalSensorClass(DigitalSensorClass sensorClass)
        {
            if (sensorClass == DigitalSensorClass.None)
                return sensorClass.ToString();
            
            var result = new StringBuilder();

            foreach (var character in sensorClass.ToString())
            {
                if (character.IsUpper())
                {
                    result.Append("_");
                    result.Append(character.ToLower());
                }
                else
                {
                    result.Append(character);
                }
            }

            return result.ToString().Trim('_');
        }

        private string MapSensorTypeToComponent(SensorType type)
        {
            switch (type)
            {
                case SensorType.Digital:
                case SensorType.Analog:
                    return "binary_sensor";
                
                case SensorType.Switch:
                    return "switch";
                
            }

            return null;
        }

        private async Task OnConnect(MqttClientConnectedEventArgs args)
        {
            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseDiscoveryTopic}/status").Build());
            await Client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic($"{Settings.MqttBaseControlTopic}/+/set").Build());
            
            await SendConfiguration();
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
                await SendConfiguration();
                return;
            }

            var topicParts = message.Topic.Split("/");

            if (topicParts.Length != 3 || topicParts[0] != Settings.MqttBaseControlTopic || topicParts[2] != "set")
                return;

            var sensorId = topicParts[1];
            var newState = Encoding.ASCII.GetString(message.Payload) == OnState;
            
            OnSwitchSetEvent?.Invoke(this, new SwitchSetEventArgs(sensorId, newState));
        }

        private void OnSendPinState(object state)
        {
            if (!Client.IsConnected)
                return;
            
            OnSendPinStateEvent?.Invoke(this, new EventArgs());
        }

        private void PublishMessages(object state)
        {
            while (!PendingMessages.IsEmpty || !Messages.IsEmpty)
            {
                PublishMessagesAsync().Wait();
            }
        }

        private async Task PublishMessagesAsync()
        {
            await Task.Yield();
            
            if (!Client.IsConnected)
                return;
            
            var messages = new List<MqttApplicationMessage>();

            if (!PendingMessages.IsEmpty)
            {
                while (!PendingMessages.IsEmpty)
                {
                    if (PendingMessages.TryDequeue(out var message))
                        messages.Add(message);
                }
            }
            else
            {
                for (var i = 0; i < MessageBatchSize; i++)
                {
                    if (!Messages.IsEmpty && Messages.TryDequeue(out var message))
                    {
                        messages.Add(message);
                    }
                }
            }

            if (messages.Count == 0)
                return;

            foreach (var message in messages)
            {
                PendingMessages.Enqueue(message);
            }

            await Client.PublishAsync(messages);
            
            PendingMessages.Clear();
        }

        private async Task SendConfiguration()
        {
            await Task.Yield();
            
            foreach (var sensor in Settings.Sensors)
            {
                switch (sensor.Type)
                {
                    case SensorType.Digital:
                    {
                        var digitalSensor = (DigitalSensor) sensor;
                        var message = new MqttApplicationMessage();

                        var payload = new
                        {
                            name = digitalSensor.Name,
                            device_class = MapDigitalSensorClass(digitalSensor.Class),
                            state_topic = GetControlStateTopicName(digitalSensor),
                            payload_on = OnState,
                            payload_off = OffState
                        };

                        message.Topic = GetDiscoveryTopicName(digitalSensor);
                        message.Retain = false;
                        message.Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

                        Messages.Enqueue(message);
                        
                        break;
                    }
                    
                    case SensorType.Analog:
                    {
                        var analogSensor = (AnalogSensor) sensor;
                        var message = new MqttApplicationMessage();

                        var payload = new
                        {
                            name = analogSensor.Name,
                            device_class = MapDigitalSensorClass(analogSensor.Class),
                            state_topic = GetControlStateTopicName(analogSensor),
                            availability_topic = GetControlStatusTopicName(analogSensor),
                            payload_on = OnState,
                            payload_off = OffState,
                            payload_available = OnlineStatus,
                            payload_not_available = OfflineStatus
                        };

                        message.Topic = GetDiscoveryTopicName(analogSensor);
                        message.Retain = false;
                        message.Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

                        Messages.Enqueue(message);
                        
                        break;
                    }

                    case SensorType.Switch:
                    {
                        var switchSensor = (SwitchSensor) sensor;
                        var message = new MqttApplicationMessage();
                        
                        var payload = new
                        {
                            name = switchSensor.Name,
                            state_topic = GetControlStateTopicName(switchSensor),
                            command_topic = GetControlSetTopicName(switchSensor),
                            payload_on = OnState,
                            payload_off = OffState
                        };

                        message.Topic = GetDiscoveryTopicName(switchSensor);
                        message.Retain = false;
                        message.Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

                        Messages.Enqueue(message);
                        
                        break;
                    }
                }
            }
        }
    }
}