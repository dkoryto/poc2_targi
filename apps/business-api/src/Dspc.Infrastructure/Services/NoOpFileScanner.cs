using Dspc.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dspc.Infrastructure.Services;

/// <summary>Demo scanner: accepts everything and logs. Replace with a ClamAV/ICAP adapter before production (SECURITY.md).</summary>
public sealed class NoOpFileScanner(ILogger<NoOpFileScanner> log) : IFileScanner
{
    public Task<(bool Clean, string? Reason)> ScanAsync(byte[] content, string fileName, CancellationToken ct)
    {
        log.LogInformation("NoOpFileScanner: {File} ({Bytes} bytes) accepted without scanning", fileName, content.Length);
        return Task.FromResult<(bool, string?)>((true, null));
    }
}
