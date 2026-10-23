using System.Security.Cryptography;

namespace AlexaBridge;

public interface IActionApproval
{
    Task<bool> ApproveAsync(string action, CancellationToken cancellationToken);
}

/// <summary>MCP callers cannot approve themselves. Approval is read only from the host console.</summary>
public sealed class ConsoleActionApproval : IActionApproval
{
    private sealed record Pending(string Code, TaskCompletionSource<bool> Completion);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Pending? _pending;
    private readonly bool _interactive;

    public ConsoleActionApproval(bool denyActions)
    {
        _interactive = !denyActions && Environment.UserInteractive && !Console.IsInputRedirected;
        if (_interactive)
            new Thread(ReadConsole) { IsBackground = true, Name = "Local action approval" }.Start();
    }

    public async Task<bool> ApproveAsync(string action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_interactive || !await _gate.WaitAsync(0, cancellationToken)) return false;
        var pending = new Pending(Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        try
        {
            _pending = pending;
            Console.WriteLine($"\nLOCAL APPROVAL: {action}\nType approve {pending.Code} within 30 seconds, or deny {pending.Code}.\nEach approval applies to this request only.");
            try { return await pending.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken); }
            catch (TimeoutException) { return false; }
        }
        finally
        {
            _pending = null;
            _gate.Release();
        }
    }

    private void ReadConsole()
    {
        try
        {
            while (Console.ReadLine() is { } line)
            {
                var pending = _pending;
                if (pending is null) continue;
                if (string.Equals(line.Trim(), "approve " + pending.Code, StringComparison.Ordinal))
                    pending.Completion.TrySetResult(true);
                else if (string.Equals(line.Trim(), "deny " + pending.Code, StringComparison.Ordinal))
                    pending.Completion.TrySetResult(false);
            }
        }
        catch (IOException) { _pending?.Completion.TrySetResult(false); }
    }
}
