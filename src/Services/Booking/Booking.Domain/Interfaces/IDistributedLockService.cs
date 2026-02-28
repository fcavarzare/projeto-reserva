namespace Booking.Domain.Interfaces;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(string key, string value, TimeSpan expiration);
    Task ReleaseLockAsync(string key, string value);
}
