using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Opt-in Player mode: pass -dysonWallpaper. The benchmark stays independent.
public sealed class DysonWallpaperController : MonoBehaviour
{
    const int Fps = 30, Slots = 4;
    sealed class Frame
    {
        public RenderTexture texture;
        public bool submitted;
        public long tick = -1;
        public int revision;
        public double count;
    }
    static DysonWallpaperController instance;
    readonly Frame[] frames = new Frame[Slots];
    readonly FrameTiming[] timing = new FrameTiming[1];
    readonly UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
    DysonSwarmRenderer swarm;
    Camera source, presentation;
    DysonPerspectiveController controls;
    RawImage image;
    GameObject canvasObject;
    Canvas[] hiddenCanvases;
    bool[] canvasStates;
    Frame displayed;
    Vector3 oldPosition;
    Quaternion oldRotation;
    float oldFov;
    bool oldCameraEnabled, oldControlsEnabled, oldOrtho, oldBackground;
    int oldFps, oldVsync, revision;
    float oldFar, oldAspect;
    double start, simulationStart, predictionAnchor, predictionCount, launchRate;
    bool capturing, noPrecompute, direct;
    StreamWriter telemetry;
    double duration, renderCpuMs;
    long renderedFrames, futureFrames;
    bool screenshotTaken;
    string output;
    double captureTime;
    int width, height;
    public int CachedFrames { get; private set; }
    public static DysonWallpaperController Active => instance;
    public double CurrentDisplayedCount => displayed != null ? displayed.count : predictionCount;
    public long MissedFrames { get; private set; }
    public float CameraOpeningDegrees { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-dysonWallpaper") < 0) return;
        new GameObject("Dyson wallpaper").AddComponent<DysonWallpaperController>();
    }
    // Integration hooks are scoped to this mode and this camera.
    public static bool IsPresentationCamera(Camera camera) => instance != null && camera == instance.presentation;
    public static bool TryGetSimulationTime(Camera camera, out double seconds)
    {
        seconds = instance != null ? instance.captureTime : 0;
        return instance != null && instance.capturing && camera == instance.source;
    }
    // Feed a new forecast at the current instant. Constant count is rate=0.
    // A revised forecast invalidates all future images without resetting orbital time.
    public void FeedPopulation(double count, double panelsPerSecond = 0)
    {
        if ((double.IsNaN(count) || double.IsInfinity(count)) || (double.IsNaN(panelsPerSecond) || double.IsInfinity(panelsPerSecond)) || count < 0 || panelsPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        predictionCount = count;
        launchRate = panelsPerSecond;
        predictionAnchor = Time.realtimeSinceStartupAsDouble;
        revision++;
    }
    double Predict(double at) => Math.Max(0, predictionCount + launchRate * Math.Max(0, at - predictionAnchor));
    // Requests and presentation are submitted to the same graphics queue. GPU command
    // order preserves write-before-sample and previous-sample-before-reuse, without CPU readback.
    bool Ready(Frame f) => true;

    static string Arg(string name, string fallback) {
        var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,name);
        return i >= 0 && i+1 < args.Length ? args[i+1] : fallback;
    }
    void Start()
    {
        swarm = FindAnyObjectByType<DysonSwarmRenderer>();
        source = Camera.main;
        if (swarm == null || source == null)
        { Debug.LogError("Wallpaper requires a swarm and main camera."); enabled = false; return; }
        if (instance != null) { enabled = false; return; }
        instance = this;
        controls = source.GetComponent<DysonPerspectiveController>();
        if (controls != null) controls.SetFocusView(false);
        oldPosition = source.transform.position; oldRotation = source.transform.rotation;
        oldFov = source.fieldOfView; oldOrtho = source.orthographic; oldFar = source.farClipPlane; oldAspect = source.aspect;
        oldCameraEnabled = source.enabled;
        oldControlsEnabled = controls != null && controls.enabled;
        oldFps = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
        oldBackground = Application.runInBackground;
        if (controls != null) controls.enabled = false;
        noPrecompute = Array.IndexOf(Environment.GetCommandLineArgs(), "-dysonWallpaperNoPrecompute") >= 0;
        direct = Array.IndexOf(Environment.GetCommandLineArgs(), "-dysonWallpaperDirect") >= 0;
        duration = double.Parse(Arg("-dysonWallpaperSeconds","0"), CultureInfo.InvariantCulture);
        double count = double.Parse(Arg("-dysonWallpaperCount",swarm.MirrorCount.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
        swarm.SetMirrorCount(count);
        ChooseCamera();
        source.enabled = false;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Fps;
        Application.runInBackground = true;
        start = Time.realtimeSinceStartupAsDouble;
        simulationStart = Time.timeAsDouble;
        FeedPopulation(swarm.MirrorCount);
        presentation = new GameObject("Wallpaper presentation camera").AddComponent<Camera>();
        presentation.cullingMask = 0; presentation.clearFlags = CameraClearFlags.SolidColor;
        presentation.backgroundColor = Color.black; presentation.depth = 100;
        hiddenCanvases = FindObjectsByType<Canvas>();
        canvasStates = new bool[hiddenCanvases.Length];
        for (int i = 0; i < hiddenCanvases.Length; i++)
        { canvasStates[i] = hiddenCanvases[i].enabled; hiddenCanvases[i].enabled = false; }
        canvasObject = new GameObject("Wallpaper image", typeof(Canvas));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var child = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
        child.transform.SetParent(canvasObject.transform, false);
        image = child.GetComponent<RawImage>(); image.raycastTarget = false;
        var rect = image.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        if (!direct) Allocate();
        output = Arg("-dysonWallpaperOutput", Application.persistentDataPath);
        Directory.CreateDirectory(output);
        if (duration > 0) {
        telemetry = new StreamWriter(Path.Combine(output,"WallpaperFrames.csv"));
        telemetry.WriteLine("elapsed,frame_ms,gpu_ms,cpu_frame_ms,render_submit_cpu_ms,displayed_tick,cached_frames,rendered_frames,future_frames,missed_frames,count,opening_degrees,swarm_bytes");
        telemetry.AutoFlush = true;
        }
        Debug.Log($"WALLPAPER_READY opening={CameraOpeningDegrees} count={swarm.MirrorCount} direct={direct} precompute={!noPrecompute}");
        if (direct) {
            source.enabled = true; presentation.enabled = false; canvasObject.SetActive(false);
        }
        if (!direct) {
        request.destination = frames[0].texture;
        if (!RenderPipeline.SupportsRenderRequest(source, request))
        { Debug.LogError("Active pipeline does not support wallpaper render requests."); enabled = false; }
        }
    }
    void ChooseCamera()
    {
        // Search a fixed direction away from every band, including the solar disc.
        // A negative margin means the current population leaves no unobstructed opening.
        Vector3 best = Vector3.up; float bestMargin = float.NegativeInfinity, outer = 1;
        for (int k = 0; k < 4096; k++)
        {
            float y = 1 - 2 * (k + 0.5f) / 4096;
            float angle = k * 2.39996323f, r = Mathf.Sqrt(1 - y * y);
            Vector3 direction = new Vector3(r * Mathf.Cos(angle), y, r * Mathf.Sin(angle));
            float margin = 90;
            for (int i = 0; i < swarm.ActiveRingCount; i++)
            {
                var ring = swarm.GetRingSettings(i);
                swarm.GetActiveRingLayout(i, out _, out _, out float radial, out float vertical);
                swarm.GetSurfaceState(i, out float core, out _, out _);
                outer = Mathf.Max(outer, ring.radius + 4 * radial + 4 * vertical);
                float belt = core; // Prefer an opening in the opaque cores; sparse tails may cross the disc.
                float sun = Mathf.Asin(Mathf.Clamp01(swarm.WallpaperSolarRadius / ring.radius)) * Mathf.Rad2Deg;
                float latitude = Mathf.Asin(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(direction, ring.ringNormal.normalized)))) * Mathf.Rad2Deg;
                margin = Mathf.Min(margin, latitude - belt - sun);
            }
            if (margin > bestMargin) { bestMargin = margin; best = direction; }
        }
        CameraOpeningDegrees = bestMargin;
        source.orthographic = false; source.fieldOfView = 45;
        float halfFov = source.fieldOfView * Mathf.Deg2Rad / 2;
        float distance = outer * 1.15f / Mathf.Sin(Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Min(1, source.aspect)));
        source.transform.position = swarm.SunPosition + best * distance;
        source.transform.rotation = Quaternion.LookRotation(-best, Vector3.up);
        source.farClipPlane = Mathf.Max(source.farClipPlane, distance + outer * 2);
        if (bestMargin <= 0) Debug.LogWarning("Wallpaper: no fully clear solar opening for the current ring layout; using maximum clearance.");
    }
    void Allocate()
    {
        width = Math.Max(1, Screen.width); height = Math.Max(1, Screen.height);
        source.aspect = width / (float)height;
        for (int i = 0; i < Slots; i++)
        {
            frames[i] = new Frame { texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) };
            frames[i].texture.name = "Wallpaper cached frame " + i;
            frames[i].texture.Create();
        }
    }
    void Render(Frame f, long tick)
    {
        var renderClock = System.Diagnostics.Stopwatch.StartNew();
        double at = start + tick / (double)Fps;
        f.count = Predict(at);
        if (swarm.MirrorCount != f.count) swarm.SetMirrorCount(f.count);
        captureTime = simulationStart + tick / (double)Fps;
        capturing = true;
        try
        {
            request.destination = f.texture;
            // Single-camera requests do not update the volume framework themselves.
            // The camera is fixed, but this preserves live global volume changes as well.
            VolumeManager.instance.Update(source.transform, source.GetUniversalAdditionalCameraData().volumeLayerMask);
            RenderPipeline.SubmitRenderRequest(source, request);
            f.submitted = true; f.tick = tick; f.revision = revision;
        }
        finally { capturing = false; renderCpuMs = renderClock.Elapsed.TotalMilliseconds; renderedFrames++; }
    }
    Frame Find(long tick)
    {
        foreach (var f in frames) if (f.submitted && f.tick == tick && f.revision == revision) return f;
        return null;
    }
    Frame Free(long current)
    {
        foreach (var f in frames)
            if (f != displayed && Ready(f) && (!f.submitted || f.revision != revision || f.tick < current)) return f;
        return null;
    }
    void LateUpdate()
    {
        if (instance != this || image == null) return;
        FrameTimingManager.CaptureFrameTimings();
        FrameTimingManager.GetLatestTimings(1, timing);
        double elapsed = Time.realtimeSinceStartupAsDouble - start;
        telemetry?.WriteLine(string.Join(",", new[] {
            elapsed, Time.unscaledDeltaTime * 1000.0, timing[0].gpuFrameTime, timing[0].cpuFrameTime,
            renderCpuMs, displayed != null ? displayed.tick : -1, CachedFrames, renderedFrames, futureFrames,
            MissedFrames, direct ? swarm.MirrorCount : CurrentDisplayedCount, CameraOpeningDegrees, swarm.AllocatedSwarmBufferBytes
        }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        if (duration > 0 && !screenshotTaken && elapsed > 8) {
            screenshotTaken = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"Wallpaper.png"));
        }
        if (duration > 0 && elapsed >= duration) {
            File.WriteAllText(Path.Combine(output,"Complete.txt"), "Completed");
            Application.Quit(); return;
        }
        if (direct) return;
        if (Screen.width != width || Screen.height != height)
        {
            foreach (var f in frames) if (!Ready(f)) return;
            image.texture = null; ReleaseTextures(); displayed = null; Allocate(); revision++;
        }
        double now = Time.realtimeSinceStartupAsDouble;
        long tick = (long)Math.Floor((now - start) * Fps);
        // Select completed images before taking a reusable slot: otherwise the newest
        // completed image could be overwritten before ever reaching the screen.
        Frame completed = null;
        foreach (var f in frames)
            if (f.submitted && f.revision == revision && f.tick <= tick && Ready(f)
                && (completed == null || f.tick > completed.tick)) completed = f;
        if (completed != null) { displayed = completed; image.texture = completed.texture; }
        bool renderedCurrent = false;
        Frame current = Find(tick);
        if (current == null)
        {
            current = Free(tick);
            if (current != null) { Render(current, tick); renderedCurrent = true; }
        }
        // Select the submitted image; presentation samples it after its render commands.
        if (current != null && Ready(current)) { displayed = current; image.texture = current.texture; }
        else {
            MissedFrames++;
            // A completed past frame is preferable to a blank screen while the current GPU job finishes.
            Frame latest = null;
            foreach (var f in frames)
                if (f.submitted && f.revision == revision && f.tick <= tick && Ready(f)
                    && (latest == null || f.tick > latest.tick)) latest = f;
            if (latest != null) { displayed = latest; image.texture = latest.texture; }
        }
        CachedFrames = 0;
        foreach (var f in frames) if (f.submitted && f.revision == revision && f.tick > tick) CachedFrames++;
        // Fill spare slots with bounded work; never more than three future images.
        // Missing/zero GPU timing means no speculation, never an assumed free GPU.
        if (noPrecompute || FrameTimingManager.GetLatestTimings(1, timing) == 0) return;
        double budget = 1000.0 / Fps;
        if (timing[0].gpuFrameTime <= 0 || timing[0].gpuFrameTime > budget * 0.45 || renderCpuMs > budget * 0.35) return;
        int spare = renderedCurrent || timing[0].gpuFrameTime >= budget * 0.3 ? 1 : 2;
        for (long future = tick + 1; future < tick + Slots; future++)
        {
            if (Find(future) != null) continue;
            Frame free = Free(tick);
            if (free == null) break;
            Render(free, future); futureFrames++;
            if (--spare <= 0) break;
        }
    }
    void ReleaseTextures()
    {
        foreach (var f in frames) if (f != null) { f.texture.Release(); Destroy(f.texture); }
    }
    void OnDisable()
    {
        if (instance != this) return;
        capturing = false; instance = null;
        telemetry?.Dispose(); telemetry = null;
        if (image != null) image.texture = null;
        ReleaseTextures();
        if (canvasObject != null) Destroy(canvasObject);
        if (presentation != null) Destroy(presentation.gameObject);
        if (hiddenCanvases != null) for (int i = 0; i < hiddenCanvases.Length; i++)
            if (hiddenCanvases[i] != null) hiddenCanvases[i].enabled = canvasStates[i];
        if (source != null)
        {
            source.transform.SetPositionAndRotation(oldPosition, oldRotation);
            source.fieldOfView = oldFov; source.farClipPlane = oldFar; source.aspect = oldAspect; source.orthographic = oldOrtho; source.enabled = oldCameraEnabled;
        }
        if (controls != null) { controls.enabled = oldControlsEnabled; if (oldControlsEnabled) controls.SetFocusView(false); }
        if (swarm != null) swarm.SetMirrorCount(Predict(Time.realtimeSinceStartupAsDouble));
        Application.targetFrameRate = oldFps; QualitySettings.vSyncCount = oldVsync;
        Application.runInBackground = oldBackground;
    }
}
