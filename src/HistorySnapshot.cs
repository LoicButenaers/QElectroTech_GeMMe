using System.IO;
using System.IO.Compression;
using System.Text;

namespace GeMMeElec;

// Lossless in-memory snapshots: the existing 64 MB budget now counts compressed
// data, so a large document can retain useful undo depth. Project files stay JSON.
internal sealed class HistorySnapshot(byte[] data, int characters)
{
    public long MemoryBytes => data.LongLength + 64;
    public static HistorySnapshot Capture(string state)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, true))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false), 16384)) writer.Write(state);
        return new(buffer.ToArray(), state.Length);
    }
    public string Restore()
    {
        using var buffer = new MemoryStream(data, false); using var gzip = new GZipStream(buffer, CompressionMode.Decompress); using var reader = new StreamReader(gzip, new UTF8Encoding(false, true));
        var state = reader.ReadToEnd(); if (state.Length != characters) throw new InvalidDataException("Historique incomplet ; le projet courant est conservé."); return state;
    }
}
