using System;
using System.Threading.Tasks;

namespace IotDirector.Mqtt
{
    public interface IHaMqttConnection
    {
        public Guid Id { get; }
        public string DeviceId { get; }
        
        public Task SetSwitchState(string sensorId, bool state);
        public Task PublishPinStates();
    }
}