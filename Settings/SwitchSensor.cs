using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class SwitchSensor : Sensor
    {
        public bool Invert { get; }
        
        public SwitchSensor(JsonElement element) : base(element)
        {
            Invert = element.GetBoolean(nameof(Invert));
        }
    }
}