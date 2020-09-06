using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IotDirector.Connection;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.SensorHandlers
{
    public class AggregateSensorHandler : SensorHandler
    {
        private List<SensorHandler> Handlers { get; }

        public AggregateSensorHandler(ArduinoProxy arduinoProxy, HaMqttClient mqttClient) : base(arduinoProxy, mqttClient, new PinState())
        {
            Handlers = new List<SensorHandler>
            {
                new DigitalSensorHandler(ArduinoProxy, MqttClient, PinState),
                new AnalogSensorHandler(ArduinoProxy, MqttClient, PinState),
                new SwitchSensorHandler(ArduinoProxy, MqttClient, PinState)
            };
        }

        public override bool CanHandle(Sensor sensor)
        {
            return TryGetHandler(sensor, out _);
        }
        
        public override async Task OnConnect(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            await handler.OnConnect(sensor);
        }

        public override async Task OnLoop(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            await handler.OnLoop(sensor);
        }

        public override async Task OnPublish(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            await handler.OnPublish(sensor);
        }

        public override async Task OnSetState(Sensor sensor, object state)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            await handler.OnSetState(sensor, state);
        }

        private bool TryGetHandler(Sensor sensor, out SensorHandler sensorHandler)
        {
            sensorHandler = Handlers.FirstOrDefault(h => h.CanHandle(sensor));

            return sensorHandler != null;
        }
    }
}