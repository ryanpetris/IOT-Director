using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IotDirector.Mqtt;
using IotDirector.Settings;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector.Connection
{
    public class Connection : IDisposable, IMqttConnection
    {
        Guid IMqttConnection.Id { get; } = Guid.NewGuid();
        string IMqttConnection.DeviceId => Arduino.DeviceId;
        
        private TcpClient Client { get; }
        private ArduinoProxy Arduino { get; }
        
        private HaMqttClient MqttClient { get; }
        
        private CancellationToken CancellationToken { get; }
        private AppSettings Settings { get; }
        private Sensor[] Sensors { get; }
        private SensorHandler SensorHandler { get; }
        
        private TaskStatus Status { get; set; }
        private Thread Thread { get; }
        
        public Connection(AppSettings settings, TcpClient client, HaMqttClient mqttClient, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;            
            Client = client;
            Arduino = new ArduinoProxy(client.GetStream());
            
            MqttClient = mqttClient;
            
            Settings = settings;
            Sensors = Settings.Sensors.Where(s => s.DeviceId == Arduino.DeviceId).ToArray();
            
            Status = TaskStatus.Created;
            Thread = new Thread(Run);
            
            SensorHandler = new AggregateSensorHandler(Arduino, MqttClient);

            MqttClient.AddConnection(this);
            
            MonitorCancellation();
        }

        public void Start()
        {
            if (Status == TaskStatus.Running)
                return;
            
            if (Status == TaskStatus.RanToCompletion || Status == TaskStatus.Canceled)
                throw new Exception("Connection stopped and cannot be restarted.");

            Status = TaskStatus.Running;
            Thread.Start();
        }

        public void Stop()
        {
            if (Status == TaskStatus.Running)
                Status = TaskStatus.Canceled;

            if (Status == TaskStatus.Canceled)
            {
                try
                {
                    Thread.Join();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error joining to connection thread: {e}");
                }

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

        private void OnConnect()
        {
            Console.WriteLine($"Client {Arduino.DeviceId} connected on {Client.Client.RemoteEndPoint}.");

            foreach (var sensor in Sensors)
                SensorHandler.OnConnect(sensor);
        }

        private void OnLoop()
        {
            foreach (var sensor in Sensors)
                SensorHandler.OnLoop(sensor);
        }

        void IMqttConnection.PublishPinStates()
        {
            foreach (var sensor in Sensors)
                SensorHandler.OnPublish(sensor);
        }

        void IMqttConnection.SetSwitchState(string sensorId, bool state)
        {
            var sensor = Sensors.FirstOrDefault(s => s.Id == sensorId);

            if (sensor == null)
                return;
            
            SensorHandler.OnSetState(sensor, state);
        }

        private void Run()
        {
            try
            {
                OnConnect();

                while (Status == TaskStatus.Running)
                {
                    if (!Client.Connected)
                    {
                        StopInternal();
                        return;
                    }
                    
                    Arduino.Noop();
                    OnLoop();
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
            
            Console.WriteLine($"Client {Arduino.DeviceId} disconnected from {Client.Client.RemoteEndPoint}.");

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
            
            Arduino?.Dispose();
            Client?.Dispose();
        }
    }
}