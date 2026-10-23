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
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TimeSpan _timeout;
    private volatile bool _inputEnded;

    public ConsoleActionApproval(bool denyActions)
        : this(Console.In, Console.Out, !denyActions && Environment.UserInteractive && !Console.IsInputRedirected, TimeSpan.FromSeconds(30))
    { }

    // In-process test seam only. There is no network argument or environment bypass for approval.
    internal ConsoleActionApproval(TextReader input, TextWriter output, bool interactive, TimeSpan timeout)
    {
        _input = input;
        _output = output;
        _timeout = timeout;
        _interactive = interactive;
        if (_interactive)
            new Thread(ReadConsole) { IsBackground = true, Name = "Local action approval" }.Start();
    }

    public async Task<bool> ApproveAsync(string action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_interactive || _inputEnded || !await _gate.WaitAsync(0, cancellationToken)) return false;
        var pending = new Pending(Convert.ToHexString(RandomNumberGenerator.GetBytes(6)),
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        try
        {
            _pending = pending;
            if (_inputEnded) return false;
            _output.WriteLine($"\nLOCAL APPROVAL: {action}\nType approve {pending.Code} within {_timeout.TotalSeconds:g} seconds, or deny {pending.Code}.\nEach approval applies to this request only.");
            try { return await pending.Completion.Task.WaitAsync(_timeout, cancellationToken); }
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
            while (_input.ReadLine() is { } line)
            {
                var pending = _pending;
                if (pending is null) continue;
                if (string.Equals(line.Trim(), "approve " + pending.Code, StringComparison.Ordinal))
                    pending.Completion.TrySetResult(true);
                else if (string.Equals(line.Trim(), "deny " + pending.Code, StringComparison.Ordinal))
                    pending.Completion.TrySetResult(false);
            }
        }
        catch (IOException) { }
        finally
        {
            _inputEnded = true;
            _pending?.Completion.TrySetResult(false);
        }
    }
}
