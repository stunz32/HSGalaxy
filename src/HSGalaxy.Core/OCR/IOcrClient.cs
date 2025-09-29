using System.Threading;
using System.Threading.Tasks;

namespace HSGalaxy.Core.OCR
{
    public interface IOcrClient
    {
        Task<OcrResult> RecognizeAsync(byte[] pngImage, CancellationToken ct = default);
        string Name { get; }
    }
}

