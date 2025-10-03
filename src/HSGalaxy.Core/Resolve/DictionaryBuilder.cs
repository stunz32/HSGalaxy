using System;
using System.Collections.Generic;
using System.IO;
using HSGalaxy.Core.Data;

namespace HSGalaxy.Core.Resolve
{
    public static class DictionaryBuilder
    {
        public static string Build(HearthstoneDataManager.CardDatabase db, string locale = "enUS")
        {
            var dictRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HSGalaxy", "dict");
            Directory.CreateDirectory(dictRoot);
            var path = Path.Combine(dictRoot, $"{locale}.symspell");
            using var sw = new StreamWriter(path, false);
            foreach (var c in db.Cards)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) continue;
                int freq = GetFrequency(c.Rarity);
                sw.WriteLine($"{c.Name}\t{freq}");
            }
            return path;
        }

        private static int GetFrequency(string rarity)
        {
            var r = (rarity ?? string.Empty).ToUpperInvariant();
            return r switch
            {
                "LEGENDARY" => 100,
                "EPIC" => 200,
                "RARE" => 500,
                _ => 1000
            };
        }
    }
}

