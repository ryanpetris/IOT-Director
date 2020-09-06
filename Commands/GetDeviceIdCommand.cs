namespace IotDirector.Commands
{
    public class GetDeviceIdCommand : Command
    {
        public override CommandType CommandType => CommandType.GetDeviceId;
        public override bool ExpectResult => true;

        public GetDeviceIdCommand()
        {
            
        }

        public override string ToString()
        {
            return $"{(char) CommandType}";
        }
    }
}