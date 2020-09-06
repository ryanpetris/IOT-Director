using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IotDirector.Commands;

namespace IotDirector.Connection
{
    public class ArduinoProxy
    {
        private ArduinoCommandHandler CommandHandler { get; }

        public ArduinoProxy(ArduinoCommandHandler commandHandler)
        {
            CommandHandler = commandHandler;
        }

        public async Task<int> AnalogRead(int pin)
        {
            var raw = await CommandHandler.Send(new AnalogReadCommand(pin));
            
            if (!int.TryParse(raw, out var result) || result < 0 || result > 1023)
                throw new ArduinoException($"Invalid Analog Read result: {raw}");

            return result;
        }

        public async Task<bool> DigitalRead(int pin)
        {
            var raw = await CommandHandler.Send(new DigitalReadCommand(pin));

            if (!int.TryParse(raw, out var result) || result < 0 || result > 1)
                throw new ArduinoException($"Invalid Digital Read result: {raw}");

            return result == 1;
        }

        public async Task DigitalWrite(int pin, bool value)
        {
            await CommandHandler.Send(new DigitalWriteCommand(pin, value));
        }
        
        public async Task<string> GetDeviceId()
        {
            var clientId = await CommandHandler.Send(new GetDeviceIdCommand());

            if (!Regex.IsMatch(clientId, "([0-9A-F]{2}:){5}[0-9A-F]{2}", RegexOptions.IgnoreCase))
                throw new Exception($"Invalid Client ID: {clientId}");

            return clientId;
        }

        public async Task Noop()
        {
            var raw = await CommandHandler.Send(new NoopCommand());
            
            if (raw != "N")
                throw new ArduinoException($"Invalid Noop result: {raw}");
        }

        public async Task SetPinMode(int pin, PinMode mode)
        {
            await CommandHandler.Send(new PinModeCommand(pin, mode));
            
            if (mode == PinMode.Output)
                await DigitalWrite(pin, false);
        }

        public async Task SetPinMode(int pin, PinMode mode, bool state)
        {
            if (mode != PinMode.Output)
                throw new ArduinoException($"Cannot set initial state for pin mode {mode}.");
            
            await CommandHandler.Send(new PinModeCommand(pin, mode));
            await DigitalWrite(pin, state);
        }
    }
}