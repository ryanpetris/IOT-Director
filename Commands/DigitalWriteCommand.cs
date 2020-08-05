namespace IotDirector.Commands
{
    public class DigitalWriteCommand : Command
    {
        public override CommandType CommandType => CommandType.DigitalWrite;
        public override int Pin { get; }
        public bool Value { get; }

        protected override string AdditionalData => $"{(Value ? '1' : '0')}";
        
        public DigitalWriteCommand(int pin, bool value)
        {
            Pin = pin;
            Value = value;
        }
    }
}