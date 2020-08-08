using System;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using IotDirector.Commands;

namespace IotDirector.Connection
{
    public class ArduinoProxy : IDisposable
    {
        private const int StreamReadTimeout = 1000;
        
        private NetworkStream Stream { get; }
        
        public string DeviceId { get; }

        public ArduinoProxy(NetworkStream stream)
        {
            Stream = stream;
            Stream.ReadTimeout = StreamReadTimeout;
            
            DeviceId = GetDeviceId();
        }

        public int AnalogRead(int pin)
        {
            var raw = SendCommand(new AnalogReadCommand(pin));
            
            if (!int.TryParse(raw, out var result) || result < 0 || result > 1023)
                throw new ArduinoException($"Invalid Analog Read result: {raw}");

            return result;
        }

        public bool DigitalRead(int pin)
        {
            var raw = SendCommand(new DigitalReadCommand(pin));

            if (!int.TryParse(raw, out var result) || result < 0 || result > 1)
                throw new ArduinoException($"Invalid Digital Read result: {raw}");

            return result == 1;
        }

        public void DigitalWrite(int pin, bool value)
        {
            SendCommand(new DigitalWriteCommand(pin, value));
        }

        public void Noop()
        {
            var raw = SendCommand(new NoopCommand());
            
            if (raw != "N")
                throw new ArduinoException($"Invalid Noop result: {raw}");
        }

        public void SetPinMode(int pin, PinMode mode)
        {
            SendCommand(new PinModeCommand(pin, mode));
            
            if (mode == PinMode.Output)
                DigitalWrite(pin, false);
        }

        public void SetPinMode(int pin, PinMode mode, bool state)
        {
            if (mode != PinMode.Output)
                throw new ArduinoException($"Cannot set initial state for pin mode {mode}.");
            
            SendCommand(new PinModeCommand(pin, mode));
            DigitalWrite(pin, state);
        }
        
        private string GetDeviceId()
        {
            var clientId = ReadLine();

            if (!Regex.IsMatch(clientId, "([0-9A-F]{2}:){5}[0-9A-F]{2}", RegexOptions.IgnoreCase))
                throw new Exception($"Invalid Client ID: {clientId}");

            return clientId;
        }
        
        private string ReadLine()
        {
            var result = new StringBuilder();

            while (true)
            {
                var data = (char) Stream.ReadByte();
                
                if (data == '\r')
                    continue;

                if (data == '\n')
                    break;
                    
                result.Append(data);
            }

            return result.ToString();
        }

        private string SendCommand(Command command)
        {
            if (command.ExpectResult)
            {
                var bytes = new byte[256];
                
                while (Stream.DataAvailable)
                {
                    Stream.Read(bytes, 0, bytes.Length);
                }
            }

            var commandBytes = Encoding.ASCII.GetBytes(command.ToString());

            Stream.Write(new ReadOnlySpan<byte>(commandBytes));

            if (command.ExpectResult)
                return ReadLine();

            return null;
        }

        public void Dispose()
        {
            Stream?.Dispose();
        }
    }
}