using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IotDirector.Extensions;
using IotDirector.Settings;
using MQTTnet;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private static TimeSpan PublishUnavailableSensorsQuietPeriod { get; } = TimeSpan.FromSeconds(1);
        
        partial void InitConfiguration()
        {
            ((Func<Task>)PublishUnavailableSensors).LoopUntilCancelled(CancellationToken, PublishUnavailableSensorsQuietPeriod);
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
                            availability_topic = GetControlStatusTopicName(digitalSensor),
                            payload_on = OnState,
                            payload_off = OffState,
                            payload_available = OnlineStatus,
                            payload_not_available = OfflineStatus
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
                            availability_topic = GetControlStatusTopicName(switchSensor),
                            payload_on = OnState,
                            payload_off = OffState,
                            payload_available = OnlineStatus,
                            payload_not_available = OfflineStatus
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

        private async Task PublishUnavailableSensors()
        {
            await Task.Yield();
            
            var devices = GetConnectedDevices();
            var sensors = Settings.Sensors.Where(s => !devices.Contains(s.DeviceId));

            foreach (var sensor in sensors)
                PublishSensorStatus(sensor, false);
        }
    }
}