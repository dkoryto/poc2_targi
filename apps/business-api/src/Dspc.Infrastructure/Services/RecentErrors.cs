using Dspc.Application.Abstractions;

namespace Dspc.Infrastructure.Services;

public sealed class RecentErrorsBuffer : IRecentErrors
{
    private readonly object _lock = new();
    private readonly Queue<RecentError> _items = new();
    private const int Capacity = 50;

    public void Record(string operation, string message, string correlationId)
    {
        lock (_lock)
        {
            _items.Enqueue(new RecentError(DateTime.UtcNow, operation, message.Length > 300 ? message[..300] : message, correlationId));
            while (_items.Count > Capacity) _items.Dequeue();
        }
    }

    public IReadOnlyList<RecentError> List() { lock (_lock) return _items.Reverse().ToList(); }
}
