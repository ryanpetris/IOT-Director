namespace IotDirector.Commands
{
    public class DigitalReadCommand : Command
    {
        public override CommandType CommandType => CommandType.DigitalRead;
        public override int Pin { get; }
        public override bool ExpectResult => true;

        public DigitalReadCommand(int pin)
        {
            Pin = pin;
        }
    }
}