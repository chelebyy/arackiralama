namespace RentACar.API.Services;

public sealed class AvailabilityCacheInvalidationSignal
{
    private CancellationTokenSource _current = new();

    public CancellationToken Token => _current.Token;

    public void Invalidate()
    {
        var previousToken = Interlocked.Exchange(ref _current, new CancellationTokenSource());
        previousToken.Cancel();
    }
}
