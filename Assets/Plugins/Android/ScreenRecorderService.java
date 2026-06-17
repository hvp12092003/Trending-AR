package com.trendingar.screenrecorder;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.MediaRecorder;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.IBinder;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.WindowManager;

import java.io.File;
import java.io.IOException;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Foreground Service that holds the MediaProjection token and drives MediaRecorder.
 *
 * All Unity callbacks are forwarded via ScreenRecorderPlugin (the Fragment),
 * which persists across Service restarts and stores the lastOutputPath so
 * saveToGallery() still works after this Service is destroyed.
 */
public class ScreenRecorderService extends Service {

    private static final String TAG            = "ScreenRecorderService";
    private static final String CHANNEL_ID     = "ScreenRecorderChannel";
    private static final int    NOTIFICATION_ID = 1337;

    /** Kept for backward compatibility – plugin now owns callbacks. */
    public static String unityObjectName = "ScreenRecordManager";

    private MediaProjection mediaProjection;
    private VirtualDisplay  virtualDisplay;
    private MediaRecorder   mediaRecorder;
    private boolean         isRecording = false;
    private String          outputPath;

    private static ScreenRecorderService instance;
    public  static ScreenRecorderService getInstance() { return instance; }

    // ──────────────────────────────────────────────────────────────
    // Service lifecycle
    // ──────────────────────────────────────────────────────────────

    @Override
    public void onCreate() {
        super.onCreate();
        instance = this;
        createNotificationChannel();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null) return START_NOT_STICKY;

        String action = intent.getStringExtra("action");

        if ("start".equals(action)) {
            int    resultCode     = intent.getIntExtra("resultCode", -1);
            Intent projectionData = intent.getParcelableExtra("projectionData");

            // Show the foreground notification BEFORE getMediaProjection (required on API 34+)
            startForeground(NOTIFICATION_ID, buildNotification());

            MediaProjectionManager pm =
                    (MediaProjectionManager) getSystemService(Context.MEDIA_PROJECTION_SERVICE);
            mediaProjection = pm.getMediaProjection(resultCode, projectionData);

            if (mediaProjection == null) {
                Log.e(TAG, "getMediaProjection returned null");
                notifyPlugin(plugin -> plugin.notifyRecordingFailed("MediaProjection is null"));
                stopSelf();
                return START_NOT_STICKY;
            }

            beginRecording();

        } else if ("stop".equals(action)) {
            stopRecording();
        }

