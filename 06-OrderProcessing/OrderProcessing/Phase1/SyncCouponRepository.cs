using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Coupon? GetByCode(string code)
    {
        Log.Info("SyncCouponRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncCouponRepository -> Executing query for Coupon '{code}'...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        List<Coupon> coupons =
            _store.ReadAll<Coupon>(DataFiles.Coupons);

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