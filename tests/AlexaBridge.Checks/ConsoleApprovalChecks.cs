using AlexaBridge;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;

internal static class ConsoleApprovalChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        using (var input = new TestInput())
        using (var output = new PromptOutput())
        {
            var approval = new ConsoleActionApproval(input, output, true, TimeSpan.FromSeconds(3));
            var first = approval.ApproveAsync("test action", CancellationToken.None);
            var code = await output.NextCodeAsync();
            input.Send("approve " + code);
            check(await first, "fresh host challenge grants exactly its pending approval");

            var next = approval.ApproveAsync("next action", CancellationToken.None);
            var nextCode = await output.NextCodeAsync();
            check(code != nextCode, "next request receives a different challenge");
            input.Send("approve " + code);
            input.Send("deny " + nextCode);
            check(!await next, "replaying a consumed approval cannot authorize the next request");

            var active = approval.ApproveAsync("active action", CancellationToken.None);
            var activeCode = await output.NextCodeAsync();
            check(!await approval.ApproveAsync("concurrent action", CancellationToken.None), "concurrent request cannot share a pending approval");
            input.Send("deny " + activeCode);
            check(!await active, "explicit current-request denial is enforced");

            using var cancellation = new CancellationTokenSource();
            var cancelled = approval.ApproveAsync("cancelled action", cancellation.Token);
            var cancelledCode = await output.NextCodeAsync();
            cancellation.Cancel();
            var cancellationObserved = false;
            try { await cancelled; }
            catch (OperationCanceledException) { cancellationObserved = true; }
            check(cancellationObserved, "pending approval observes cancellation");
            var afterCancellation = approval.ApproveAsync("after cancellation", CancellationToken.None);
            var afterCode = await output.NextCodeAsync();
            input.Send("approve " + cancelledCode);
            input.Send("deny " + afterCode);
            check(!await afterCancellation, "cancelled challenge cannot approve a later request");

            var eof = approval.ApproveAsync("console closes", CancellationToken.None);
            _ = await output.NextCodeAsync();
            input.Complete();
            check(!await eof, "closed host input denies a pending action");
            check(!await approval.ApproveAsync("no console", CancellationToken.None), "closed host input denies future actions without waiting");
        }

        using (var input = new TestInput())
        using (var output = new PromptOutput())
        {
            var approval = new ConsoleActionApproval(input, output, true, TimeSpan.FromMilliseconds(300));
            var expired = approval.ApproveAsync("expires", CancellationToken.None);
            var oldCode = await output.NextCodeAsync();
            check(!await expired, "approval expiry denies the request");
            var later = approval.ApproveAsync("later action", CancellationToken.None);
            var currentCode = await output.NextCodeAsync();
            input.Send("approve " + oldCode);
            input.Send("deny " + currentCode);
            check(!await later, "expired challenge cannot authorize a later request");
        }
    }

    private sealed class TestInput : TextReader
    {
        private readonly BlockingCollection<string> _lines = new();
        public void Send(string line) => _lines.Add(line);
        public void Complete() => _lines.CompleteAdding();
        public override string? ReadLine()
        {
            try { return _lines.Take(); }
            catch (InvalidOperationException) { return null; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) Complete();
            base.Dispose(disposing);
        }
    }

    private sealed class PromptOutput : TextWriter
    {
        private readonly Channel<string> _codes = Channel.CreateUnbounded<string>();
        public override Encoding Encoding => Encoding.UTF8;
        public override void WriteLine(string? value)
        {
            var match = Regex.Match(value ?? "", @"approve ([0-9A-F]{12})");
            if (match.Success) _codes.Writer.TryWrite(match.Groups[1].Value);
        }
        public async Task<string> NextCodeAsync() => await _codes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    }
}
