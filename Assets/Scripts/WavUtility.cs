using System;
using UnityEngine;

/// <summary>
/// Utility class to handle standard WAV audio file formatting.
/// </summary>
public static class WavUtility
{
    private const int HEADER_SIZE = 44;

    /// <summary>
    /// Converts an AudioClip into standard 16-bit PCM WAV formatted bytes.
    /// </summary>
    /// <param name="clip">The source AudioClip.</param>
    /// <returns>A byte array containing the WAV file contents.</returns>
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip), "Source AudioClip cannot be null.");
        }

        // Get raw float data from clip
        var buffer = new float[clip.samples * clip.channels];
        clip.GetData(buffer, 0);

        // Convert float data to 16-bit PCM byte array
        byte[] pcmData = new byte[buffer.Length * 2];
        for (int i = 0; i < buffer.Length; i++)
        {
            // Clamp float sample value between -1.0 and 1.0
            float sample = Mathf.Clamp(buffer[i], -1.0f, 1.0f);
            short value = (short)(sample * 32767f);
            byte[] bytes = BitConverter.GetBytes(value);
            pcmData[i * 2] = bytes[0];
            pcmData[i * 2 + 1] = bytes[1];
        }

        // Assemble the WAV file with a standard 44-byte header
        byte[] wavBytes = new byte[HEADER_SIZE + pcmData.Length];
        
        // 1. "RIFF" chunk descriptor
        wavBytes[0] = (byte)'R'; wavBytes[1] = (byte)'I'; wavBytes[2] = (byte)'F'; wavBytes[3] = (byte)'F';
        
        // 2. Chunk size (file size - 8 bytes)
        byte[] fileLength = BitConverter.GetBytes(wavBytes.Length - 8);
        Array.Copy(fileLength, 0, wavBytes, 4, 4);

        // 3. "WAVE" format
        wavBytes[8] = (byte)'W'; wavBytes[9] = (byte)'A'; wavBytes[10] = (byte)'V'; wavBytes[11] = (byte)'E';

        // 4. "fmt " sub-chunk
        wavBytes[12] = (byte)'f'; wavBytes[13] = (byte)'m'; wavBytes[14] = (byte)'t'; wavBytes[15] = (byte)' ';

        // 5. Sub-chunk size (16 for PCM format)
        byte[] subchunk1Size = BitConverter.GetBytes(16);
        Array.Copy(subchunk1Size, 0, wavBytes, 16, 4);

        // 6. Audio format (1 for uncompressed PCM)
        byte[] audioFormat = BitConverter.GetBytes((short)1);
        Array.Copy(audioFormat, 0, wavBytes, 20, 2);

        // 7. Number of channels
        byte[] numChannels = BitConverter.GetBytes((short)clip.channels);
        Array.Copy(numChannels, 0, wavBytes, 22, 2);

        // 8. Sample rate (frequency)
        byte[] sampleRate = BitConverter.GetBytes(clip.frequency);
        Array.Copy(sampleRate, 0, wavBytes, 24, 4);

        // 9. Byte rate (sampleRate * channels * bytesPerSample)
        byte[] byteRate = BitConverter.GetBytes(clip.frequency * clip.channels * 2);
        Array.Copy(byteRate, 0, wavBytes, 28, 4);

        // 10. Block align (channels * bytesPerSample)
        byte[] blockAlign = BitConverter.GetBytes((short)(clip.channels * 2));
        Array.Copy(blockAlign, 0, wavBytes, 32, 2);

        // 11. Bits per sample (16 bits)
        byte[] bitsPerSample = BitConverter.GetBytes((short)16);
        Array.Copy(bitsPerSample, 0, wavBytes, 34, 2);

        // 12. "data" sub-chunk header
        wavBytes[36] = (byte)'d'; wavBytes[37] = (byte)'a'; wavBytes[38] = (byte)'t'; wavBytes[39] = (byte)'a';

        // 13. Sub-chunk size (size of the PCM data)
        byte[] subchunk2Size = BitConverter.GetBytes(pcmData.Length);
        Array.Copy(subchunk2Size, 0, wavBytes, 40, 4);

        // 14. Copy the raw PCM samples into the WAV file payload
        Array.Copy(pcmData, 0, wavBytes, HEADER_SIZE, pcmData.Length);

        return wavBytes;
    }
}
