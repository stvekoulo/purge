namespace PurgeWin.Services;

public sealed record ScanContext(string Key, PathScope Scope, CancellationToken Token, long Generation);

/// A result can only be applied to the exact context that requested it.
public sealed class ScopeSession : IDisposable
{
    private CancellationTokenSource _cancel = new();
    public ScanContext Current { get; private set; } = new("ALL", new PathScope([]), default, 0);

    public ScanContext Change(string key, PathScope scope)
    {
        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new();
        return Current = new(key, scope, _cancel.Token, Current.Generation + 1);
    }

    public bool IsCurrent(ScanContext context) => ReferenceEquals(Current, context) && !context.Token.IsCancellationRequested;
    public void Cancel() => _cancel.Cancel();
    public void Dispose() { _cancel.Cancel(); _cancel.Dispose(); }
}
