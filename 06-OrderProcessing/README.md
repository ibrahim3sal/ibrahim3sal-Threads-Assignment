Phase 1

Q1. Why are the sync and async times almost the same when orders are awaited one after another?

Because async/await does not automatically execute operations in parallel. Each order is processed sequentially, and the next order starts only after the previous one completes.

Asynchronous operations can release the thread while waiting, but they do not necessarily reduce the total execution time when operations are awaited one by one.

Note: In this implementation, the async run also saves every order to orders.json, while the sync run does not save its results. Therefore, the two measured times do not represent exactly the same amount of work.

Q2. Is the thread ID before an await always the same as after it? Why?

No. The thread ID is not guaranteed to remain the same after an await.

When an asynchronous operation is waiting, the current thread can return to the ThreadPool. When the operation completes, the continuation may execute on a different available thread.

Q3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?

Thread.Sleep() blocks the current thread for the specified duration. That thread cannot perform other work during the sleep.

Task.Delay() does not block a thread while waiting. It allows the current thread to return to the ThreadPool so it can be used for other work.

Q4. Why should a repository method return Task<T> instead of T?

A repository often performs I/O operations such as reading from or writing to files or databases.

Returning Task<T> allows the caller to await the operation without blocking a thread while asynchronous I/O is in progress. It also allows cancellation and asynchronous operation composition.

Q5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?

File.ReadAllText() performs synchronous file reading and blocks the calling thread until the operation completes.

File.ReadAllTextAsync() performs asynchronous file reading and returns a Task<string> that can be awaited.

The asynchronous version generally does not need to keep a ThreadPool thread blocked while the operating system performs the asynchronous I/O operation.


phase 2


Q1: What is the difference between starting a task and awaiting it?

Starting a task means initiating an asynchronous operation. The operation can begin executing without blocking the current method.

Awaiting a task means asynchronously waiting for that operation to complete before continuing to the next statement.

You can start multiple tasks first and then await them later. This allows their operations to overlap instead of waiting for each one to finish before starting the next.

Q2: Does every Task create a new thread? Does Task.Delay() use a thread while waiting?

No, not every Task creates a new thread. A Task represents an asynchronous operation or a unit of work, not necessarily a dedicated thread.

Task.Delay() does not occupy a thread while waiting. It uses a timer to complete the task after the specified time. This allows the thread to be available for other work.

Q3: What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous?

.Result and .Wait() block the current thread until the task completes. This can reduce application performance and may cause deadlocks, especially in environments with a synchronization context, such as UI applications.

Using await is generally safer because it waits asynchronously without blocking the current thread.

A separate demo method was implemented to demonstrate the blocking behavior of .Result or .Wait(). This is the only place where these methods are used.

Q4: What happens to an exception thrown inside a task that is never awaited?

An exception thrown inside a Task is stored in the task as a faulted exception.

If the task is never awaited and its exception is never observed, the calling code may continue without noticing the failure. This can hide errors and make debugging difficult.

Awaiting the task allows the exception to propagate to the calling method, where it can be handled using try-catch.

Q5: When should you use Task.Run(), and when should you avoid it?

Task.Run() is useful for CPU-bound operations that would otherwise block the current thread. It schedules the work on a ThreadPool thread.

It should generally be avoided for naturally asynchronous I/O operations, such as database queries, file operations, and HTTP requests, because these operations already support asynchronous execution.

For example, Task.Run() is suitable for a CPU-intensive calculation, while File.ReadAllTextAsync() should be used directly for asynchronous file reading.

Q6: What is the difference between Task and Task<T>?

Task represents an asynchronous operation that does not return a result.

Task<T> represents an asynchronous operation that returns a value of type T when it completes.

For example:
- Task: Sending an email asynchronously.
- Task<int>: Calculating a value and returning an integer asynchronously.

Q7: Why can a lock block not contain an await?

A lock block cannot contain await because the lock must be released by the same thread that acquired it.

However, after await, execution may continue on a different thread. This would make it unsafe to release the lock.

For asynchronous synchronization, SemaphoreSlim can be used with WaitAsync() and Release().

Q8: Why did the unprotected stock reservation oversell, and how did you prevent it?

The unprotected stock reservation oversold because multiple tasks could read the same available stock before any of them updated it.

For example, if only one item is available, two tasks may both read the stock quantity as 1 and both decide that the reservation is possible. This can result in selling more items than are actually available.

I prevented this by protecting the complete stock reservation operation with a lock. Checking the available quantity and updating the stock must happen as one atomic operation.

This ensures that only one task can reserve the available stock at a time.


