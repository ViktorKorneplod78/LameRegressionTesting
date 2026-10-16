namespace LameRegression;

// Параметры WAV-файла, прочитанные из заголовка
public record WavInfo(int Channels, int SampleRate, int BitsPerSample, double DurationSeconds)
{
    public override string ToString() =>
        $"{SampleRate} Hz, {BitsPerSample}-bit, {(Channels == 1 ? "mono" : Channels == 2 ? "stereo" : Channels + " ch")}, {DurationSeconds:F1} s";

    public static WavInfo Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidDataException($"{path} is not a RIFF file");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidDataException($"{path} is not a WAVE file");

        int channels = 0, sampleRate = 0, byteRate = 0, bits = 0;
        // Перебираем чанки, пока не найдём "data"; "fmt " идёт раньше
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string id = new string(reader.ReadChars(4));
            int size = reader.ReadInt32();
            if (id == "fmt ")
            {
                reader.ReadInt16(); // audio format
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                byteRate = reader.ReadInt32();
                reader.ReadInt16(); // block align
                bits = reader.ReadInt16();
                reader.BaseStream.Seek(size - 16, SeekOrigin.Current);
            }
            else if (id == "data")
            {
                double duration = byteRate > 0 ? (double)size / byteRate : 0;
                return new WavInfo(channels, sampleRate, bits, duration);
            }
            else
            {
                reader.BaseStream.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }
        throw new InvalidDataException($"{path}: data chunk not found");
    }
}
