namespace ImageForge.App;

internal sealed class FatalUiErrorState
{
    private int requested;
    public bool Requested => Volatile.Read(ref requested) != 0;
    public bool BeginShutdown() => Interlocked.CompareExchange(ref requested, 1, 0) == 0;
}