phase 3

1. When is await necessary?

await is necessary when the calling method needs to wait for an asynchronous operation to finish, use its result, or handle its exceptions and cancellation. It ensures that the operation completes before the method continues.

2. When is fire-and-forget acceptable?

Fire-and-forget can be acceptable for non-critical background operations that do not need to be completed before the caller returns, provided that their exceptions are handled and their execution is monitored independently.

It should not be used for critical operations such as order processing or payment confirmation when completion must be guaranteed.

3. Why is async void dangerous?

async void is dangerous because it does not return a Task, so callers cannot await its completion or catch its asynchronous exceptions using a normal try-catch around the call.

This makes error handling and execution flow difficult to control.

4. Why is async void acceptable for event handlers?

Event handlers often require a void return type because of their delegate signature, such as EventHandler.

Therefore, async void is acceptable for event handlers when necessary. However, exceptions should be handled inside the event handler using try-catch.


phase 4

Q1: For each of Tasks 10.1–10.4: Which tool did you use, and why can't "start, then await one by one" meet the requirement?

Task 10.1 — Task.WhenEach

I used Task.WhenEach to process all 20 orders concurrently and handle each task as soon as it completes.

Awaiting tasks one by one would process their results in the order they were started, not in the order they finished. This would not satisfy the live progress requirement.

Task 10.2 — Task.WhenAll

I used Task.WhenAll to start the four dashboard operations concurrently and wait for all of them with a single await.

Awaiting each operation one by one would make the total execution time close to the sum of all four operations instead of being close to the slowest operation.

Task 10.3 — Task.WhenAny

I used Task.WhenAny to identify the first shipping carrier that successfully returns a quote.

Awaiting carriers one by one would delay the selection of the fastest carrier because the program would wait for each carrier in sequence.

Task 10.4 A — Parallel.ForEach

I used Parallel.ForEach to distribute CPU-bound fraud scoring calculations across multiple processor cores.

Processing orders one by one would not take advantage of parallel execution and would take longer.

Task 10.4 B — SemaphoreSlim

I used SemaphoreSlim to limit the number of email operations running concurrently.

Processing emails one by one would be unnecessarily slow, while starting all emails without a limit could overload the email service. SemaphoreSlim provides controlled concurrency.

Q2: In 10.2, when several loads fail, how do you get every exception? What does a plain await give you?

When multiple tasks fail inside Task.WhenAll, the returned task stores all exceptions in its Exception property as an AggregateException.

A plain await Task.WhenAll(...) propagates one exception to the calling code, even if multiple tasks have failed.

To inspect every exception, we can keep a reference to the task returned by Task.WhenAll and examine its Exception.InnerExceptions property after it has completed with a faulted status.

For example:

Task allTasks = Task.WhenAll(tasks);

try
{
    await allTasks;
}
catch
{
    if (allTasks.Exception is not null)
    {
        foreach (Exception ex in allTasks.Exception.InnerExceptions)
        {
            Console.WriteLine(ex.Message);
        }
    }
}

Therefore, await allows the exception to propagate, while Exception.InnerExceptions provides access to all collected exceptions.

Q3: What happens to the losing carriers if you never cancel them?

If the losing carrier requests are not cancelled, they continue running even after the fastest carrier has returned its quote.

This wastes resources and allows unnecessary operations to continue.

Using CancellationTokenSource allows us to cancel the losing requests after selecting the fastest successful quote. Each carrier must observe its cancellation token to stop its operation.

Q4: Why does 10.4 A get faster with more CPU cores, while 10.4 B does not depend on the number of cores?

Task 10.4 A performs CPU-bound work. Parallel.ForEach distributes calculations across available CPU cores, allowing multiple calculations to execute in parallel. More available cores can increase performance.

Task 10.4 B performs asynchronous I/O operations, such as sending emails. Most of the time is spent waiting for the external email service rather than performing CPU calculations.

Therefore, email concurrency is mainly controlled by the SemaphoreSlim limit, not by the number of CPU cores.

Q5: What is the difference between parallel work (10.4 A) and concurrent asynchronous work (10.1)?

Parallel work means executing multiple operations simultaneously, usually using multiple CPU cores. It is particularly useful for CPU-bound operations.

Concurrent asynchronous work means allowing multiple operations to make progress during overlapping periods without necessarily executing them simultaneously on different threads. It is particularly useful for I/O-bound operations.

Task 10.4 A uses parallel execution for CPU-intensive fraud scoring, while Task 10.1 uses asynchronous concurrency to process orders and report their completion as soon as possible.