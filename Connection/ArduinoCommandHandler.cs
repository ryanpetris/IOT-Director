using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IotDirector.Commands;

namespace IotDirector.Connection
{
    public class ArduinoCommandHandler : IDisposable
    {
        private const int StreamReadTimeout = 1000;
        private const int CommandResultTimeout = 5000;
        
        private NetworkStream Stream { get; }
        private CancellationToken CancellationToken { get; }
        
        private TaskStatus Status { get; set; }
        private Thread ReceiveThread { get; }
        
        private int Counter { get; set; }
        private ReaderWriterLockSlim SendLock { get; }
        private ConcurrentDictionary<int, ArduinoCommandResult> Results { get; }

        public ArduinoCommandHandler(NetworkStream stream, CancellationToken cancellationToken)
        {
            Stream = stream;
            Stream.ReadTimeout = StreamReadTimeout;

            CancellationToken = cancellationToken;
            
            Status = TaskStatus.Created;
            ReceiveThread = new Thread(DoReceive);

            Counter = 0;
            SendLock = new ReaderWriterLockSlim();
            Results = new ConcurrentDictionary<int, ArduinoCommandResult>();
        }

        public async Task<string> Send(Command command)
        {
            var commandId = await SendInternal(command);

            if (!command.ExpectResult)
                return null;

            var result = new ArduinoCommandResult(commandId);

            Results.TryAdd(commandId, result);

            try
            {
                await Task.Delay(CommandResultTimeout, result.CancellationToken);
            }
            catch (TaskCanceledException) { }

            Results.TryRemove(commandId, out _);

            return result.Result;
        }

        public void Start()
        {
            if (Status == TaskStatus.Running)
                return;
            
            if (Status == TaskStatus.RanToCompletion || Status == TaskStatus.Canceled)
                throw new Exception("Connection stopped and cannot be restarted.");

            Status = TaskStatus.Running;
            ReceiveThread.Start();
        }

        public void Stop()
        {
            if (Status == TaskStatus.Running)
                Status = TaskStatus.Canceled;

            if (Status == TaskStatus.Canceled)
            {
                try
                {
                    ReceiveThread.Join();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error joining to receive thread: {e}");
                }

                StopInternal();
            }
        }

        private void DoReceive()
        {
            try
            {
                while (Status == TaskStatus.Running)
                {
                    if (CancellationToken.IsCancellationRequested)
                        CancellationToken.ThrowIfCancellationRequested();

                    var line = ReadLine();

                    if (string.IsNullOrEmpty(line) || !line.StartsWith("C"))
                        continue;

                    var commandIdText = line.Substring(1, 4).TrimStart('0');

                    if (string.IsNullOrEmpty(commandIdText))
                        commandIdText = "0";

                    var commandId = int.Parse(commandIdText);

                    if (Results.TryGetValue(commandId, out var result))
                    {
                        result.SetResult(line.Substring(5));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception in Arduino Command Handler thread: {e}");
                
                StopInternal();
            }
        }

        private Task<int> GetNextCommandId()
        {
            Counter = (Counter + 1) % 10000;

            if (Counter == 0)
                Counter = 1;
            
            return Task.FromResult(Counter);
        }
        
        private string ReadLine()
        {
            try
            {
                var result = new StringBuilder();

                while (true)
                {
                    var data = (char) Stream.ReadByte();

                    if (data == '\r')
                        continue;

                    if (data == '\n')
                        break;

                    result.Append(data);
                }

                return result.ToString();
            }
            catch (IOException)
            {
                return null;
            }
        }

        private async Task<int> SendInternal(Command command)
        {
            await Task.Yield();
            
            SendLock.EnterWriteLock();

            try
            {
                var commandId = await GetNextCommandId();
                var commandText = $"C{commandId.ToString().PadLeft(4, '0')}{command}\n";
                var commandBytes = Encoding.ASCII.GetBytes(commandText);

                Stream.Write(new ReadOnlySpan<byte>(commandBytes));

                return commandId;
            }
            finally
            {
                SendLock.ExitWriteLock();
            }
        }

        private void StopInternal()
        {
            if (Status == TaskStatus.RanToCompletion)
                return;
            
            Status = TaskStatus.RanToCompletion;
        }

        public void Dispose()
        {
            StopInternal();
            
            Stream?.Dispose();
        }
    }
}