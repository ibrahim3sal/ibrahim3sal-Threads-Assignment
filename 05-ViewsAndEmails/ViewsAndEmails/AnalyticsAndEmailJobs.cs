
namespace ViewsAndEmails;

public sealed class AnalyticsAndEmailJobs
{
    public readonly Dictionary<string, int> Views = new();
    public readonly Queue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    private readonly object _viewsSync = new();
    private readonly object _emailSync = new();
    private readonly object _sentSync = new();

    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        lock (_viewsSync)
        {
            if (!Views.ContainsKey(product))
                Views.Add(product, 0);

            Views[product]++;
        }
    }

    public void PlaceOrder(int orderId)
    {
        lock (_emailSync)
        {
            EmailJobs.Enqueue($"Email: order {orderId}");
        }
    }

    public void EmailWorker()
    {
        while (true)
        {
            string? job = null;

            lock (_emailSync)
            {
                if (EmailJobs.Count > 0)
                    job = EmailJobs.Dequeue();

                else if (ProducersDone)
                    return;
            }

            if (job is not null)
            {
                lock (_sentSync)
                {
                    SentEmails.Add(job);
                }

                continue;
            }

            Thread.Sleep(1);
        }
    }
}