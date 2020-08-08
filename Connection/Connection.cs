using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IotDirector.Commands;
using IotDirector.Mqtt;
using IotDirector.Settings;
using AppSettings = IotDirector.Settings.Settings;

namespace IotDirector.Connection
{
    public class Connection : IDisposable, IMqttConnection
    {
        private TcpClient Client { get; }
        private ArduinoProxy Arduino { get; }
        
        private HaMqttClient MqttClient { get; }
        
        private AppSettings Settings { get; }
        private Sensor[] Sensors { get; }
        private PinState PinState { get; } = new PinState();
        
        private TaskStatus Status { get; set; }
        private Thread Thread { get; }

        Guid IMqttConnection.Id { get; } = Guid.NewGuid();
        string IMqttConnection.DeviceId => Arduino.DeviceId;
        
        public Connection(AppSettings settings, TcpClient client, HaMqttClient mqttClient)
        {
            Client = client;
            Arduino = new ArduinoProxy(client.GetStream());
            
            MqttClient = mqttClient;
            
            Settings = settings;
            Sensors = Settings.Sensors.Where(s => s.DeviceId == Arduino.DeviceId).ToArray();
            
            Status = TaskStatus.Created;
            Thread = new Thread(Run);

            MqttClient.AddConnection(this);
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

        private void OnConnect()
        {
            Console.WriteLine($"Client {Arduino.DeviceId} connected on {Client.Client.RemoteEndPoint}.");

            foreach (var sensor in Sensors)
            {
                switch (sensor.Type)
                {
                    case SensorType.Digital:
                    {
                        Arduino.SetPinMode(sensor.Pin, PinMode.InputPullup);
                        
                        break;
                    }

                    case SensorType.Analog:
                    {
                        Arduino.SetPinMode(sensor.Pin, PinMode.Input);
                        
                        break;
                    }

                    case SensorType.Switch:
                    {
                        var switchSensor = (SwitchSensor) sensor;
                        var state = switchSensor.DefaultState;

                        if (switchSensor.Invert)
                            state = !state;

                        PinState.Set(sensor.Pin, state);
                        Arduino.SetPinMode(sensor.Pin, PinMode.Output, state);
                        MqttClient.PublishSensorState(sensor, switchSensor.DefaultState);
                        
                        break;
                    }
                    
                }
            }
        }

        private void OnLoop()
        {
            foreach (var sensor in Sensors)
            {
                switch (sensor.Type)
                {
                    case SensorType.Digital:
                    {
                        var digitalSensor = (DigitalSensor) sensor;
                        var state = Arduino.DigitalRead(digitalSensor.Pin);
                        
                        if (!PinState.Set(sensor.Pin, state))
                            continue;
                        
                        if (digitalSensor.Invert)
                            state = !state;
                        
                        Console.WriteLine($"{sensor.Name} state changed to {(state ? "on" : "off")}.");
                        MqttClient.PublishSensorState(sensor, state);
                        
                        break;
                    }
                    
                    case SensorType.Analog:
                    {
                        var analogSensor = (AnalogSensor) sensor;
                        var value = Arduino.AnalogRead(analogSensor.Pin);
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
                        
                        if (!PinState.Set(sensor.Pin, pinState))
                            continue;

                        if (!status)
                        {
                            Console.WriteLine($"{sensor.Name} state changed to offline.");
                        }
                        else
                        {
                            Console.WriteLine($"{sensor.Name} state changed to {(state ? "on" : "off")}.");
                        }

                        MqttClient.PublishSensorStatus(sensor, status);
                        MqttClient.PublishSensorState(sensor, state);
                        
                        break;
                    }
                        
                }
            }
        }

        void IMqttConnection.PublishPinStates()
        {
            foreach (var sensor in Sensors)
            {
                if (!PinState.Has(sensor.Pin))
                    continue;

                switch (sensor.Type)
                {
                    case SensorType.Digital:
                    {
                        var digitalSensor = (DigitalSensor) sensor;
                        var digitalState = PinState.GetBool(sensor.Pin);

                        if (digitalSensor.Invert)
                            digitalState = !digitalState;

                        Console.WriteLine($"Send {sensor.Name} state as {(digitalState ? "on" : "off")}.");
                        MqttClient.PublishSensorState(sensor, digitalState);

                        break;
                    }

                    case SensorType.Analog:
                    {
                        var state = PinState.Get(sensor.Pin);
                        var analogStatus = state >= 0;
                        var analogState = state == 1;

                        if (!analogStatus)
                        {
                            Console.WriteLine($"Send {sensor.Name} state as offline.");
                        }
                        else
                        {
                            Console.WriteLine($"Send {sensor.Name} state as {(analogState ? "on" : "off")}.");
                        }

                        MqttClient.PublishSensorStatus(sensor, analogStatus);
                        MqttClient.PublishSensorState(sensor, analogState);
                        
                        break;
                    }
                    
                    case SensorType.Switch:
                    {
                        var switchSensor = (SwitchSensor) sensor;
                        var switchState = PinState.GetBool(sensor.Pin);

                        if (switchSensor.Invert)
                            switchState = !switchState;

                        Console.WriteLine($"Send {sensor.Name} state as {(switchState ? "on" : "off")}.");
                        MqttClient.PublishSensorState(sensor, switchState);

                        break;
                    }
                }
            }
        }

        void IMqttConnection.SetSwitchState(string sensorId, bool state)
        {
            var sensor = Sensors.FirstOrDefault(s => s.Id == sensorId) as SwitchSensor;

            if (sensor == null)
                return;

            var pinState = state;

            if (sensor.Invert)
                pinState = !pinState;
            
            if (!PinState.Set(sensor.Pin, pinState))
                return;
            
            Arduino.DigitalWrite(sensor.Pin, pinState);
                        
            Console.WriteLine($"{sensor.Name} state changed to {(state ? "on" : "off")} via MQTT.");
            MqttClient.PublishSensorState(sensor, state);
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
            Console.WriteLine($"Client {Arduino.DeviceId} disconnected from {Client.Client.RemoteEndPoint}.");

            MqttClient.RemoveConnection(this);
            
            try
            {
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
            Arduino?.Dispose();
            Client?.Dispose();
        }
    }
}