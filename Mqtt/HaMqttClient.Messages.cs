using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using IotDirector.Settings;
using MQTTnet;
using MQTTnet.Client;

namespace IotDirector.Mqtt
{
    public partial class HaMqttClient
    {
        private const int MessageBatchSize = 10;
        private static TimeSpan MessageQuietPeriod { get; } = TimeSpan.FromMilliseconds(50);
        
        private ConcurrentQueue<MqttApplicationMessage> Messages { get; } = new ConcurrentQueue<MqttApplicationMessage>();
        private ConcurrentQueue<MqttApplicationMessage> PendingMessages { get; } = new ConcurrentQueue<MqttApplicationMessage>();

        partial void InitMessages()
        {
            PublishMessagesLoop();
        }

        private async Task PublishMessages()
        {
            await Task.Yield();
            
            while (!PendingMessages.IsEmpty || !Messages.IsEmpty)
            {
                if (!Client.IsConnected)
                    return;
                
                var messages = new List<MqttApplicationMessage>();

                if (!PendingMessages.IsEmpty)
                {
                    while (!PendingMessages.IsEmpty)
                    {
                        if (PendingMessages.TryDequeue(out var message))
                            messages.Add(message);
                    }
                }
                else
                {
                    for (var i = 0; i < MessageBatchSize; i++)
                    {
                        if (!Messages.IsEmpty && Messages.TryDequeue(out var message))
                            messages.Add(message);
                    }
                }

                if (messages.Count == 0)
                    return;

                foreach (var message in messages)
                {
                    PendingMessages.Enqueue(message);
                }

                await Client.PublishAsync(messages);
                
                PendingMessages.Clear();
            }
        }

        private async void PublishMessagesLoop()
        {
            await Task.Yield();
            
            try {
                while (true)
                {
                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();

                    await PublishMessages();
                    
                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await Task.Delay(MessageQuietPeriod, CancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Publish Messages thread shutdown.");
            }
        }
        
        public void PublishSensorState(Sensor sensor, bool state)
        {
            var message = new MqttApplicationMessage();

            message.Topic = GetControlStateTopicName(sensor);
            message.Retain = false;
            message.Payload = Encoding.UTF8.GetBytes(state ? OnState : OffState);

            Messages.Enqueue(message);
        }

        public void PublishSensorStatus(Sensor sensor, bool status)
        {
            var message = new MqttApplicationMessage();

            message.Topic = GetControlStatusTopicName(sensor);
            message.Retain = false;
            message.Payload = Encoding.UTF8.GetBytes(status ? OnlineStatus : OfflineStatus);

            Messages.Enqueue(message);
        }
    }
}