using System;
using System.Threading.Tasks;
using IotDirector.Commands;
using IotDirector.Connection;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.SensorHandlers
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

        public override async Task OnConnect(Sensor sensor)
        {
            await Task.Yield();
            
            if (!(sensor is DigitalSensor digitalSensor))
                return;

            await ArduinoProxy.SetPinMode(digitalSensor.Pin, PinMode.InputPullup);
            MqttClient.PublishSensorStatus(digitalSensor, true);
        }

        public override async Task OnLoop(Sensor sensor)
        {
            await Task.Yield();
            
            if (!(sensor is DigitalSensor digitalSensor))
                return;

            var state = await ArduinoProxy.DigitalRead(digitalSensor.Pin);
            
            if (!PinState.Set(digitalSensor.Pin, state))
                return;
            
            if (digitalSensor.Invert)
                state = !state;
            
            Console.WriteLine($"{digitalSensor.Name} state changed to {(state ? "on" : "off")}.");
            
            MqttClient.PublishSensorStatus(digitalSensor, true);
            MqttClient.PublishSensorState(digitalSensor, state);
        }

        public override async Task OnPublish(Sensor sensor)
        {
            await Task.Yield();
            
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

        public override async Task OnSetState(Sensor sensor, object state)
        {
            await Task.Yield();
            
            // This function intentionally left empty.
        }
    }
}