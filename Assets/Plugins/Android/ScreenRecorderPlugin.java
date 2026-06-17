package com.trendingar.screenrecorder;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

/**
 * Fragment-based Android screen recording plugin for Unity.
 *
 * Key design: Fragment tồn tại suốt vòng đời Activity (không bị destroy sau khi
 * Service dừng), nên nó lưu lastOutputPath và tự xử lý gallery saving.
 * Service chỉ chịu trách nhiệm ghi hình; mọi callback về Unity đều đi qua đây.
 *
 * Unity callbacks (UnitySendMessage → unityObjectName):
 *   OnAndroidRecordingStarted(string "")
 *   OnAndroidRecordingStopped(string filePath)
 *   OnAndroidRecordingFailed(string errorMessage)
 *   OnAndroidSaveSuccess(string "")
 *   OnAndroidSaveFailed(string errorMessage)
 */
public class ScreenRecorderPlugin extends Fragment {

    private static final String TAG      = "ScreenRecorderPlugin";
    private static final String FRAG_TAG = "ScreenRecorderPluginFragment";
    private static final int    REQ_CODE = 2001;

    /** Must match the Unity GameObject that has ScreenRecordManager.cs */
    public static String unityObjectName = "ScreenRecordManager";

    private static ScreenRecorderPlugin instance;

    /** Path of the most recently completed recording (persists after Service dies). */
    private String lastOutputPath = "";


    // ──────────────────────────────────────────────────────────────
    // Static init (called from C#)
    // ──────────────────────────────────────────────────────────────