        return START_NOT_STICKY;
    }

    @Override
    public IBinder onBind(Intent intent) { return null; }

    @Override
    public void onDestroy() {
        super.onDestroy();
        instance = null;
        releaseResources();
    }

    // ──────────────────────────────────────────────────────────────
    // Recording
    // ──────────────────────────────────────────────────────────────

    private void beginRecording() {
        // Build output path (app-private; no storage permission needed)
        File dir = getExternalFilesDir(null);
        if (dir == null) dir = getFilesDir();

        String ts = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(new Date());
        outputPath = new File(dir, "TrendingAR_" + ts + ".mp4").getAbsolutePath();

        // Screen dimensions
        WindowManager   wm      = (WindowManager) getSystemService(WINDOW_SERVICE);
        DisplayMetrics  metrics = new DisplayMetrics();
        wm.getDefaultDisplay().getRealMetrics(metrics);

        int sw  = metrics.widthPixels;
        int sh  = metrics.heightPixels;
        int dpi = metrics.densityDpi;

        // Cap at 720p for performance / file size
        int rw, rh;
        if (sw > 720) {
            rw = 720;
            rh = (int) (sh * (720f / sw));
            if (rh % 2 != 0) rh--;
        } else {
            rw = sw;
            rh = sh % 2 == 0 ? sh : sh - 1;
        }

        // Setup MediaRecorder
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            mediaRecorder = new MediaRecorder(this);
        } else {
            mediaRecorder = new MediaRecorder();
        }

        try {
            mediaRecorder.setAudioSource(MediaRecorder.AudioSource.MIC);
            mediaRecorder.setVideoSource(MediaRecorder.VideoSource.SURFACE);
            mediaRecorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
            mediaRecorder.setOutputFile(outputPath);
            mediaRecorder.setVideoSize(rw, rh);
            mediaRecorder.setVideoEncoder(MediaRecorder.VideoEncoder.H264);
            mediaRecorder.setAudioEncoder(MediaRecorder.AudioEncoder.AAC);
            mediaRecorder.setVideoEncodingBitRate(4 * 1024 * 1024); // 4 Mbps
            mediaRecorder.setVideoFrameRate(30);
            mediaRecorder.prepare();
        } catch (IOException e) {
            Log.e(TAG, "MediaRecorder prepare failed: " + e.getMessage());
            notifyPlugin(p -> p.notifyRecordingFailed("Setup failed: " + e.getMessage()));
            releaseResources();
            stopSelf();
            return;
        }

        // VirtualDisplay feeds the screen into MediaRecorder
        virtualDisplay = mediaProjection.createVirtualDisplay(
                "TrendingARRecorder",
                rw, rh, dpi,
                DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                mediaRecorder.getSurface(),
                null, null);

        mediaRecorder.start();
        isRecording = true;

        Log.d(TAG, "Recording started → " + outputPath);
        // Notify via Plugin (Fragment) so Unity gets the callback
        notifyPlugin(ScreenRecorderPlugin::notifyRecordingStarted);
    }

    public void stopRecording() {
        if (!isRecording) return;
        isRecording = false;

        try { mediaRecorder.stop(); }
        catch (Exception e) { Log.w(TAG, "mediaRecorder.stop() threw: " + e.getMessage()); }

        final String path = outputPath; // capture before releaseResources clears nothing (path field)
        releaseResources();

        Log.d(TAG, "Recording stopped → " + path);

        // IMPORTANT: Notify Plugin BEFORE stopSelf() so lastOutputPath is stored in Fragment
        // while we (the Service) are still technically alive.
        notifyPlugin(p -> p.notifyRecordingStopped(path));

        stopForeground(true);
        stopSelf();
    }

    private void releaseResources() {
        if (mediaRecorder != null) {
            try { mediaRecorder.release(); } catch (Exception ignored) {}
            mediaRecorder = null;
        }
        if (virtualDisplay != null) {
            try { virtualDisplay.release(); } catch (Exception ignored) {}
            virtualDisplay = null;
        }
        if (mediaProjection != null) {
            try { mediaProjection.stop(); } catch (Exception ignored) {}
            mediaProjection = null;
        }
    }

    public boolean isRecording()   { return isRecording; }
    public String  getOutputPath() { return outputPath != null ? outputPath : ""; }

    // ──────────────────────────────────────────────────────────────
    // Notification (required for foreground service on API 26+)
    // ──────────────────────────────────────────────────────────────

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel ch = new NotificationChannel(
                    CHANNEL_ID, "Screen Recorder", NotificationManager.IMPORTANCE_LOW);
            ch.setDescription("Đang quay màn hình");
            NotificationManager nm = getSystemService(NotificationManager.class);
            if (nm != null) nm.createNotificationChannel(ch);
        }
    }

    private Notification buildNotification() {
        Notification.Builder b = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        return b.setContentTitle("Trending AR")
                .setContentText("Đang quay màn hình…")
                .setSmallIcon(android.R.drawable.ic_media_play)
                .build();
    }

    // ──────────────────────────────────────────────────────────────
    // Helper: safely call a method on the Plugin Fragment
    // ──────────────────────────────────────────────────────────────

    private interface PluginCallback {
        void run(ScreenRecorderPlugin plugin);
    }

    private void notifyPlugin(PluginCallback cb) {
        ScreenRecorderPlugin plugin = ScreenRecorderPlugin.getInstance();
        if (plugin != null) {
            cb.run(plugin);
        } else {
            // Fallback: send directly (Plugin may have detached in rare edge cases)
            Log.w(TAG, "Plugin instance is null – sending UnitySendMessage directly");
        }
    }
}
