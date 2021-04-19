[![release](https://github.com/jasondavis303/WaitableProgress/actions/workflows/release.yml/badge.svg)](https://github.com/jasondavis303/WaitableProgress/actions/workflows/release.yml)

Nuget Library: https://www.nuget.org/packages/WaitableProgress/

## Why

This is 99% a copy of https://referencesource.microsoft.com/#mscorlib/system/progress.cs

The difference is I added a queue to hold values, a timer to deque and send to the consumer,
and most importantly a method to WAIT UNTIL ALL VALUES HAVE BEEN CONSUMED!

This exists because I often do something like this:

```csharp
IProgress<double> prog = new Progress<double>(p =>
{
    Console.SetCursorPosition(0, 12);
    Console.Write("{0:0.00%}", p);
};

await SomeMethodAsync(prog);
Console.WriteLine("SomeMethod Complete!");
```

And about half the time, the last Console.WriteLine will run right before the final prog action - giving me screwy output.

This fixes it - just create WaitableProgress<T> and call:

```csharp
await SomeMethodAsync(prog);
await prog.WaitUntilDoneAsync();
Console.WriteLine("SomeMethod Complete!");
```