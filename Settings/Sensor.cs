using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class Sensor
    {
        public string Id { get; }
        public string DeviceId { get; }
        public string Name { get; }
        public int Pin { get; }
        public SensorType Type { get; }

        public Sensor(JsonElement element)
        {
            Id = element.GetString(nameof(Id));
            DeviceId = element.GetString(nameof(DeviceId));
            Name = element.GetString(nameof(Name));
            Pin = element.GetInt(nameof(Pin));
            Type = element.GetEnum<SensorType>(nameof(Type));
        }
    }
}