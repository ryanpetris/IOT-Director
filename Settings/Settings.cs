using System;
using System.Collections.Generic;
using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class Settings
    {
        public int ListenPort { get; }
        public string MqttHost { get; }
        public int MqttPort { get; }
        public string MqttClientId { get; }
        public string MqttBaseDiscoveryTopic { get; }
        public string MqttBaseControlTopic { get; }
        public Sensor[] Sensors { get; }

        public Settings(JsonDocument document)
        {
            var root = document.RootElement;

            ListenPort = root.GetInt(nameof(ListenPort));
            MqttHost = root.GetString(nameof(MqttHost));
            MqttPort = root.GetInt(nameof(MqttPort));
            MqttClientId = root.GetString(nameof(MqttClientId));
            MqttBaseDiscoveryTopic = root.GetString(nameof(MqttBaseDiscoveryTopic));
            MqttBaseControlTopic = root.GetString(nameof(MqttBaseControlTopic));

            var sensors = new List<Sensor>();

            foreach (var sensorElement in root.GetProperty("Sensors").EnumerateArray())
            {
                var type = sensorElement.GetProperty("Type").GetEnum<SensorType>();

                switch (type)
                {
                    case SensorType.Digital:
                        sensors.Add(new DigitalSensor(sensorElement));
                        break;
                    
                    case SensorType.Analog:
                        sensors.Add(new AnalogSensor(sensorElement));
                        break;
                    
                    case SensorType.Switch:
                        sensors.Add(new SwitchSensor(sensorElement));
                        break;
                    
                    default:
                        throw new Exception($"Invalid Sensor Type: {type}");
                }
            }

            Sensors = sensors.ToArray();
        }
    }
}