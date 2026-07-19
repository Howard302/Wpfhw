using System.Text.Json.Serialization;

namespace wpfhw;

public class ModSearchHit
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    public string DownloadsDisplay => FormatDownloads(Downloads);

    private static string FormatDownloads(long count)
    {
        if (count >= 100000000)
            return $"{count / 100000000.0:F1}亿次下载";
        if (count >= 10000)
            return $"{count / 10000.0:F1}万次下载";
        if (count >= 1000)
            return $"{count / 1000.0:F1}千次下载";
        return $"{count}次下载";
    }
}