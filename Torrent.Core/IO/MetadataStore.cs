using System.IO;
using System.Text.Json;
using Torrent.Core.Models;

namespace Torrent.Core.IO;

public static class MetadataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public static string Save(string filePath, TorrentMetadata metadata)
    {
        string metadataPath = filePath + ".ttmeta";
        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        File.WriteAllText(metadataPath, json);
        return metadataPath;
    }

    public static TorrentMetadata Load(string metadataPath)
    {
        string json = File.ReadAllText(metadataPath);
        TorrentMetadata? metadata = JsonSerializer.Deserialize<TorrentMetadata>(json);
        if (metadata == null)
        {
            throw new InvalidDataException("Metadata dosyasi okunamadi.");
        }

        return metadata;
    }
}
