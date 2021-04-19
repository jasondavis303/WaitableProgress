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
        await prog.WaitUntilDoneAsync();

    after calling SomeMethodAsync
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
        private readonly System.Timers.Timer m_timer;

        public WaitableProgress()
        {
            m_synchronizationContext = ProgressStatics.DefaultContext;

            Contract.Assert(m_synchronizationContext != null);
            m_invokeHandlers = new SendOrPostCallback(InvokeHandlers);

            m_queue = new ConcurrentQueue<T>();
            m_timer = new System.Timers.Timer(double.Epsilon) { AutoReset = false };
            m_timer.Elapsed += m_timer_Elapsed;
            m_timer.Start();
        }

        public WaitableProgress(Action<T> handler) : this()
        {
            m_handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public event EventHandler<T> ProgressChanged;

        protected virtual void OnReport(T value) => m_queue.Enqueue(value);

        void IProgress<T>.Report(T value) { OnReport(value); }

        private void InvokeHandlers(object state)
        {
            T value = (T)state;

            Action<T> handler = m_handler;
            EventHandler<T> changedEvent = ProgressChanged;

            handler?.Invoke(value);
            changedEvent?.Invoke(this, value);
        }

        private void m_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (m_queue.TryDequeue(out T value))
            {
                Action<T> handler = m_handler;
                EventHandler<T> changedEvent = ProgressChanged;
                if (handler != null || changedEvent != null)
                    m_synchronizationContext.Post(m_invokeHandlers, value);
            }

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