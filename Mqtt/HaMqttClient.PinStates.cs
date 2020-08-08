using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IotDirector.Extensions;
using IotDirector.Settings;
using MQTTnet;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private static TimeSpan PublishPinStatesQuietPeriod { get; } = TimeSpan.FromSeconds(5);

        partial void InitPinStates()
        {
            ((Func<Task>)PublishPinStates).LoopUntilCancelled(CancellationToken, PublishPinStatesQuietPeriod);
        }
        
        private async Task PublishPinStates()
        {
            await Task.Yield();
            
            if (!Client.IsConnected)
                return;
            
            RunAllConnections(c => c.PublishPinStates());
        }
    }
}