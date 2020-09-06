using System.Threading;

namespace IotDirector.Connection
{
    public class ArduinoCommandResult
    {
        private int CommandId { get; }
        private CancellationTokenSource CancellationTokenSource { get; }

        public CancellationToken CancellationToken => CancellationTokenSource.Token;
        public string Result { get; private set; }

        public ArduinoCommandResult(int commandId)
        {
            CommandId = commandId;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void SetResult(string result)
        {
            Result = result;
            CancellationTokenSource.Cancel();
        }
    }
}