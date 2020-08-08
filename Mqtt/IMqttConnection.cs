using System;

namespace IotDirector.Mqtt
{
    public interface IMqttConnection
    {
        public Guid Id { get; }
        public string DeviceId { get; }
        public void SetSwitchState(string sensorId, bool state);
        public void PublishPinStates();
    }
}