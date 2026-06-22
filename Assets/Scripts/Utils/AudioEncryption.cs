using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Tiện ích mã hóa và giải mã dữ liệu âm thanh (WAV bytes) bằng AES-128.
/// </summary>
public static class AudioEncryption
{
    // Khóa và IV tĩnh 16-bytes cho AES-128
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("TrendingARKey123");
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("TrendingARIV4567");

    /// <summary>
    /// Mã hóa mảng byte dữ liệu âm thanh bằng thuật toán AES.
    /// </summary>
    public static byte[] Encrypt(byte[] rawData)
    {
        if (rawData == null || rawData.Length == 0) return rawData;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(rawData, 0, rawData.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Giải mã mảng byte dữ liệu âm thanh bằng thuật toán AES.
    /// </summary>
    public static byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length == 0) return encryptedData;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(encryptedData, 0, encryptedData.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
        }
    }
}
