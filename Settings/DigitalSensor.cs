using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class DigitalSensor : Sensor
    {
        public DigitalSensorClass Class { get; }
        public bool Invert { get; }

        public DigitalSensor(JsonElement element) : base(element)
        {
            Class = element.GetEnum<DigitalSensorClass>(nameof(Class));
            Invert = element.GetBoolean(nameof(Invert));
        }
    }
}