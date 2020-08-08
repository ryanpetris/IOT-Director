using System.Text;
using IotDirector.Extensions;
using IotDirector.Settings;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private static string OnState => "on";
        private static string OffState => "off";
        private static string OnlineStatus => "online";
        private static string OfflineStatus => "offline";
        
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
    }
}