namespace IotDirector.Commands
{
    public class NoopCommand : Command
    {
        public override CommandType CommandType => CommandType.Noop;
        public override bool ExpectResult => true;

        public NoopCommand()
        {
            
        }

        public override string ToString()
        {
            return $"{(char) CommandType}\n";
        }
    }
}