using System.Net;
using System.Text.Json;

namespace StardewBrowser.Platform.Content;

public static class StaticAssetDownload
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<byte[]> GetBytesAsync(HttpClient http, string path, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await http.GetByteArrayAsync(path, cancellationToken); }
            catch (HttpRequestException error) when (attempt < 3 && error.StatusCode is
                HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                // Static hosts can temporarily reject an otherwise valid asset.
                // Bound retries, preserve cancellation, and verify bytes after download.
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
            }
        }
    }

    public static async Task<T?> GetJsonAsync<T>(HttpClient http, string path, CancellationToken cancellationToken)
        => JsonSerializer.Deserialize<T>(await GetBytesAsync(http, path, cancellationToken), JsonOptions);
}
