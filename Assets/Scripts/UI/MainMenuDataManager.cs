using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Quản lý việc đọc/ghi dữ liệu thực tế lên Firebase Firestore cho màn hình Menu chính.
/// </summary>
public class MainMenuDataManager : MonoBehaviour
{
    public static MainMenuDataManager Instance { get; private set; }

    private FirebaseFirestore _db;

    /// <summary>
    /// Lưu trữ CastData của nhân vật tự thiết kế (Custom Character) được chọn để chuyển tiếp sang scene AR.
    /// </summary>
    public CastData castData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirestore();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFirestore()
    {
        _db = FirebaseFirestore.DefaultInstance;
        Debug.Log("[MainMenuDataManager] Firebase Firestore đã kết nối thành công.");
    }

    /// <summary>
    /// Đảm bảo document của user hiện tại luôn tồn tại trong Firestore.
    /// Nếu chưa có (sau khi đăng ký mới), hệ thống sẽ tạo mới với điểm số bằng 0.
    /// </summary>
    public async Task EnsureUserDocumentExistsAsync()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null)
        {
            Debug.LogWarning("[MainMenuDataManager] Không tìm thấy người dùng đăng nhập.");
            return;
        }

        await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference userDocRef = _db.Collection("users").Document(firebaseUser.UserId);
                DocumentSnapshot snapshot = await userDocRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    string email = firebaseUser.Email ?? "";
                    string displayName = email.Split('@')[0]; // Lấy phần trước dấu @ làm nickname tạm thời

                    var userData = new Dictionary<string, object>
                    {
                        { "userId", firebaseUser.UserId },
                        { "displayName", displayName },
                        { "email", email },
                        { "points", 0 }
                    };

                    await userDocRef.SetAsync(userData);
                    Debug.Log($"[MainMenuDataManager] Đã tạo document người dùng mới cho: {email}");
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi kiểm tra document người dùng", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
        });
    }
    /// <summary>
    /// Tải danh sách nhân vật đã tạo của người dùng hiện tại từ users/{myUID}/characters.
    /// </summary>
    public async Task<List<CharacterData>> GetCreatedCharactersAsync()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return new List<CharacterData>();

        return await NetworkGuard.RunAsync(async () =>
        {
            var list = new List<CharacterData>();
            try
            {
                CollectionReference charColRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("characters");
                QuerySnapshot snapshot = await charColRef.GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    CharacterData data = doc.ConvertTo<CharacterData>();
                    data.characterId = doc.Id;
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải danh sách nhân vật", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return list;
        });
    }

    /// <summary>
    /// Tải danh sách mẫu nhạc (Band Template) toàn cục mà người dùng đã mua.
    /// (Lọc các templates có chứa UID của mình trong danh sách buyerIds).
    /// </summary>
    public async Task<List<BandTemplateData>> GetPurchasedTemplatesAsync()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return new List<BandTemplateData>();

        return await NetworkGuard.RunAsync(async () =>
        {
            var list = new List<BandTemplateData>();
            try
            {
                Query query = _db.Collection("templates").WhereArrayContains("buyerIds", firebaseUser.UserId);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    BandTemplateData data = doc.ConvertTo<BandTemplateData>();
                    data.templateId = doc.Id;
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải danh sách templates đã mua", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return list;
        });
    }

    /// <summary>
    /// Tải danh sách mẫu nhạc do chính người dùng hiện tại tạo ra.
    /// (Lọc các templates có creatorId trùng với UID của mình).
    /// </summary>
    public async Task<List<BandTemplateData>> GetMyCreatedTemplatesAsync()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return new List<BandTemplateData>();

        return await NetworkGuard.RunAsync(async () =>
        {
            var list = new List<BandTemplateData>();
            try
            {
                Query query = _db.Collection("templates").WhereEqualTo("creatorId", firebaseUser.UserId);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    BandTemplateData data = doc.ConvertTo<BandTemplateData>();
                    data.templateId = doc.Id;
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải danh sách templates tự tạo", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return list;
        });
    }

    /// <summary>
    /// Tải danh sách bảng xếp hạng (Top 50 người dùng có điểm points cao nhất).
    /// </summary>
    public async Task<List<UserData>> GetLeaderboardAsync()
    {
        return await NetworkGuard.RunAsync(async () =>
        {
            var list = new List<UserData>();
            try
            {
                Query query = _db.Collection("users").OrderByDescending("points").Limit(50);
                QuerySnapshot snapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    UserData data = doc.ConvertTo<UserData>();
                    data.userId = doc.Id;
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải bảng xếp hạng", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return list;
        });
    }

    /// <summary>
    /// Thực hiện giao dịch mua Band Template.
    /// Thêm người dùng vào danh sách buyerIds của template và cộng 1 điểm (points) cho người tạo (creator).
    /// </summary>
    public async Task<(bool success, string error)> BuyTemplateAsync(string templateId)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return (false, "Người dùng chưa đăng nhập!");

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference templateDocRef = _db.Collection("templates").Document(templateId);

                bool transactionSuccess = await _db.RunTransactionAsync(async transaction =>
                {
                    DocumentSnapshot templateSnapshot = await transaction.GetSnapshotAsync(templateDocRef);
                    if (!templateSnapshot.Exists) return false;

                    BandTemplateData template = templateSnapshot.ConvertTo<BandTemplateData>();

                    // Kiểm tra xem đã mua chưa
                    if (template.buyerIds != null && template.buyerIds.Contains(firebaseUser.UserId))
                    {
                        return true; // Đã mua từ trước, giao dịch thành công (no-op)
                    }

                    // Cập nhật danh sách người mua
                    if (template.buyerIds == null) template.buyerIds = new List<string>();
                    template.buyerIds.Add(firebaseUser.UserId);

                    transaction.Update(templateDocRef, new Dictionary<string, object>
                    {
                        { "buyerIds", template.buyerIds },
                        { "downloadCount", FieldValue.Increment(1) }
                    });

                    // Cộng điểm cho tác giả (creator) của template đó
                    if (!string.IsNullOrEmpty(template.creatorId))
                    {
                        DocumentReference creatorDocRef = _db.Collection("users").Document(template.creatorId);
                        transaction.Update(creatorDocRef, new Dictionary<string, object>
                        {
                            { "points", FieldValue.Increment(1) }
                        });
                    }

                    return true;
                });

                return (transactionSuccess, transactionSuccess ? "" : "Giao dịch bị từ chối.");
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi trong Transaction mua template", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return (false, ex.Message);
            }
        });
    }

    /// <summary>
    /// Hàm tiện ích: Tạo mới một template nhạc để thử nghiệm hoặc do người dùng tạo.
    /// </summary>
    public async Task<bool> CreateTemplateAsync(string templateName, int price)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference templateDocRef = _db.Collection("templates").Document(); // Tự sinh ID ngẫu nhiên
                string email = firebaseUser.Email ?? "";
                string displayName = email.Split('@')[0];

                var templateData = new Dictionary<string, object>
                {
                    { "templateId", templateDocRef.Id },
                    { "name", templateName },
                    { "creatorId", firebaseUser.UserId },
                    { "creatorName", displayName },
                    { "price", price },
                    { "buyerIds", new List<string>() },
                    { "downloadCount", 0 }
                };

                await templateDocRef.SetAsync(templateData);
                Debug.Log($"[MainMenuDataManager] Đã tạo template nhạc thành công: {templateName}");
                return true;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tạo template", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }

    /// <summary>
    /// Hàm tiện ích: Tạo mới một nhân vật do người dùng thiết kế.
    /// </summary>
    public async Task<bool> CreateCharacterAsync(string name, string prefabName, string instrumentId = "", string danceAnimId = "", List<string> danceAnimIds = null)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference charDocRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("characters").Document();

                var characterData = new Dictionary<string, object>
                {
                    { "characterId", charDocRef.Id },
                    { "name", name },
                    { "prefabName", prefabName },
                    { "createdAt", FieldValue.ServerTimestamp },
                    { "instrumentId", instrumentId },
                    { "danceAnimId", danceAnimId },
                    { "danceAnimIds", danceAnimIds ?? new List<string>() }
                };

                await charDocRef.SetAsync(characterData);
                Debug.Log($"[MainMenuDataManager] Đã tạo nhân vật thành công: {name}");
                return true;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tạo nhân vật", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }

    /// <summary>
    /// Cập nhật chuỗi Base64 của ảnh đại diện lên Firestore.
    /// </summary>
    public async Task<bool> UpdateAvatarAsync(string base64Data)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference userDocRef = _db.Collection("users").Document(firebaseUser.UserId);
                var updateData = new Dictionary<string, object> { { "avatarBase64", base64Data } };
                await userDocRef.SetAsync(updateData, SetOptions.MergeAll);
                Debug.Log("[MainMenuDataManager] Cập nhật avatar lên Firestore thành công.");
                return true;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi cập nhật avatar", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }

    /// <summary>
    /// Tải chuỗi ảnh đại diện Base64 của người dùng hiện tại từ Firestore.
    /// </summary>
    public async Task<string> GetUserAvatarBase64Async()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return null;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference userDocRef = _db.Collection("users").Document(firebaseUser.UserId);
                DocumentSnapshot snapshot = await userDocRef.GetSnapshotAsync();
                if (snapshot.Exists && snapshot.TryGetValue("avatarBase64", out string base64))
                {
                    return base64;
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải avatar từ Firestore", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return null;
        });
    }

    private void LogFirestoreError(string context, Exception ex)
    {
        bool isOffline = ex.Message.Contains("offline") || 
                         ex.Message.Contains("client is offline") || 
                         (ex.InnerException != null && (ex.InnerException.Message.Contains("offline") || ex.InnerException.Message.Contains("client is offline")));
        
        if (isOffline)
        {
            Debug.LogWarning($"[MainMenuDataManager] {context} (Ngoại tuyến): {ex.Message}");
        }
        else
        {
            Debug.LogError($"[MainMenuDataManager] {context}: {ex.Message}");
        }
    }

    /// <summary>
    /// Lưu bản ghi âm giọng nói lên Firestore.
    /// </summary>
    public async Task<bool> SaveRecordingAsync(string recordingId, string name, string audioBase64)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference recDocRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("recordings").Document(recordingId);

                var recData = new Dictionary<string, object>
                {
                    { "recordingId", recordingId },
                    { "name", name },
                    { "audioBase64", audioBase64 },
                    { "createdAt", FieldValue.ServerTimestamp }
                };

                await recDocRef.SetAsync(recData);
                Debug.Log($"[MainMenuDataManager] Đã lưu bản ghi âm lên Firestore: {name} ({recordingId})");
                return true;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi lưu bản ghi âm", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }

    /// <summary>
    /// Tải danh sách bản ghi âm của người dùng từ Firestore.
    /// </summary>
    public async Task<List<RecordingData>> GetRecordingsAsync()
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return new List<RecordingData>();

        return await NetworkGuard.RunAsync(async () =>
        {
            var list = new List<RecordingData>();
            try
            {
                CollectionReference recColRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("recordings");
                QuerySnapshot snapshot = await recColRef.OrderByDescending("createdAt").GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    RecordingData data = doc.ConvertTo<RecordingData>();
                    data.recordingId = doc.Id;
                    list.Add(data);
                }
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi tải danh sách bản ghi âm", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
            }
            return list;
        });
    }

    /// <summary>
    /// Xóa một bản ghi âm khỏi Firestore.
    /// </summary>
    public async Task<bool> DeleteRecordingAsync(string recordingId)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference recDocRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("recordings").Document(recordingId);
                await recDocRef.DeleteAsync();
                Debug.Log($"[MainMenuDataManager] Đã xóa bản ghi âm khỏi Firestore: {recordingId}");
                return true;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi xóa bản ghi âm", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }

    /// <summary>
    /// Xóa một nhân vật khỏi Firestore, đồng thời tự động xóa bản ghi âm đi kèm nếu có.
    /// </summary>
    public async Task<bool> DeleteCharacterAsync(string characterId)
    {
        var firebaseUser = FirebaseAuth.DefaultInstance.CurrentUser;
        if (firebaseUser == null) return false;

        return await NetworkGuard.RunAsync(async () =>
        {
            try
            {
                DocumentReference charDocRef = _db.Collection("users").Document(firebaseUser.UserId).Collection("characters").Document(characterId);
                DocumentSnapshot snapshot = await charDocRef.GetSnapshotAsync();

                if (snapshot.Exists)
                {
                    string instrumentId = "";
                    if (snapshot.TryGetValue("instrumentId", out instrumentId))
                    {
                        // Nếu nhân vật sử dụng âm thanh tự thu, tự động xóa bản ghi âm tương ứng
                        if (!string.IsNullOrEmpty(instrumentId) && instrumentId.StartsWith("rec_"))
                        {
                            await DeleteRecordingAsync(instrumentId);
                        }
                    }
                    
                    await charDocRef.DeleteAsync();
                    Debug.Log($"[MainMenuDataManager] Đã xóa nhân vật khỏi Firestore: {characterId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogFirestoreError("Lỗi xóa nhân vật", ex);
                if (NetworkGuard.IsNetworkException(ex))
                {
                    throw;
                }
                return false;
            }
        });
    }
}

[FirestoreData]
public class RecordingData
{
    public string recordingId { get; set; } // Sẽ được điền thủ công từ doc.Id

    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public string audioBase64 { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────
// Cấu trúc dữ liệu ánh xạ sang Firestore
// ─────────────────────────────────────────────────────────────────────────

[FirestoreData]
public class CharacterData
{
    public string characterId { get; set; } // Sẽ được điền thủ công từ doc.Id

    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public string prefabName { get; set; }
    [FirestoreProperty] public Timestamp createdAt { get; set; }
    [FirestoreProperty] public string instrumentId { get; set; }
    [FirestoreProperty] public string danceAnimId { get; set; }
    [FirestoreProperty] public List<string> danceAnimIds { get; set; } = new List<string>();
}

[FirestoreData]
public class BandTemplateData
{
    public string templateId { get; set; } // Sẽ được điền thủ công từ doc.Id

    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public string creatorId { get; set; }
    [FirestoreProperty] public string creatorName { get; set; }
    [FirestoreProperty] public int price { get; set; }
    [FirestoreProperty] public List<string> buyerIds { get; set; } = new List<string>();
    [FirestoreProperty] public int downloadCount { get; set; }
}

[FirestoreData]
public class UserData
{
    public string userId { get; set; } // Sẽ được điền thủ công từ doc.Id

    [FirestoreProperty] public string displayName { get; set; }
    [FirestoreProperty] public string email { get; set; }
    [FirestoreProperty] public int points { get; set; }
    [FirestoreProperty] public string avatarBase64 { get; set; }
}
