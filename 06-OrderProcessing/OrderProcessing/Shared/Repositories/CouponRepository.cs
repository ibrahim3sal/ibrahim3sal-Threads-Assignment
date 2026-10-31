
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Shared.Repositories;

public class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Coupon?> GetByCodeAsync(
        string code,
        CancellationToken ct)
    {
        Log.Info("CouponRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"CouponRepository -> Executing query for Coupon '{code}'...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Coupon> coupons =
            await _store.ReadAllAsync<Coupon>(DataFiles.Coupons, ct);

        Coupon? coupon = coupons.FirstOrDefault(
            c => string.Equals(
                c.Code,
                code,
                StringComparison.OrdinalIgnoreCase));

        if (coupon is null)
        {
            Log.Error($"Coupon '{code}' not found.");
            return null;
        }

        Log.Success($"Coupon '{code}' found.");
        return coupon;
    }
}
