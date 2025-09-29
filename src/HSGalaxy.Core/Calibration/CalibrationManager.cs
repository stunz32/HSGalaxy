using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HSGalaxy.Core.Calibration
{
    public static class CalibrationManager
    {
        public static async Task SaveAsync(CalibrationProfile profile, string folder)
        {
            Directory.CreateDirectory(folder);
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            await File.WriteAllTextAsync(Path.Combine(folder, $"{profile.Name}.json"), json, Encoding.UTF8);
        }

        public static async Task<CalibrationProfile?> LoadAsync(string folder, string name)
        {
            var path = Path.Combine(folder, $"{name}.json");
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return JsonConvert.DeserializeObject<CalibrationProfile>(json);
        }
    }
}

