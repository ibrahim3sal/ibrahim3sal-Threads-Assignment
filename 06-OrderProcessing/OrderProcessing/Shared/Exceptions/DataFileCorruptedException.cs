namespace OrderProcessing.Shared.Storage;

public class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(
        string fileName,
        Exception innerException)
        : base($"The JSON file '{fileName}' is corrupted.", innerException)
    {
    }
}