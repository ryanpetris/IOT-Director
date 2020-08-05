namespace IotDirector.Commands
{
    public abstract class Command
    {
        public virtual CommandType CommandType => CommandType.None;
        public virtual int Pin => 0;
        public virtual bool ExpectResult => false;
        
        protected virtual string AdditionalData => string.Empty;

        public override string ToString()
        {
            return $"{(char) CommandType}{Pin:D2}{AdditionalData}\n";
        }
    }
}