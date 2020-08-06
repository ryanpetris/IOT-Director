using System;
using System.Collections.Generic;
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
    public class Connection : IDisposable
    {
        private TcpClient Client { get; }
        private ArduinoProxy Arduino { get; }
        
        private HaMqttClient MqttClient { get; }
        
        private AppSettings Settings { get; }
        private Sensor[] Sensors { get; }
        private Dictionary<int, int> PinStates { get; }
        
        private TaskStatus Status { get; set; }
        private Thread Thread { get; }
        
        public Connection(AppSettings settings, TcpClient client, HaMqttClient mqttClient)
        {
            Client = client;
            Arduino = new ArduinoProxy(client.GetStream());
            
            MqttClient = mqttClient;
            
            Settings = settings;
            Sensors = Settings.Sensors.Where(s => s.DeviceId == Arduino.ClientId).ToArray();
            PinStates = new Dictionary<int, int>();
            
            Status = TaskStatus.Created;
            Thread = new Thread(Run);

            MqttClient.OnSendPinStateEvent += OnSendPinState;
            MqttClient.OnSwitchSetEvent += OnSwitchSet;
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
            Console.WriteLine($"Client {Arduino.ClientId} connected on {Client.Client.RemoteEndPoint}.");

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
                        
                        Arduino.SetPinMode(sensor.Pin, PinMode.Output, switchSensor.Invert);
                        PinStates[sensor.Pin] = switchSensor.Invert ? 1 : 0;
                        
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
                        
                        if (!SavePinState(sensor.Pin, state))
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
                        
                        if (!SavePinState(sensor.Pin, pinState))
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

        private void OnSendPinState(object sender, EventArgs args)
        {
            foreach (var sensor in Sensors)
            {
                if (!PinStates.ContainsKey(sensor.Pin))
                    continue;

                var state = PinStates[sensor.Pin];

                switch (sensor.Type)
                {
                    case SensorType.Digital:
                    {
                        var digitalSensor = (DigitalSensor) sensor;
                        var digitalState = state == 1;

                        if (digitalSensor.Invert)
                            digitalState = !digitalState;

                        Console.WriteLine($"Send {sensor.Name} state as {(digitalState ? "on" : "off")}.");
                        MqttClient.PublishSensorState(sensor, digitalState);

                        break;
                    }

                    case SensorType.Analog:
                    {
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
                        var switchState = state == 1;

                        if (switchSensor.Invert)
                            switchState = !switchState;

                        Console.WriteLine($"Send {sensor.Name} state as {(switchState ? "on" : "off")}.");
                        MqttClient.PublishSensorState(sensor, switchState);

                        break;
                    }
                }
            }
        }

        private void OnSwitchSet(object sender, SwitchSetEventArgs args)
        {
            var sensor = Sensors.FirstOrDefault(s => s.Id == args.SensorId) as SwitchSensor;

            if (sensor == null)
                return;

            var state = args.NewState;

            if (sensor.Invert)
                state = !state;
            
            if (!SavePinState(sensor.Pin, state))
                return;
            
            Arduino.DigitalWrite(sensor.Pin, state);
                        
            Console.WriteLine($"{sensor.Name} state changed to {(args.NewState ? "on" : "off")} via MQTT.");
            MqttClient.PublishSensorState(sensor, args.NewState);
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

        private bool SavePinState(int pin, bool value)
        {
            return SavePinState(pin, value ? 1 : 0);
        }

        private bool SavePinState(int pin, int value)
        {
            var changed = !PinStates.ContainsKey(pin) || PinStates[pin] != value;

            PinStates[pin] = value;

            return changed;
        }

        private void StopInternal()
        {
            Console.WriteLine($"Client {Arduino.ClientId} disconnected from {Client.Client.RemoteEndPoint}.");

            // ReSharper disable once DelegateSubtraction
            MqttClient.OnSendPinStateEvent -= OnSendPinState;
            // ReSharper disable once DelegateSubtraction
            MqttClient.OnSwitchSetEvent -= OnSwitchSet;
            
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