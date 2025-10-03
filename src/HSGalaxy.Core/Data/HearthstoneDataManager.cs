using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HSGalaxy.Core.Net;

namespace HSGalaxy.Core.Data
{
    public sealed class HearthstoneDataManager
    {
        private const string LatestLocale = "enUS";
        private const string BaseUrl = "https://api.hearthstonejson.com/v1/latest";
        private string? _pinnedBuild;
        public CardDatabase Database { get; private set; } = new CardDatabase();

        public string CacheRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "cache", "hearthstone");

        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(CacheRoot);
            // Use 'latest' snapshot directly (build.txt may not be present)
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _pinnedBuild = "latest";
            var cachePath = Path.Combine(CacheRoot, $"cards_{LatestLocale}_latest.json");
            if (!File.Exists(cachePath))
            {
                using var wc = new System.Net.WebClient();
                wc.Headers["User-Agent"] = "HSGalaxy/0.1";
                var json = wc.DownloadString($"{BaseUrl}/{LatestLocale}/cards.json");
                await File.WriteAllTextAsync(cachePath, json);
            }
            // Load from cache
            var raw = await File.ReadAllTextAsync(cachePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var cards = JsonSerializer.Deserialize<List<HsJsonCard>>(raw, options) ?? new List<HsJsonCard>();
            Database = CardDatabase.FromHsJson(cards);
            // Save pinned build id
            await File.WriteAllTextAsync(Path.Combine(CacheRoot, "pinned_build.txt"), _pinnedBuild);
        }

        public sealed class CardDatabase
        {
            public List<Card> Cards { get; set; } = new List<Card>();
            public static CardDatabase FromHsJson(List<HsJsonCard> hs)
            {
                var db = new CardDatabase();
                foreach (var c in hs)
                {
                    // Only ingest cards with a name and id
                    if (string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)) continue;
                    db.Cards.Add(new Card
                    {
                        Id = c.Id!,
                        Name = c.Name!,
                        PlayerClass = string.IsNullOrWhiteSpace(c.Class) ? (c.Classes?.FirstOrDefault() ?? "NEUTRAL") : c.Class!,
                        Cost = c.Cost ?? 0,
                        Collectible = c.Collectible ?? false,
                        Set = c.Set ?? string.Empty,
                        Rarity = c.Rarity ?? string.Empty,
                        Type = c.Type ?? string.Empty,
                        Race = (c.Race ?? c.MinionType ?? string.Empty).ToUpperInvariant(),
                        Text = c.Text ?? string.Empty
                    });
                }
                return db;
            }
        }

        public sealed class Card
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string PlayerClass { get; set; } = "NEUTRAL"; // NEUTRAL, MAGE, etc.
            public int Cost { get; set; }
            public bool Collectible { get; set; }
            public string Set { get; set; } = string.Empty;
            public string Rarity { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Race { get; set; } = string.Empty; // MURLOC, MECHANICAL, etc.
            public string Text { get; set; } = string.Empty;

            public bool IsArenaEligible()
            {
                // Simple heuristic: must be collectible and not a Hero/Enchantment token
                if (!Collectible) return false;
                var t = (Type ?? string.Empty).ToUpperInvariant();
                if (t.Contains("HERO") || t.Contains("ENCHANTMENT")) return false;
                return true;
            }
        }

        public sealed class HsJsonCard
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("cardClass")] public string? Class { get; set; }
            [JsonPropertyName("classes")] public List<string>? Classes { get; set; }
            [JsonPropertyName("cost")] public int? Cost { get; set; }
            [JsonPropertyName("collectible")] public bool? Collectible { get; set; }
            [JsonPropertyName("set")] public string? Set { get; set; }
            [JsonPropertyName("rarity")] public string? Rarity { get; set; }
            [JsonPropertyName("type")] public string? Type { get; set; }
            [JsonPropertyName("race")] public string? Race { get; set; }
            [JsonPropertyName("minionType")] public string? MinionType { get; set; }
            [JsonPropertyName("text")] public string? Text { get; set; }
        }
    }
}
