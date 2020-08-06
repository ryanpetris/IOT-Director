using System.Text.Json;
using IotDirector.Extensions;

namespace IotDirector.Settings
{
    public class SwitchSensor : Sensor
    {
        public bool Invert { get; }
        public bool DefaultState { get; }
        
        public SwitchSensor(JsonElement element) : base(element)
        {
            Invert = element.GetBoolean(nameof(Invert));
            DefaultState = element.GetBoolean(nameof(DefaultState));
        }
    }
}