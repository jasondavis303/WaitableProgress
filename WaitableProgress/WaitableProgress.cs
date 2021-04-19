/*
    This is 99% a copy of https://referencesource.microsoft.com/#mscorlib/system/progress.cs

    The difference is I added a queue to hold values, a timer to deque and send to the consumer,
    and most importantly a method to WAIT UNTIL ALL VALUES HAVE BEEN CONSUMED!

    This exists because I often do something like this:

    ---------------------------------------------------------------

    IProgress<double> prog = new Progress<double>(p =>
    {
        Console.SetCursorPosition(0, 12);
        Console.Write("{0:0.00%}", p);
    };

    await SomeMethodAsync(prog);
    Console.WriteLine("SomeMethod Complete!");

    ---------------------------------------------------------------

    And about half the time, the last Console.WriteLine will run right before the final prog action - giving me screwy output.
    This fixes it - just call:
        await SomeMethodAsync(prog);
        await prog.WaitUntilDoneAsync();
        Console.WriteLine("SomeMethod Complete!");
*/

using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
    public class WaitableProgress<T> : IProgress<T>, IDisposable
    {
        private readonly SynchronizationContext m_synchronizationContext;
        private readonly Action<T> m_handler;
        private readonly SendOrPostCallback m_invokeHandlers;
        private readonly ConcurrentQueue<T> m_queue;
        private readonly Timers.Timer m_timer;

        public WaitableProgress(Action<T> handler, double interval = double.Epsilon)
        {
            m_handler = handler ?? throw new ArgumentNullException(nameof(handler));

            m_synchronizationContext = ProgressStatics.DefaultContext;

            Contract.Assert(m_synchronizationContext != null);
            m_invokeHandlers = new SendOrPostCallback(InvokeHandlers);

            m_queue = new ConcurrentQueue<T>();
            m_timer = new Timers.Timer(interval) { AutoReset = false };
            m_timer.Elapsed += m_timer_Elapsed;
            m_timer.Start();
        }

        public event EventHandler<T> ProgressChanged;

        public void Report(T value) => m_queue.Enqueue(value);

        private void InvokeHandlers(object state)
        {
            try
            {
                T value = (T)state;
                m_handler?.Invoke(value);
                ProgressChanged?.Invoke(this, value);
            }
            catch
            {
                //Swallow
            }
        }

        private void m_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Because WaitUntilDone is checking if queue.Count == 0,
            //Make sure it's not empty until after the handler is finished running

            //Peek to get the value,
            //Send to synchronously post the value,
            //Dequeue to remove the value from the queue

            if (m_queue.TryPeek(out T value))
            {
                m_synchronizationContext.Send(m_invokeHandlers, value);
                m_queue.TryDequeue(out _);
            }

            //If disposed, just swallow - were done
            try { m_timer.Start(); }
            catch { }
        }

        public void WaitUntilDone()
        {
            while (m_queue.Count > 0)
            {
                Thread.Sleep(100);
            }
        }

        public async Task WaitUntilDoneAsync(CancellationToken cancellationToken = default)
        {
            while (m_queue.Count > 0)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            m_timer.Dispose();
            while (m_queue.TryDequeue(out _)) { }
        }
    }
}