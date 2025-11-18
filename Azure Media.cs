using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using System.Collections;

[RequireComponent(typeof(VideoPlayer))]
public class AzureBlobVideoDiagnostics : MonoBehaviour
{
    public VideoPlayer player;          // drag your VideoPlayer
    [TextArea(2,6)] public string sasUrl; // paste SAS here
    public AudioSource audioSource;     // optional

    void Awake()
    {
        if (!player) player = GetComponent<VideoPlayer>();
        player.source = VideoSource.Url;
        player.url = sasUrl;
        player.renderMode = VideoRenderMode.RenderTexture;

        if (audioSource)
        {
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audioSource);
        }
        else
        {
            player.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        player.errorReceived += (vp, msg) => Debug.LogError("[VideoPlayer] " + msg);
        player.prepareCompleted += vp => { Debug.Log("[VideoPlayer] Prepared. Playing..."); vp.Play(); };
        player.seekCompleted += vp => Debug.Log("[VideoPlayer] Seek completed");
        player.started += vp => Debug.Log("[VideoPlayer] Started");
        player.loopPointReached += vp => Debug.Log("[VideoPlayer] Reached end");
        player.frameDropped += vp => Debug.LogWarning("[VideoPlayer] Frame dropped");
    }

    void Start() { StartCoroutine(CheckAndPlay()); }

    IEnumerator CheckAndPlay()
    {
        // Sanity check the URL before giving it to the player
        Debug.Log("[Azure] HEAD " + sasUrl);
        using (var head = UnityWebRequest.Head(sasUrl))
        {
            head.redirectLimit = 16;
            head.timeout = 30;
            yield return head.SendWebRequest();

            if (head.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Azure] HEAD failed: " + head.error);
                yield break;
            }

            Debug.Log("[Azure] 200 OK - Content-Type: " + head.GetResponseHeader("Content-Type"));
        }

        Debug.Log("[VideoPlayer] Preparing...");
        player.Prepare();
        while (!player.isPrepared) yield return null;
        // prepareCompleted handler will call Play()
    }
}
