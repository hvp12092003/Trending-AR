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
