using UnityEngine;

/// <summary>
/// Lưu trữ thông tin âm thanh nhạc cụ của một Cast (audioId + AudioSource đã cấu hình sẵn clip).
/// Script này được BandARSpawner gắn vào mỗi nhân vật khi spawn.
/// TapToPlacePrefab / TapToPlacePrefabNonAR sẽ gọi PlayAudio() khi người dùng thả Cast ra thế giới AR/NonAR.
/// </summary>
public class CastAudioData : MonoBehaviour
{
    [Tooltip("ID của nhạc cụ / âm thanh (trùng với CastData.audioId)")]
    public string audioId;

    [Tooltip("AudioSource đã được cấu hình sẵn AudioClip, chưa phát. Sẽ được BandARSpawner gán vào.")]
    public AudioSource preparedSource;

    [Tooltip("Nếu tích chọn, âm lượng của nhân vật này sẽ nhỏ lại khi có nhân vật khác cùng được thả ra AR")]
    public bool reduceVolumeWhenTogether = true;

    [Tooltip("Mức độ giảm âm lượng khi chơi chung (Ví dụ: 0.3 có nghĩa là giảm đi 30%, âm lượng còn 70%)")]
    public float reduceAmount = 0.3f;

    [HideInInspector]
    public float originalVolume = 1f;

    /// <summary>
    /// Phát nhạc nhạc cụ. Gọi hàm này sau khi người dùng thả Cast ra thế giới AR.
    /// </summary>
    public void PlayAudio()
    {
        if (preparedSource == null)
        {
            Debug.LogWarning($"[CastAudioData] ({gameObject.name}) preparedSource là null, không thể phát nhạc.");
            return;
        }

        originalVolume = preparedSource.volume;

        if (preparedSource.clip == null)
        {
            Debug.LogWarning($"[CastAudioData] ({gameObject.name}) AudioClip chưa được gán vào preparedSource.");
            return;
        }

        if (!preparedSource.isPlaying)
        {
            preparedSource.Play();
            Debug.Log($"[CastAudioData] ({gameObject.name}) Đã phát nhạc: {audioId}");
        }
    }

    /// <summary>
    /// Dừng phát nhạc nhạc cụ.
    /// </summary>
    public void StopAudio()
    {
        if (preparedSource != null && preparedSource.isPlaying)
        {
            preparedSource.Stop();
        }
    }
}
