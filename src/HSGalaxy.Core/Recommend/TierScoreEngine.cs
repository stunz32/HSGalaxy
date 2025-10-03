using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HSGalaxy.Core.Data;

namespace HSGalaxy.Core.Recommend
{
    public sealed class TierScoreEngine
    {
        private readonly TierList _tiers;

        public TierScoreEngine(TierList tiers)
        {
            _tiers = tiers;
        }

        public static TierList LoadDefault(string? folder = null)
        {
            try
            {
                string root = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "tiers");
                string file = Path.Combine(root, "tiers.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var t = JsonSerializer.Deserialize<TierList>(json, opt);
                    if (t != null) return t;
                }
            }
            catch { }
            return TierList.Empty();
        }

        public ScoreResult ScoreCard(HearthstoneDataManager.Card card, string playerClass, List<HearthstoneDataManager.Card> currentDeck)
        {
            playerClass = (playerClass ?? "NEUTRAL").ToUpperInvariant();
            float baseScore = _tiers.GetScore(card.Name, playerClass);
            float synergy = CalculateSynergyBonus(card, currentDeck);
            float curve = CalculateCurveModifier(card, currentDeck);
            float total = Math.Clamp(baseScore + synergy + curve, 0f, 100f);
            var reasons = new List<string> { $"Base:{baseScore:F1}" };
            if (Math.Abs(synergy) > 0.01f) reasons.Add($"Synergy:{synergy:+0.0;-0.0}");
            if (Math.Abs(curve) > 0.01f) reasons.Add($"Curve:{curve:+0.0;-0.0}");
            return new ScoreResult(total, string.Join(" | ", reasons));
        }

        private static float CalculateSynergyBonus(HearthstoneDataManager.Card card, List<HearthstoneDataManager.Card> deck)
        {
            float bonus = 0f;
            // Tribal synergy
            if (!string.IsNullOrWhiteSpace(card.Race))
            {
                int tribeCount = deck.Count(c => string.Equals(c.Race, card.Race, StringComparison.OrdinalIgnoreCase));
                bonus += Math.Min(tribeCount * 2.0f, 10f);
            }
            // Spell synergies (rough)
            if (!string.IsNullOrWhiteSpace(card.Text) && card.Text.IndexOf("Spell Damage", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int spells = deck.Count(c => string.Equals(c.Type, "SPELL", StringComparison.OrdinalIgnoreCase));
                bonus += Math.Min(spells * 1.5f, 5f);
            }
            return Math.Clamp(bonus, -15f, 15f);
        }

        private static float CalculateCurveModifier(HearthstoneDataManager.Card card, List<HearthstoneDataManager.Card> deck)
        {
            var curve = deck.GroupBy(c => c.Cost).ToDictionary(g => g.Key, g => g.Count());
            // Ideal curve 2-4 mana
            if (card.Cost >= 2 && card.Cost <= 4)
            {
                if (curve.GetValueOrDefault(card.Cost, 0) < 7)
                    return 5.0f;
            }
            // Penalty for too many expensive cards
            int heavy = curve.Where(x => x.Key >= 7).Sum(x => x.Value);
            if (card.Cost >= 7 && heavy >= 3) return -10.0f;
            return 0f;
        }
    }

    public sealed class TierList
    {
        public Dictionary<string, float> CardScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, float> ClassModifiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static TierList Empty() => new TierList();

        public float GetScore(string cardName, string playerClass)
        {
            float baseScore = 50f;
            if (!string.IsNullOrWhiteSpace(cardName) && CardScores.TryGetValue(cardName, out var s)) baseScore = s;
            float mod = 0f;
            if (!string.IsNullOrWhiteSpace(playerClass) && ClassModifiers.TryGetValue(playerClass, out var m)) mod = m;
            return Math.Clamp(baseScore + mod, 0f, 100f);
        }
    }

    public readonly struct ScoreResult
    {
        public float Score { get; }
        public string Rationale { get; }
        public ScoreResult(float score, string rationale) { Score = score; Rationale = rationale; }
        public override string ToString() => $"{Score:F1} | {Rationale}";
    }
}

