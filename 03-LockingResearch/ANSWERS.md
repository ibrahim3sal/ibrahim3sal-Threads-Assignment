# Assignment 03 — Answers

Student name:  ibrahim ali
Group / ID:  3

1. The Monitor class is used to synchronize access to shared resources between multiple threads.

It ensures that only one thread at a time can execute a critical section protected by the same lock object.

It provides methods such as Enter, Exit, TryEnter, Wait, Pulse, and PulseAll.

2. When using lock with an object, the C# compiler translates it into Monitor.Enter() and Monitor.Exit() wrapped inside a try-finally block.

The finally block guarantees that the lock is released even if an exception occurs.

3. Simplicity: lock is easier to write and automatically handles acquiring and releasing the lock.

Exception safety: lock automatically releases the lock through a finally block, while using Monitor directly requires us to implement try-finally.

Advanced control: Monitor provides additional methods such as TryEnter, Wait, and Pulse, which are not directly available through the lock statement.

4. TryEnter: When a thread should attempt to acquire a lock without waiting indefinitely. It can also specify a timeout.

Wait: When a thread needs to wait for a condition to become true. It releases the lock while waiting and reacquires it before returning.

Pulse: When a thread changes a shared object's state and wants to notify a waiting thread that it can check the condition again.

5.
Using lock

```csharp
private static readonly object _lock = new();
private static int counter = 0;

public static void IncrementWithLock()
{
    lock (_lock)
    {
        counter++;
    }
}
```

Using Monitor.Enter / Monitor.Exit

```csharp
private static readonly object _lock = new();
   private static int counter = 0;

    public static void IncrementWithMonitor()
    {
        bool lockTaken = false;

        try
        {
            Monitor.Enter(_lock, ref lockTaken);

            counter++;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_lock);
        }
    }
```

Links:

[monitor](https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor?view=net-10.0)
[lock](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)