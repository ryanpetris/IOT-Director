using System;
using IotDirector.Commands;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.Connection
{
    public class DigitalSensorHandler : SensorHandler
    {
        public DigitalSensorHandler(ArduinoProxy arduinoProxy, HaMqttClient mqttClient, PinState pinState) : base(arduinoProxy, mqttClient, pinState)
        {
            
        }

        public override bool CanHandle(Sensor sensor)
        {
            return sensor.Type == SensorType.Digital && sensor is DigitalSensor;
        }

        public override void OnConnect(Sensor sensor)
        {
            if (!(sensor is DigitalSensor digitalSensor))
                return;

            ArduinoProxy.SetPinMode(digitalSensor.Pin, PinMode.InputPullup);
            MqttClient.PublishSensorStatus(digitalSensor, true);
        }

        public override void OnLoop(Sensor sensor)
        {
            if (!(sensor is DigitalSensor digitalSensor))
                return;

            var state = ArduinoProxy.DigitalRead(digitalSensor.Pin);
            
            if (!PinState.Set(digitalSensor.Pin, state))
                return;
            
            if (digitalSensor.Invert)
                state = !state;
            
            Console.WriteLine($"{digitalSensor.Name} state changed to {(state ? "on" : "off")}.");
            
            MqttClient.PublishSensorStatus(digitalSensor, true);
            MqttClient.PublishSensorState(digitalSensor, state);
        }

        public override void OnPublish(Sensor sensor)
        {
            if (!(sensor is DigitalSensor digitalSensor))
                return;

            if (!PinState.Has(sensor.Pin))
                return;

            var digitalState = PinState.GetBool(digitalSensor.Pin);

            if (digitalSensor.Invert)
                digitalState = !digitalState;
            
            MqttClient.PublishSensorStatus(digitalSensor, true);
            MqttClient.PublishSensorState(digitalSensor, digitalState);
        }

        public override void OnSetState(Sensor sensor, object state)
        {
            // This function intentionally left empty.
        }
    }
}