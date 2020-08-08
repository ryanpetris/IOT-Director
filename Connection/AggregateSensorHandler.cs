using System.Collections.Generic;
using System.Linq;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.Connection
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
        
        public override void OnConnect(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            handler.OnConnect(sensor);
        }

        public override void OnLoop(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            handler.OnLoop(sensor);
        }

        public override void OnPublish(Sensor sensor)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            handler.OnPublish(sensor);
        }

        public override void OnSetState(Sensor sensor, object state)
        {
            if (!TryGetHandler(sensor, out var handler))
                return;
            
            handler.OnSetState(sensor, state);
        }

        private bool TryGetHandler(Sensor sensor, out SensorHandler sensorHandler)
        {
            sensorHandler = Handlers.FirstOrDefault(h => h.CanHandle(sensor));

            return sensorHandler != null;
        }
    }
}