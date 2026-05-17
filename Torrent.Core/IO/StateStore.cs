using System;
using System.IO;
using System.Text.Json;
using Torrent.Core.Models;

namespace Torrent.Core.IO;

public static class StateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public static string GetStatePath(string targetFilePath)
    {
        return targetFilePath + ".ttstate";
    }

    public static TorrentState LoadOrCreate(string targetFilePath, string infoHash, int totalPieces)
    {
        string statePath = GetStatePath(targetFilePath);
        if (!File.Exists(statePath))
        {
            return new TorrentState
            {
                InfoHash = infoHash,
                TargetFilePath = targetFilePath,
                CompletedPieces = new bool[totalPieces],
                LastUpdatedUtc = DateTime.UtcNow
            };
        }

        string json = File.ReadAllText(statePath);
        TorrentState? state = JsonSerializer.Deserialize<TorrentState>(json);
        if (state == null || !string.Equals(state.InfoHash, infoHash, StringComparison.OrdinalIgnoreCase))
        {
            return new TorrentState
            {
                InfoHash = infoHash,
                TargetFilePath = targetFilePath,
                CompletedPieces = new bool[totalPieces],
                LastUpdatedUtc = DateTime.UtcNow
            };
        }

        if (state.CompletedPieces.Length != totalPieces)
        {
            bool[] resized = new bool[totalPieces];
            Array.Copy(state.CompletedPieces, resized, Math.Min(state.CompletedPieces.Length, totalPieces));
            state.CompletedPieces = resized;
        }

        state.TargetFilePath = targetFilePath;
        return state;
    }

    public static void Save(TorrentState state)
    {
        state.LastUpdatedUtc = DateTime.UtcNow;
        string json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(GetStatePath(state.TargetFilePath), json);
    }
}
