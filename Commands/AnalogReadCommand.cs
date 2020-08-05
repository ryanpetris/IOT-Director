namespace IotDirector.Commands
{
    public class AnalogReadCommand : Command
    {
        public override CommandType CommandType => CommandType.AnalogRead;
        public override int Pin { get; }
        public override bool ExpectResult => true;

        public AnalogReadCommand(int pin)
        {
            Pin = pin;
        }
    }
}