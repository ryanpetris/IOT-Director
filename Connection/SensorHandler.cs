using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.Connection
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
        public abstract void OnConnect(Sensor sensor);
        public abstract void OnLoop(Sensor sensor);
        public abstract void OnPublish(Sensor sensor);
        public abstract void OnSetState(Sensor sensor, object state);
    }
}