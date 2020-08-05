namespace IotDirector.Commands
{
    public enum CommandType
    {
        None = '\0',
        PinMode = 'M',
        DigitalRead = 'R',
        DigitalWrite = 'W',
        AnalogRead = 'A',
        Noop = 'N'
    }
}