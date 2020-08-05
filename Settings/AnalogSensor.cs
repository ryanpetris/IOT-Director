using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class AnalogSensor : Sensor
    {
        public DigitalSensorClass Class { get; }
        
        public int OfflineMax { get; }
        public int OfflineMin { get; }
        public int OffMax { get; }
        public int OffMin { get; }
        public int OnMax { get; }
        public int OnMin { get; }
        
        public AnalogSensor(JsonElement element) : base(element)
        {
            Class = element.GetEnum<DigitalSensorClass>(nameof(Class));
            OfflineMax = element.GetInt(nameof(OfflineMax));
            OfflineMin = element.GetInt(nameof(OfflineMin));
            OffMax = element.GetInt(nameof(OffMax));
            OffMin = element.GetInt(nameof(OffMin));
            OnMax = element.GetInt(nameof(OnMax));
            OnMin = element.GetInt(nameof(OnMin));
        }
    }
}