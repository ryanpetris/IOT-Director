using System;
using IotDirector.Commands;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.Connection
{
    public class SwitchSensorHandler : SensorHandler
    {
        public SwitchSensorHandler(ArduinoProxy arduinoProxy, HaMqttClient mqttClient, PinState pinState) : base(arduinoProxy, mqttClient, pinState)
        {
            
        }

        public override bool CanHandle(Sensor sensor)
        {
            return sensor.Type == SensorType.Switch && sensor is SwitchSensor;
        }

        public override void OnConnect(Sensor sensor)
        {
            if (!(sensor is SwitchSensor switchSensor))
                return;
            
            var state = switchSensor.DefaultState;

            if (switchSensor.Invert)
                state = !state;

            PinState.Set(switchSensor.Pin, state);
            ArduinoProxy.SetPinMode(switchSensor.Pin, PinMode.Output, state);
            MqttClient.PublishSensorState(switchSensor, switchSensor.DefaultState);
        }

        public override void OnLoop(Sensor sensor)
        {
            // This function intentionally left empty.
        }

        public override void OnPublish(Sensor sensor)
        {
            if (!(sensor is SwitchSensor switchSensor))
                return;
            
            if (!PinState.Has(sensor.Pin))
                return;

            var switchState = PinState.GetBool(switchSensor.Pin);

            if (switchSensor.Invert)
                switchState = !switchState;

            Console.WriteLine($"Send {switchSensor.Name} state as {(switchState ? "on" : "off")}.");
            MqttClient.PublishSensorState(switchSensor, switchState);
        }

        public override void OnSetState(Sensor sensor, object state)
        {
            if (!(sensor is SwitchSensor switchSensor))
                return;

            var apparentState = (bool) state;
            var actualState = apparentState;

            if (switchSensor.Invert)
                actualState = !actualState;
            
            if (!PinState.Set(switchSensor.Pin, actualState))
                return;
            
            ArduinoProxy.DigitalWrite(switchSensor.Pin, actualState);
                        
            Console.WriteLine($"{switchSensor.Name} state changed to {(apparentState ? "on" : "off")} via MQTT.");
            MqttClient.PublishSensorState(switchSensor, apparentState);
        }
    }
}