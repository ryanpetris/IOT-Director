using System;
using System.Threading.Tasks;
using IotDirector.Commands;
using IotDirector.Connection;
using IotDirector.Mqtt;
using IotDirector.Settings;

namespace IotDirector.SensorHandlers
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

        public override async Task OnConnect(Sensor sensor)
        {
            await Task.Yield();
            
            if (!(sensor is SwitchSensor switchSensor))
                return;
            
            var state = switchSensor.DefaultState;

            if (switchSensor.Invert)
                state = !state;

            PinState.Set(switchSensor.Pin, state);
            await ArduinoProxy.SetPinMode(switchSensor.Pin, PinMode.Output, state);
            MqttClient.PublishSensorStatus(switchSensor, true);
            MqttClient.PublishSensorState(switchSensor, switchSensor.DefaultState);
        }

        public override async Task OnLoop(Sensor sensor)
        {
            await Task.Yield();
            
            if (!(sensor is SwitchSensor switchSensor))
                return;

            MqttClient.PublishSensorStatus(switchSensor, true);
        }

        public override async Task OnPublish(Sensor sensor)
        {
            await Task.Yield();
            
            if (!(sensor is SwitchSensor switchSensor))
                return;
            
            if (!PinState.Has(sensor.Pin))
                return;

            var switchState = PinState.GetBool(switchSensor.Pin);

            if (switchSensor.Invert)
                switchState = !switchState;
            
            MqttClient.PublishSensorStatus(switchSensor, true);
            MqttClient.PublishSensorState(switchSensor, switchState);
        }

        public override async Task OnSetState(Sensor sensor, object state)
        {
            await Task.Yield();
            
            if (!(sensor is SwitchSensor switchSensor))
                return;

            var apparentState = (bool) state;
            var actualState = apparentState;

            if (switchSensor.Invert)
                actualState = !actualState;
            
            if (!PinState.Set(switchSensor.Pin, actualState))
                return;
            
            await ArduinoProxy.DigitalWrite(switchSensor.Pin, actualState);
                        
            Console.WriteLine($"{switchSensor.Name} state changed to {(apparentState ? "on" : "off")} via MQTT.");
            
            MqttClient.PublishSensorStatus(switchSensor, true);
            MqttClient.PublishSensorState(switchSensor, apparentState);
        }
    }
}