using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class AzureSasDownloadAndPlay : MonoBehaviour
{
    [Header("Paste your full SAS URL (https://...mp4?...sig=...)")]
    [TextArea(2, 6)] public string sasUrl;

    [Header("References")]
    public VideoPlayer videoPlayer;       // Drag your VideoPlayer here
    public AudioSource audioSource;       // Optional: drag an AudioSource if you want audio

    [Header("Options")]
    public string fileName = "IMMERSIVE_VR_ManCity_fixed_cached.mp4";
    public bool overwriteExisting = false;  // set true to force re-download each run
    public bool autoPlay = true;

    string LocalPath => Path.Combine(Application.persistentDataPath, fileName);

    void Reset()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        audioSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (!videoPlayer) videoPlayer = GetComponent<VideoPlayer>();

        // Set up audio route if provided
        if (audioSource)
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }
        else
        {
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }

        // Diagnostics
        videoPlayer.errorReceived += (_, msg) => Debug.LogError("[VideoPlayer] " + msg);
        videoPlayer.prepareCompleted += _ =>
        {
            Debug.Log("[VideoPlayer] Prepared. Playing…");
            if (autoPlay) { videoPlayer.Play(); if (audioSource) audioSource.Play(); }
        };
    }

    IEnumerator Start()
    {
        if (string.IsNullOrWhiteSpace(sasUrl))
        {
            Debug.LogError("[AzureSAS] SAS URL is empty.");
            yield break;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));

        if (File.Exists(LocalPath) && !overwriteExisting)
        {
            Debug.Log("[AzureSAS] Using cached file: " + LocalPath);
            PlayLocal(LocalPath);
            yield break;
        }

        Debug.Log("[AzureSAS] Downloading to: " + LocalPath);
        yield return StartCoroutine(DownloadToFile(sasUrl, LocalPath));

        if (File.Exists(LocalPath))
        {
            Debug.Log("[AzureSAS] Download complete: " + LocalPath);
            PlayLocal(LocalPath);
        }
        else
        {
            Debug.LogError("[AzureSAS] Download failed — file not found.");
        }
    }

    void PlayLocal(string path)
    {
        var fileUrl = "file://" + path;     // NOTE: two slashes is correct here
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = fileUrl;
        Debug.Log("[AzureSAS] Playing local: " + fileUrl);
        videoPlayer.Prepare();              // triggers prepareCompleted → Play
    }

    IEnumerator DownloadToFile(string url, string finalPath)
    {
        var tempPath = finalPath + ".part";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        using (var req = UnityWebRequest.Get(url))
        {
            // Stream to disk (no big memory spikes)
            req.downloadHandler = new DownloadHandlerFile(tempPath) { removeFileOnAbort = true };
            var op = req.SendWebRequest();

            while (!op.isDone)
            {
                // Optional: expose progress UI via req.downloadProgress (0..1)
                yield return null;
            }

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError("[AzureSAS] Download error: " + req.error);
                yield break;
            }
        }

        // Atomically move temp → final
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(tempPath, finalPath);
    }

    // Call this from a button if you ever want to clear cache
    public void DeleteCachedFile()
    {
        if (File.Exists(LocalPath)) { File.Delete(LocalPath); Debug.Log("[AzureSAS] Cache cleared."); }
    }

    // Helper to open the folder (Editor only)
#if UNITY_EDITOR
    [ContextMenu("Reveal persistentDataPath")]
    void RevealPersistentDataPath() => UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);
#endif
}
