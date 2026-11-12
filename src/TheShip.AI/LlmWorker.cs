using System.Collections.Concurrent;

namespace TheShip.AI;

public enum LlmPriority { Reading = 0, Dialogue = 1, Background = 2 }

/// <summary>
/// Owns the model and runs one call at a time on a background thread, most urgent first: the
/// reading of a line the player just typed beats a crew line, which beats background judgements.
/// The game loop never blocks on it. Ported from the Cities repo.
/// </summary>
public sealed class LlmWorker : IDisposable
{
    private readonly PriorityQueue<Func<Task>, (int, long)> _queue = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _loop;
    private long _sequence;

    public ILlmProvider Llm { get; }
    public int Pending { get { lock (_lock) return _queue.Count; } }
    public bool Busy { get; private set; }

    public LlmWorker(ILlmProvider llm)
    {
        Llm = llm;
        _loop = Task.Factory.StartNew(RunAsync, TaskCreationOptions.LongRunning).Unwrap();
    }

    public Task<T> Enqueue<T>(LlmPriority priority, Func<ILlmProvider, CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Run()
        {
            if (ct.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
                tcs.TrySetCanceled();
                return;
            }
            // Shutting down cancels the running call too (llama.cpp stops generating), not only the queue.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
            try
            {
                tcs.TrySetResult(await work(Llm, linked.Token));
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled();
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }
        lock (_lock)
            _queue.Enqueue(Run, ((int)priority, _sequence++));
        _signal.Release();
        return tcs.Task;
    }

    private async Task RunAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            Func<Task>? job;
            lock (_lock)
                _queue.TryDequeue(out job, out _);
            if (job is null)
                continue;
            Busy = true;
            await job();
            Busy = false;
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try { _loop.Wait(TimeSpan.FromSeconds(5)); } catch { /* shutting down */ }
        // Whatever was still queued is cancelled, so nothing awaits it forever.
        List<Func<Task>> left;
        lock (_lock)
        {
            left = new List<Func<Task>>();
            while (_queue.TryDequeue(out var job, out _))
                left.Add(job);
        }
        foreach (var job in left)
            _ = job();
        Llm.Dispose();
    }
}

/// <summary>
/// Work that has to happen on the game thread (anything that touches the voyage) queued from LLM
/// continuations, which run on the thread pool. The game drains it every frame.
/// </summary>
public sealed class MainThreadQueue
{
    private readonly ConcurrentQueue<Action> _actions = new();

    /// <summary>For tests: run everything immediately on the calling thread.</summary>
    public bool Immediate { get; init; }

    public Task<T> Run<T>(Func<T> func)
    {
        if (Immediate)
            return Task.FromResult(func());
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _actions.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    /// <summary>Game-thread work with nothing to return; completes once it has run.</summary>
    public Task Run(Action action) => Run(() =>
    {
        action();
        return true;
    });

    public void Post(Action action)
    {
        if (Immediate)
            action();
        else
            _actions.Enqueue(action);
    }

    public void Drain()
    {
        while (_actions.TryDequeue(out var action))
            action();
    }
}
