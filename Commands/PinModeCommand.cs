namespace IotDirector.Commands
{
    public class PinModeCommand : Command
    {
        public override CommandType CommandType => CommandType.PinMode;
        public override int Pin { get; }
        public PinMode PinMode { get; }
        
        protected override string AdditionalData => $"{(char) PinMode}";

        public PinModeCommand(int pin, PinMode pinMode)
        {
            Pin = pin;
            PinMode = pinMode;
        }
    }
}