    /** Attach this Fragment to the UnityPlayerActivity (idempotent, safe to call multiple times). */
    public static void init() {
        final Activity activity = UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            FragmentManager fm = activity.getFragmentManager();
            ScreenRecorderPlugin frag = (ScreenRecorderPlugin) fm.findFragmentByTag(FRAG_TAG);
            if (frag == null) {
                frag = new ScreenRecorderPlugin();
                fm.beginTransaction().add(frag, FRAG_TAG).commitAllowingStateLoss();
                fm.executePendingTransactions();
            }
            instance = frag;
            Log.d(TAG, "ScreenRecorderPlugin initialised (unityObj=" + unityObjectName + ")");
        });
    }

    public static ScreenRecorderPlugin getInstance() { return instance; }

    // ──────────────────────────────────────────────────────────────
    // Fragment lifecycle
    // ──────────────────────────────────────────────────────────────

    @Override
    public void onAttach(Context context) {
        super.onAttach(context);
        instance = this;
    }

    @Override
    public void onAttach(Activity activity) { // pre-API 23
        super.onAttach(activity);
        instance = this;
    }

    @Override
    public void onDetach() {
        super.onDetach();
        if (instance == this) instance = null;
    }

    // ──────────────────────────────────────────────────────────────
    // Public API – called from C# via JNI
    // ──────────────────────────────────────────────────────────────

    /**
     * Opens the system "Allow screen capture?" dialog.
     * Result arrives in onActivityResult → Service is started there.
     */
    public void startRecording() {
        Activity activity = getActivity();
        if (activity == null) {
            sendFailed("Activity is null");
            return;
        }
        if (isServiceRecording()) {
            sendFailed("Already recording");
            return;
        }

        android.media.projection.MediaProjectionManager pm =
                (android.media.projection.MediaProjectionManager)
                        activity.getSystemService(Context.MEDIA_PROJECTION_SERVICE);

        startActivityForResult(pm.createScreenCaptureIntent(), REQ_CODE);
    }


    /** Stop the running recording. */
    public void stopRecording() {
        ScreenRecorderService svc = ScreenRecorderService.getInstance();
        if (svc != null) {
            svc.stopRecording();
        } else {
            // Fallback: send intent (service might not have started yet)
            Activity a = getActivity();
            if (a != null) {
                Intent i = new Intent(a, ScreenRecorderService.class);
                i.putExtra("action", "stop");
                a.startService(i);
            }
        }
    }

    /**
     * Copy the last recording to the system Gallery.
     * Uses lastOutputPath stored in the Fragment (valid even after Service is gone).
     */
    public void saveToGallery() {
        if (lastOutputPath == null || lastOutputPath.isEmpty()) {
            sendSaveFailed("No recording file path available");
            return;
        }

        java.io.File file = new java.io.File(lastOutputPath);
        if (!file.exists()) {
            sendSaveFailed("File not found: " + lastOutputPath);
            return;
        }

        Activity activity = getActivity();
        if (activity == null) {
            sendSaveFailed("Activity is null");
            return;
        }

        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                saveViaMediaStore(activity, file);
            } else {
                saveViaMediaScanner(activity, file);
            }
        } catch (Exception e) {
            Log.e(TAG, "saveToGallery error: " + e.getMessage());
            sendSaveFailed(e.getMessage());
        }
    }

    public boolean isRecording() { return isServiceRecording(); }

    public String getLastFilePath() { return lastOutputPath; }

    // ──────────────────────────────────────────────────────────────
    // Called by ScreenRecorderService to report events back to Unity
    // ──────────────────────────────────────────────────────────────

    /** Service calls this when recording has actually started. */
    public void notifyRecordingStarted() {
        Log.d(TAG, "notifyRecordingStarted");
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidRecordingStarted", "");
    }

    /**
     * Service calls this BEFORE stopping itself (so Fragment can store the path
     * while Service is still alive — but path is kept after Service dies).
     */
    public void notifyRecordingStopped(String filePath) {
        Log.d(TAG, "notifyRecordingStopped → " + filePath);
        lastOutputPath = (filePath != null) ? filePath : "";
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidRecordingStopped", lastOutputPath);
    }

    /** Service calls this on error. */
    public void notifyRecordingFailed(String error) {
        Log.e(TAG, "notifyRecordingFailed: " + error);
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidRecordingFailed", error);
    }

    // ──────────────────────────────────────────────────────────────
    // onActivityResult – MediaProjection permission dialog result
    // ──────────────────────────────────────────────────────────────

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQ_CODE) return;

        if (resultCode == Activity.RESULT_OK && data != null) {
            Activity activity = getActivity();
            if (activity == null) {
                sendFailed("Activity gone after permission grant");
                return;
            }

            Intent svcIntent = new Intent(activity, ScreenRecorderService.class);
            svcIntent.putExtra("action", "start");
            svcIntent.putExtra("resultCode", resultCode);
            svcIntent.putExtra("projectionData", data);

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                activity.startForegroundService(svcIntent);
            } else {
                activity.startService(svcIntent);
            }
        } else {
            Log.w(TAG, "User denied screen capture (resultCode=" + resultCode + ")");
            sendFailed("User denied screen capture");
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Gallery saving (runs in Fragment – Context always available)
    // ──────────────────────────────────────────────────────────────

    /** Android 10+ (API 29+): no WRITE_EXTERNAL_STORAGE permission needed. */
    private void saveViaMediaStore(Context ctx, java.io.File file) throws Exception {
        android.content.ContentValues values = new android.content.ContentValues();
        values.put(android.provider.MediaStore.Video.Media.DISPLAY_NAME, file.getName());
        values.put(android.provider.MediaStore.Video.Media.MIME_TYPE, "video/mp4");
        values.put(android.provider.MediaStore.Video.Media.RELATIVE_PATH,
                android.os.Environment.DIRECTORY_MOVIES + "/TrendingAR");
        values.put(android.provider.MediaStore.Video.Media.IS_PENDING, 1);

        android.net.Uri uri = ctx.getContentResolver().insert(
                android.provider.MediaStore.Video.Media.EXTERNAL_CONTENT_URI, values);

        if (uri == null) {
            sendSaveFailed("MediaStore insert returned null");
            return;
        }

        try (java.io.InputStream is  = new java.io.FileInputStream(file);
             java.io.OutputStream os = ctx.getContentResolver().openOutputStream(uri)) {
            if (os == null) throw new Exception("Cannot open OutputStream");
            byte[] buf = new byte[8192];
            int n;
            while ((n = is.read(buf)) != -1) os.write(buf, 0, n);
        }

        values.clear();
        values.put(android.provider.MediaStore.Video.Media.IS_PENDING, 0);
        ctx.getContentResolver().update(uri, values, null, null);

        Log.d(TAG, "Saved via MediaStore: " + uri);
        // Gửi URI về Unity để dùng cho tính năng chia sẻ native
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidSaveSuccess", uri.toString());
    }

    /** Android 9 and below: use MediaScanner to index the file. */
    private void saveViaMediaScanner(Context ctx, java.io.File file) {
        android.media.MediaScannerConnection.scanFile(
                ctx,
                new String[]{ file.getAbsolutePath() },
                new String[]{ "video/mp4" },
                (path, uri) -> {
                    if (uri != null) {
                        Log.d(TAG, "MediaScanner indexed: " + path);
                        // Gửi URI về Unity để dùng cho tính năng chia sẻ native
                        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidSaveSuccess", uri.toString());
                    } else {
                        sendSaveFailed("MediaScanner returned null URI for: " + path);
                    }
                });
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private boolean isServiceRecording() {
        ScreenRecorderService svc = ScreenRecorderService.getInstance();
        return svc != null && svc.isRecording();
    }

    private void sendFailed(String msg) {
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidRecordingFailed", msg);
    }

    private void sendSaveFailed(String msg) {
        Log.e(TAG, "SaveFailed: " + msg);
        UnityPlayer.UnitySendMessage(unityObjectName, "OnAndroidSaveFailed", msg);
    }
}
