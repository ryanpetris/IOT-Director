using System;

namespace IotDirector.Connection
{
    public class ArduinoException : Exception
    {
        public ArduinoException(string message) : base(message)
        {
            
        }

        public ArduinoException(string message, Exception innerException) : base(message, innerException)
        {
            
        }
    }
}