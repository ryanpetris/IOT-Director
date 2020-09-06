using System.Threading.Tasks;
using IotDirector.Connection;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.SensorHandlers
{
    public abstract class SensorHandler
    {
        protected ArduinoProxy ArduinoProxy { get; }
        protected HaMqttClient MqttClient { get; }
        protected PinState PinState { get; }

        protected SensorHandler(ArduinoProxy arduinoProxy, HaMqttClient mqttClient, PinState pinState)
        {
            ArduinoProxy = arduinoProxy;
            MqttClient = mqttClient;
            PinState = pinState;
        }
        
        public abstract bool CanHandle(Sensor sensor);
        public abstract Task OnConnect(Sensor sensor);
        public abstract Task OnLoop(Sensor sensor);
        public abstract Task OnPublish(Sensor sensor);
        public abstract Task OnSetState(Sensor sensor, object state);
    }
}