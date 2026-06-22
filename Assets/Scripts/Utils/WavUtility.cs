using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Tiện ích chuyển đổi AudioClip sang mảng byte WAV định dạng PCM 16-bit và ngược lại hoàn toàn trong bộ nhớ (RAM).
/// </summary>
public static class WavUtility
{
    private const int HeaderSize = 44;

    /// <summary>
    /// Chuyển đổi AudioClip thành mảng byte WAV (PCM 16-bit).
    /// </summary>
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[WavUtility] AudioClip null!");
            return null;
        }

        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream))
            {
                WriteWavHeader(writer, clip.channels, clip.frequency, samples.Length);
                WriteSamples(writer, samples);
            }
            return stream.ToArray();
        }
    }

    /// <summary>
    /// Chuyển đổi mảng byte WAV (PCM 16-bit) thành AudioClip trong bộ nhớ.
    /// </summary>
    public static AudioClip ToAudioClip(byte[] wavBytes, string clipName = "CustomRecording")
    {
        if (wavBytes == null || wavBytes.Length < HeaderSize)
        {
            Debug.LogError("[WavUtility] Mảng byte WAV không hợp lệ hoặc quá ngắn!");
            return null;
        }

        try
        {
            using (var stream = new MemoryStream(wavBytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    // Đọc RIFF header
                    string riff = new string(reader.ReadChars(4));
                    if (riff != "RIFF")
                    {
                        Debug.LogError("[WavUtility] Không phải định dạng RIFF!");
                        return null;
                    }

                    reader.ReadInt32(); // File size - 8

                    string wave = new string(reader.ReadChars(4));
                    if (wave != "WAVE")
                    {
                        Debug.LogError("[WavUtility] Không phải định dạng WAVE!");
                        return null;
                    }

                    // Đọc format subchunk
                    string fmt = new string(reader.ReadChars(4));
                    // Đôi khi có thể có các chunk khác trước fmt, nhưng ta giả định cấu trúc WAV chuẩn
                    if (fmt != "fmt ")
                    {
                        Debug.LogError("[WavUtility] Không tìm thấy chunk fmt!");
                        return null;
                    }

                    int fmtSize = reader.ReadInt32();
                    int audioFormat = reader.ReadInt16(); // 1 = PCM
                    int channels = reader.ReadInt16();
                    int sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // Byte rate
                    reader.ReadInt16(); // Block align
                    int bitsPerSample = reader.ReadInt16();

                    // Nhảy qua các bytes dư thừa nếu chunk fmtSize > 16
                    if (fmtSize > 16)
                    {
                        reader.ReadBytes(fmtSize - 16);
                    }

                    // Tìm data chunk
                    string dataSignature = new string(reader.ReadChars(4));
                    while (dataSignature != "data" && stream.Position < stream.Length - 4)
                    {
                        int chunkSize = reader.ReadInt32();
                        reader.ReadBytes(chunkSize);
                        dataSignature = new string(reader.ReadChars(4));
                    }

                    if (dataSignature != "data")
                    {
                        Debug.LogError("[WavUtility] Không tìm thấy chunk data!");
                        return null;
                    }

                    int dataSize = reader.ReadInt32();
                    int sampleCount = dataSize / (bitsPerSample / 8);
                    
                    float[] floatSamples = new float[sampleCount];

                    if (bitsPerSample == 16)
                    {
                        for (int i = 0; i < sampleCount; i++)
                        {
                            if (stream.Position >= stream.Length) break;
                            short sample = reader.ReadInt16();
                            floatSamples[i] = sample / 32768.0f;
                        }
                    }
                    else if (bitsPerSample == 8)
                    {
                        for (int i = 0; i < sampleCount; i++)
                        {
                            if (stream.Position >= stream.Length) break;
                            byte sample = reader.ReadByte();
                            floatSamples[i] = (sample - 128) / 128.0f;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[WavUtility] Chưa hỗ trợ định dạng {bitsPerSample}-bit!");
                        return null;
                    }

                    int totalSamplesPerChannel = floatSamples.Length / channels;
                    AudioClip clip = AudioClip.Create(clipName, totalSamplesPerChannel, channels, sampleRate, false);
                    clip.SetData(floatSamples, 0);

                    return clip;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WavUtility] Lỗi giải mã WAV: {ex.Message}");
            return null;
        }
    }

    private static void WriteWavHeader(BinaryWriter writer, int channels, int sampleRate, int totalSamples)
    {
        writer.Seek(0, SeekOrigin.Begin);

        writer.Write(Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(36 + totalSamples * 2); // File size - 8 (PCM 16-bit = 2 bytes/sample)
        writer.Write(Encoding.UTF8.GetBytes("WAVE"));
        writer.Write(Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16); // Subchunk 1 size (16 cho định dạng PCM)
        writer.Write((short)1); // Audio format (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); // Byte rate (SampleRate * Channels * BytesPerSample)
        writer.Write((short)(channels * 2)); // Block align (Channels * BytesPerSample)
        writer.Write((short)16); // Bits per sample (16-bit)
        writer.Write(Encoding.UTF8.GetBytes("data"));
        writer.Write(totalSamples * 2); // Subchunk 2 size (Dung lượng phần dữ liệu âm thanh)
    }

    private static void WriteSamples(BinaryWriter writer, float[] samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            // Ép từ float (-1.0f -> 1.0f) sang short 16-bit (-32768 -> 32767)
            short intSample = (short)Mathf.Clamp(samples[i] * 32768f, -32768f, 32767f);
            writer.Write(intSample);
        }
    }
}
