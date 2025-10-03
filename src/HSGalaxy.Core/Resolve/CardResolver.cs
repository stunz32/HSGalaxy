using System;
using System.Collections.Generic;
using System.Linq;
using HSGalaxy.Core.Data;

namespace HSGalaxy.Core.Resolve
{
    public sealed class CardResolver
    {
        private readonly HearthstoneDataManager.CardDatabase _db;

        public CardResolver(HearthstoneDataManager.CardDatabase db)
        {
            _db = db;
        }

        public ResolveResult Resolve(string ocrText, float ocrConfidence, string playerClass)
        {
            playerClass = (playerClass ?? "NEUTRAL").ToUpperInvariant();
            var normalized = (ocrText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized)) return ResolveResult.None();

            // Stage 1: Exact match (strict)
            var exact = _db.Cards.FirstOrDefault(c => string.Equals(c.Name, normalized, StringComparison.OrdinalIgnoreCase));
            if (exact != null && ocrConfidence >= 0.90f)
                return ResolveResult.From(exact, 1.0f, "Exact match");

            // Stage 2: Fuzzy match (edit distance)
            var candidates = _db.Cards
                .Where(c => c.IsArenaEligible() && (c.PlayerClass.Equals("NEUTRAL", StringComparison.OrdinalIgnoreCase) || c.PlayerClass.Equals(playerClass, StringComparison.OrdinalIgnoreCase)))
                .Select(c => new { c, dist = EditDistance(normalized, c.Name) })
                .OrderBy(x => x.dist)
                .ThenBy(x => x.c.Cost)
                .Take(5)
                .ToList();

            if (candidates.Count == 0)
                return ResolveResult.None();

            var best = candidates[0];
            // Confidence shaping: map edit distance to [0,1], combine with OCR confidence
            int L = Math.Max(normalized.Length, best.c.Name.Length);
            float nameSim = 1.0f - (float)best.dist / Math.Max(L, 1);
            float combined = Math.Min(1.0f, (ocrConfidence * 0.6f + nameSim * 0.4f));
            return ResolveResult.From(best.c, combined, $"fuzzy dist={best.dist}");
        }

        private static int EditDistance(string a, string b)
        {
            a = a ?? string.Empty; b = b ?? string.Empty;
            int n = a.Length, m = b.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            return dp[n, m];
        }
    }

    public sealed class ResolveResult
    {
        public string? CardId { get; set; }
        public string? CardName { get; set; }
        public float Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public static ResolveResult None() => new ResolveResult { CardId = null, CardName = null, Confidence = 0f, Reason = "No match" };
        public static ResolveResult From(HearthstoneDataManager.Card c, float conf, string reason) => new ResolveResult { CardId = c.Id, CardName = c.Name, Confidence = conf, Reason = reason };
    }
}

