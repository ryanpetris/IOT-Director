using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IotDirector.Mqtt;
using IotDirector.SensorHandlers;
using IotDirector.Settings;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector.Connection
{
    public class Connection : IDisposable, IHaMqttConnection
    {
        Guid IHaMqttConnection.Id { get; } = Guid.NewGuid();
        public string DeviceId { get; private set; }
        
        private TcpClient Client { get; }
        private ArduinoCommandHandler ArduinoCommandHandler { get; }
        private ArduinoProxy Arduino { get; }
        
        private HaMqttClient MqttClient { get; }
        
        private CancellationToken CancellationToken { get; }
        private AppSettings Settings { get; }
        private Sensor[] Sensors { get; set; }
        private SensorHandler SensorHandler { get; set; }
        
        private TaskStatus Status { get; set; }
        private Task RunTask { get; set; }
        
        public Connection(AppSettings settings, TcpClient client, HaMqttClient mqttClient, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;            
            Client = client;
            ArduinoCommandHandler = new ArduinoCommandHandler(client.GetStream(), cancellationToken);
            Arduino = new ArduinoProxy(ArduinoCommandHandler);
            
            MqttClient = mqttClient;
            Settings = settings;
            Status = TaskStatus.Created;
        }

        public async Task Start()
        {
            await Task.Yield();
            
            if (Status == TaskStatus.Running)
                return;
            
            if (Status == TaskStatus.RanToCompletion || Status == TaskStatus.Canceled)
                throw new Exception("Connection stopped and cannot be restarted.");

            Status = TaskStatus.Running;
            
            ArduinoCommandHandler.Start();

            DeviceId = await Arduino.GetDeviceId();
            Sensors = Settings.Sensors.Where(s => s.DeviceId == DeviceId).ToArray();
            SensorHandler = new AggregateSensorHandler(Arduino, MqttClient);
            
            MqttClient.AddConnection(this);

            RunTask = Run();
            
            MonitorCancellation();
        }

        public void Stop()
        {
            if (Status == TaskStatus.Running)
                Status = TaskStatus.Canceled;

            if (Status == TaskStatus.Canceled)
            {
                try
                {
                    // ReSharper disable once MethodSupportsCancellation
                    RunTask.Wait();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error joining to connection thread: {e}");
                }
                
                ArduinoCommandHandler.Stop();
                StopInternal();
            }
        }

        private async void MonitorCancellation()
        {
            await Task.Yield();

            try
            {
                await Task.Delay(-1, CancellationToken);
            }
            catch (TaskCanceledException)
            {
                Stop();
            }
        }

        private async Task OnConnect()
        {
            await Task.Yield();
            
            Console.WriteLine($"Client {DeviceId} connected on {Client.Client.RemoteEndPoint}.");

            await Task.WhenAll(Sensors.Select(SensorHandler.OnConnect));
        }

        private async Task OnLoop()
        {
            await Task.Yield();
            await Task.WhenAll(Sensors.Select(SensorHandler.OnLoop));
        }

        async Task IHaMqttConnection.PublishPinStates()
        {
            await Task.Yield();
            await Task.WhenAll(Sensors.Select(SensorHandler.OnPublish));
        }

        async Task IHaMqttConnection.SetSwitchState(string sensorId, bool state)
        {
            await Task.Yield();
            
            var sensor = Sensors.FirstOrDefault(s => s.Id == sensorId);

            if (sensor == null)
                return;
            
            await SensorHandler.OnSetState(sensor, state);
        }

        private async Task Run()
        {
            try
            {
                await OnConnect();

                while (Status == TaskStatus.Running)
                {
                    if (!Client.Connected)
                    {
                        StopInternal();
                        return;
                    }
                    
                    await Arduino.Noop();
                    await OnLoop();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception in connection thread: {e}");
                
                StopInternal();
            }
        }

        private void StopInternal()
        {
            if (Status == TaskStatus.RanToCompletion)
                return;
            
            Console.WriteLine($"Client {DeviceId} disconnected from {Client.Client.RemoteEndPoint}.");

            MqttClient.RemoveConnection(this);
            
            try
            {
                Client.Client.Shutdown(SocketShutdown.Both);
                Client.Client.Close();
                Client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error closing client: {e}");
            }
            
            Status = TaskStatus.RanToCompletion;
        }

        public void Dispose()
        {
            StopInternal();
            
            ArduinoCommandHandler?.Dispose();
            Client?.Dispose();
        }
    }
}