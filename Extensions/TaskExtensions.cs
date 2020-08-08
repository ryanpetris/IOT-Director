using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IotDirector.Extensions
{
    public static class TaskExtensions
    {
        public static async void LoopUntilCancelled(this Func<Task> action, CancellationToken cancellationToken, TimeSpan quietPeriod)
        {
            await Task.Yield();
            
            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        cancellationToken.ThrowIfCancellationRequested();

                    await action();

                    if (cancellationToken.IsCancellationRequested)
                        cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await Task.Delay(quietPeriod, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // This block intentionally left empty.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var name = Regex.Replace(action.Method.Name, "([A-Z]+)", " $1").Trim();
                
                Console.WriteLine($"{name} thread shutdown.");
            }
        }
    }
}