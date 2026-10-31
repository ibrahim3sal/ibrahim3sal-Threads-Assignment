namespace OrderProcessing.Phase4.Shipping;

public class ShippingQuote
{
    public string Carrier { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public long ElapsedMs { get; set; }
}