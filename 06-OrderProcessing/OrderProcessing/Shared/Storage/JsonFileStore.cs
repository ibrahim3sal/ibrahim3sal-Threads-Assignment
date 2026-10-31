using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderProcessing.Shared.Storage;

public class JsonFileStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim>
        FileLocks = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static SemaphoreSlim GetLock(string path)
    {
        return FileLocks.GetOrAdd(
            Path.GetFullPath(path),
            _ => new SemaphoreSlim(1, 1));
    }

    // ==================== ASYNC METHODS ====================

    public async Task<List<T>> ReadAllAsync<T>(
        string fileName,
        CancellationToken ct)
    {
        string path = DataFiles.GetPath(fileName);
        SemaphoreSlim semaphore = GetLock(path);

        await semaphore.WaitAsync(ct);

        try
        {
            return await ReadInternalAsync<T>(path, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task WriteAllAsync<T>(
        string fileName,
        List<T> data,
        CancellationToken ct)
    {
        string path = DataFiles.GetPath(fileName);
        SemaphoreSlim semaphore = GetLock(path);

        await semaphore.WaitAsync(ct);

        try
        {
            await WriteInternalAsync(path, data, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task UpdateAsync<T>(
        string fileName,
        Func<List<T>, List<T>> update,
        CancellationToken ct)
    {
        string path = DataFiles.GetPath(fileName);
        SemaphoreSlim semaphore = GetLock(path);

        await semaphore.WaitAsync(ct);

        try
        {
            List<T> data = await ReadInternalAsync<T>(path, ct);

            data = update(data);

            await WriteInternalAsync(path, data, ct);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ==================== SYNC METHODS ====================

    public List<T> ReadAll<T>(string fileName)
    {
        string path = DataFiles.GetPath(fileName);

        SemaphoreSlim semaphore = GetLock(path);

        semaphore.Wait();

        try
        {
            return ReadInternal<T>(path);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void WriteAll<T>(string fileName, List<T> data)
    {
        string path = DataFiles.GetPath(fileName);

        SemaphoreSlim semaphore = GetLock(path);

        semaphore.Wait();

        try
        {
            WriteInternal(path, data);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Update<T>(
    string fileName,
    Func<List<T>, List<T>> update)
    {
        string path = DataFiles.GetPath(fileName);
        SemaphoreSlim semaphore = GetLock(path);

        semaphore.Wait();

        try
        {
            List<T> data = ReadInternal<T>(path);

            data = update(data);

            WriteInternal(path, data);
        }
        finally
        {
            semaphore.Release();
        }
    }

    // ==================== INTERNAL METHODS ====================

    private static async Task<List<T>> ReadInternalAsync<T>(
        string path,
        CancellationToken ct)
    {
        if (!File.Exists(path))
            return new List<T>();

        try
        {
            string json = await File.ReadAllTextAsync(path, ct);

            return JsonSerializer.Deserialize<List<T>>(
                json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(
                Path.GetFileName(path), ex);
        }
    }

    private static List<T> ReadInternal<T>(string path)
    {
        if (!File.Exists(path))
            return new List<T>();

        try
        {
            string json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<List<T>>(
                json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(
                Path.GetFileName(path), ex);
        }
    }

    private static async Task WriteInternalAsync<T>(
        string path,
        List<T> data,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tempPath = path + "." + Guid.NewGuid() + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);

            await File.WriteAllTextAsync(tempPath, json, ct);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void WriteInternal<T>(
        string path,
        List<T> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string tempPath = path + "." + Guid.NewGuid() + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);

            File.WriteAllText(tempPath, json);

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

}