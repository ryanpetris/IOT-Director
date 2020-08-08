using System;
using IotDirector.Commands;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.Connection
{
    public class AnalogSensorHandler : SensorHandler
    {
        public AnalogSensorHandler(ArduinoProxy arduinoProxy, HaMqttClient mqttClient, PinState pinState) : base(arduinoProxy, mqttClient, pinState)
        {
            
        }

        public override bool CanHandle(Sensor sensor)
        {
            return sensor.Type == SensorType.Analog && sensor is AnalogSensor;
        }

        public override void OnConnect(Sensor sensor)
        {
            if (!(sensor is AnalogSensor analogSensor))
                return;

            ArduinoProxy.SetPinMode(analogSensor.Pin, PinMode.Input);
            MqttClient.PublishSensorStatus(analogSensor, true);
        }

        public override void OnLoop(Sensor sensor)
        {
            if (!(sensor is AnalogSensor analogSensor))
                return;

            var value = ArduinoProxy.AnalogRead(analogSensor.Pin);
            var status = false;
            var state = false;

            if (value >= analogSensor.OfflineMin && value <= analogSensor.OfflineMax)
            {
                status = false;
                state = false;
            }
            else if (value >= analogSensor.OffMin && value <= analogSensor.OffMax)
            {
                status = true;
                state = false;
            }
            else if (value >= analogSensor.OnMin && value <= analogSensor.OnMax)
            {
                status = true;
                state = true;
            }
            else
            {
                status = false;
                state = false;
            }

            var pinState = (state ? 1 : 0) + (status ? 0 : -1);
                        
            if (!PinState.Set(analogSensor.Pin, pinState))
                return;

            if (!status)
            {
                Console.WriteLine($"{analogSensor.Name} state changed to offline.");
            }
            else
            {
                Console.WriteLine($"{analogSensor.Name} state changed to {(state ? "on" : "off")}.");
            }

            MqttClient.PublishSensorStatus(analogSensor, status);
            MqttClient.PublishSensorState(analogSensor, state);
        }

        public override void OnPublish(Sensor sensor)
        {
            if (!(sensor is AnalogSensor analogSensor))
                return;
            
            if (!PinState.Has(sensor.Pin))
                return;

            var state = PinState.Get(analogSensor.Pin);
            var analogStatus = state >= 0;
            var analogState = state == 1;

            if (!analogStatus)
            {
                Console.WriteLine($"Send {analogSensor.Name} state as offline.");
            }
            else
            {
                Console.WriteLine($"Send {analogSensor.Name} state as {(analogState ? "on" : "off")}.");
            }

            MqttClient.PublishSensorStatus(analogSensor, analogStatus);
            MqttClient.PublishSensorState(analogSensor, analogState);
        }

        public override void OnSetState(Sensor sensor, object state)
        {
            // This function intentionally left empty.
        }
    }
}