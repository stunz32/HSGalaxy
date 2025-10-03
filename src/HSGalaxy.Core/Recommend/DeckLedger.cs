using System;
using System.Collections.Generic;
using System.Linq;
using HSGalaxy.Core.Data;

namespace HSGalaxy.Core.Recommend
{
    public sealed class DeckLedger
    {
        private readonly List<PickRecord> _picks = new();

        public IReadOnlyList<PickRecord> Picks => _picks;

        public sealed class PickRecord
        {
            public int PickNumber { get; set; }
            public HearthstoneDataManager.Card SelectedCard { get; set; } = new();
            public HearthstoneDataManager.Card[] Options { get; set; } = Array.Empty<HearthstoneDataManager.Card>();
            public float Score { get; set; }
            public string Rationale { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        public void RecordPick(HearthstoneDataManager.Card selected, HearthstoneDataManager.Card[] options, float score = 0, string rationale = "")
        {
            var rec = new PickRecord
            {
                PickNumber = _picks.Count + 1,
                SelectedCard = selected,
                Options = options ?? Array.Empty<HearthstoneDataManager.Card>(),
                Score = score,
                Rationale = rationale,
                Timestamp = DateTime.UtcNow
            };
            _picks.Add(rec);
        }

        public ReconcileResult ReconcileWithPanel(List<HearthstoneDataManager.Card> panelCards, double panelConfidence = 1.0)
        {
            if (panelCards == null) return new ReconcileResult { Success = false, Reason = "No panel data" };
            if (panelConfidence < 0.9) return new ReconcileResult { Success = false, Reason = "Low panel confidence" };
            if (panelCards.Count != _picks.Count)
            {
                return new ReconcileResult { Success = false, Reason = "Card count mismatch" };
            }
            for (int i = 0; i < panelCards.Count; i++)
            {
                if (!string.Equals(panelCards[i].Name, _picks[i].SelectedCard.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return new ReconcileResult { Success = false, Reason = $"Mismatch at pick {i + 1}" };
                }
            }
            return new ReconcileResult { Success = true, Reason = "OK" };
        }
    }

    public sealed class ReconcileResult
    {
        public bool Success { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
