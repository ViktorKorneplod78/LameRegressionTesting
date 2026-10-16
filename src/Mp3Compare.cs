using System.Security.Cryptography;

namespace LameRegression;

// Хэши mp3-файлов для сравнения версий
public static class Mp3Compare
{
    // SHA-256 всего файла
    public static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // SHA-256 только аудиокадров. LAME записывает в первый кадр Xing/Info-тег со строкой версии
    // (например "LAME3.100"), поэтому файлы разных версий всегда отличаются целиком.
    // Этот хэш показывает, отличается ли само закодированное аудио.
    public static string AudioSha256(string path) =>
        Convert.ToHexString(SHA256.HashData(AudioFrames(File.ReadAllBytes(path))));

    // Возвращает данные без ID3v2, без кадра с Xing/Info-тегом и без ID3v1
    private static byte[] AudioFrames(byte[] data)
    {
        int start = SkipId3v2(data);
        int end = data.Length;

        int frameLength = FrameLength(data, start);
        if (frameLength > 0 && start + frameLength <= end && HasInfoTag(data, start, frameLength))
            start += frameLength;

        // ID3v1: последние 128 байт, начинаются с "TAG"
        if (end - 128 >= start && data[end - 128] == 'T' && data[end - 127] == 'A' && data[end - 126] == 'G')
            end -= 128;

        return data[start..end];
    }

    private static int SkipId3v2(byte[] d)
    {
        if (d.Length < 10 || d[0] != 'I' || d[1] != 'D' || d[2] != '3')
            return 0;
        // размер тега — 4 байта по 7 значащих бит
        int size = (d[6] << 21) | (d[7] << 14) | (d[8] << 7) | d[9];
        return 10 + size;
    }

    private static bool HasInfoTag(byte[] d, int start, int length)
    {
        var frame = d.AsSpan(start, length);
        return frame.IndexOf("Xing"u8) >= 0 || frame.IndexOf("Info"u8) >= 0;
    }

    private static readonly int[] BitratesMpeg1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
    private static readonly int[] BitratesMpeg2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
    private static readonly int[] SampleRatesMpeg1 = { 44100, 48000, 32000 };

    // Длина MPEG Layer III кадра по его заголовку; 0, если заголовок не распознан
    private static int FrameLength(byte[] d, int pos)
    {
        if (pos + 4 > d.Length || d[pos] != 0xFF || (d[pos + 1] & 0xE0) != 0xE0)
            return 0;

        int version = (d[pos + 1] >> 3) & 3; // 3 = MPEG1, 2 = MPEG2, 0 = MPEG2.5
        int layer = (d[pos + 1] >> 1) & 3;   // 1 = Layer III
        int bitrateIndex = d[pos + 2] >> 4;
        int sampleRateIndex = (d[pos + 2] >> 2) & 3;
        int padding = (d[pos + 2] >> 1) & 1;

        if (version == 1 || layer != 1 || bitrateIndex is 0 or 15 || sampleRateIndex == 3)
            return 0;

        bool mpeg1 = version == 3;
        int bitrate = (mpeg1 ? BitratesMpeg1 : BitratesMpeg2)[bitrateIndex] * 1000;
        int sampleRate = SampleRatesMpeg1[sampleRateIndex] / (version == 3 ? 1 : version == 2 ? 2 : 4);

        return (mpeg1 ? 144 : 72) * bitrate / sampleRate + padding;
    }
}
