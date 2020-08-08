using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private ConcurrentDictionary<Guid, IMqttConnection> Connections { get; } = new ConcurrentDictionary<Guid, IMqttConnection>();

        public void AddConnection(IMqttConnection connection)
        {
            if (!Connections.TryAdd(connection.Id, connection))
                throw new Exception($"Count not add connection {connection.Id} to list of connections.");
        }
        
        public void RemoveConnection(IMqttConnection connection)
        {
            Connections.TryRemove(connection.Id, out _);
        }

        private IImmutableList<string> GetConnectedDevices()
        {
            var connections = Connections.Values.ToImmutableList();

            return connections.Select(c => c.DeviceId).Distinct().ToImmutableList();
        }

        private IMqttConnection GetConnection(string deviceId)
        {
            return Connections.Values.FirstOrDefault(c => c.DeviceId == deviceId);
        }

        private void RunAllConnections(Action<IMqttConnection> action)
        {
            var connections = Connections.Values.ToImmutableList();

            foreach (var connection in connections)
            {
                try
                {
                    action.Invoke(connection);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error running command in connection {connection.Id}: {e}");
                }
            }
        }
    }
}