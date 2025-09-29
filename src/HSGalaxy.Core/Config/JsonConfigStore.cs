using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HSGalaxy.Core.Config
{
    /// <summary>
    /// Simple JSON configuration store for POCO settings.
    /// </summary>
    public sealed class JsonConfigStore<T> where T : class, new()
    {
        private readonly string _filePath;

        public JsonConfigStore(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public async Task<T> LoadAsync()
        {
            if (!File.Exists(_filePath))
            {
                var fresh = new T();
                await SaveAsync(fresh).ConfigureAwait(false);
                return fresh;
            }

            var json = await File.ReadAllTextAsync(_filePath, Encoding.UTF8).ConfigureAwait(false);
            var obj = JsonConvert.DeserializeObject<T>(json);
            return obj ?? new T();
        }

        public Task SaveAsync(T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonConvert.SerializeObject(value, Formatting.Indented);
            return File.WriteAllTextAsync(_filePath, json, Encoding.UTF8);
        }
    }
}

