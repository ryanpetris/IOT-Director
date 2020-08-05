using System;

namespace IotDirector.Mqtt
{
    public class SwitchSetEventArgs : EventArgs
    {
        public string SensorId { get; }
        public bool NewState { get; }

        public SwitchSetEventArgs(string sensorId, bool newState)
        {
            SensorId = sensorId;
            NewState = newState;
        }
    }
}