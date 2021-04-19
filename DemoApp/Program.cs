using System;
using System.Threading.Tasks;

namespace DemoApp
{
    class Program
    {
        static void Main()
        {
            RunAsync().Wait();

            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.Read();
        }

        static async Task RunAsync()
        {
            //This will create 10 events instantly, but take 10 seconds to finish running them

            WaitableProgress<int> progress = new(1000, p =>
            {
                Console.WriteLine(p);
            });

            await CreateLotsOEvents(progress);
            Console.WriteLine("Waiting for events to complete");
            await progress.WaitUntilDoneAsync();
            Console.WriteLine("Complete");
        }

        static Task CreateLotsOEvents(IProgress<int> progress)
        {
            for (int i = 1; i <= 10; i++)
                progress.Report(i);
            return Task.CompletedTask;
        }
    }
}
