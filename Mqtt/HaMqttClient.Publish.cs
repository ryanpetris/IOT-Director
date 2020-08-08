using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IotDirector.Settings;
using MQTTnet;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private static TimeSpan PublishPinStatesQuietPeriod { get; }= TimeSpan.FromSeconds(5);

        partial void InitPublish()
        {
            PublishPinStatesLoop();
        }
        
        private async Task PublishConfiguration()
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
        
        private void PublishPinStates()
        {
            if (!Client.IsConnected)
                return;
            
            RunAllConnections(c => c.PublishPinStates());
        }

        private async void PublishPinStatesLoop()
        {
            await Task.Yield();
            
            try
            {
                while (true)
                {
                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();

                    PublishPinStates();

                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await Task.Delay(MessageQuietPeriod, CancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // This block intentionally left empty.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Publish Pin States thread shutdown.");
            }
        }
    }
}