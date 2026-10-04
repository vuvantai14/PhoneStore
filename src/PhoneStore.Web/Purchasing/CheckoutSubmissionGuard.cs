using Microsoft.Extensions.Caching.Memory;

namespace PhoneStore.Web.Purchasing;

public sealed class CheckoutSubmissionGuard(IMemoryCache cache)
{
    private readonly object _sync = new();
    public Submission Get(int userId, string token)
    {
        lock (_sync)
        {
            return cache.GetOrCreate("checkout:" + userId + ":" + token, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return new Submission();
            })!;
        }
    }
    public sealed class Submission
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int? OrderId { get; set; }
    }
}

