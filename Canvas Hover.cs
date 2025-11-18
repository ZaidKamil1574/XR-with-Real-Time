using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
public class HoverPopupAnimator : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("The world-space Canvas (or its topmost panel) that should rise/fade in.")]
    public Transform popupTransform;         // e.g., the black panel root
    [Tooltip("Optional: CanvasGroup on the popup for smooth fade. If null, alpha fade is skipped.")]
    public CanvasGroup popupCanvasGroup;

    [Header("Animation")]
    [Tooltip("How far the popup rises when shown (in local units).")]
    public float riseHeight = 0.25f;
    [Tooltip("Seconds to animate.")]
    public float duration = 0.25f;
    [Tooltip("Ease curve for the animation (0..1 time -> 0..1 value).")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Start hidden on play?")]
    public bool startHidden = true;

    [Header("XR Hover (optional)")]
    [Tooltip("If the road has an XRBaseInteractable, hover events are used automatically.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable xrInteractable;   // auto-found if left empty

    [Header("Collider Hover (fallback)")]
    [Tooltip("Tags treated as controller/laser colliders when they enter/exit this object's trigger.")]
    public string[] controllerTags = new[] { "XRController", "PlayerHand" };

    // internals
    Vector3 baseLocalPos;
    Vector3 hiddenLocalPos;
    Vector3 shownLocalPos;
    Coroutine animCo;
    bool isShown;

    void Awake()
    {
        if (!popupTransform)
        {
            Debug.LogError("[HoverPopupAnimator] Assign popupTransform.", this);
            enabled = false;
            return;
        }

        if (!xrInteractable) xrInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (xrInteractable)
        {
            xrInteractable.hoverEntered.AddListener(OnHoverEnteredXR);
            xrInteractable.hoverExited.AddListener(OnHoverExitedXR);
        }

        baseLocalPos = popupTransform.localPosition;
        shownLocalPos = baseLocalPos + Vector3.up * riseHeight;
        hiddenLocalPos = baseLocalPos;

        // Start state
        if (startHidden)
        {
            isShown = false;
            popupTransform.localPosition = hiddenLocalPos;
            if (popupCanvasGroup) popupCanvasGroup.alpha = 0f;
            SetActiveIfCanvas(false);
        }
        else
        {
            isShown = true;
            popupTransform.localPosition = shownLocalPos;
            if (popupCanvasGroup) popupCanvasGroup.alpha = 1f;
            SetActiveIfCanvas(true);
        }
    }

    void OnDestroy()
    {
        if (xrInteractable)
        {
            xrInteractable.hoverEntered.RemoveListener(OnHoverEnteredXR);
            xrInteractable.hoverExited.RemoveListener(OnHoverExitedXR);
        }
    }

    // XR hover handlers
    void OnHoverEnteredXR(HoverEnterEventArgs _)
    {
        Show();
    }

    void OnHoverExitedXR(HoverExitEventArgs _)
    {
        Hide();
    }

    // Trigger-collider fallback (make sure this GameObject has a trigger collider)
    void OnTriggerEnter(Collider other)
    {
        if (MatchesControllerTag(other)) Show();
    }

    void OnTriggerExit(Collider other)
    {
        if (MatchesControllerTag(other)) Hide();
    }

    bool MatchesControllerTag(Component other)
    {
        foreach (var t in controllerTags)
            if (!string.IsNullOrEmpty(t) && other.CompareTag(t))
                return true;
        return false;
    }

    // Public controls (if you want to call them from elsewhere)
    public void Show()
    {
        if (isShown) return;
        isShown = true;
        if (animCo != null) StopCoroutine(animCo);
        SetActiveIfCanvas(true); // ensure enabled before anim
        animCo = StartCoroutine(Animate(hiddenLocalPos, shownLocalPos, 0f, 1f));
    }

    public void Hide()
    {
        if (!isShown) return;
        isShown = false;
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(Animate(shownLocalPos, hiddenLocalPos, 1f, 0f, deactivateAtEnd:true));
    }

    IEnumerator Animate(Vector3 fromPos, Vector3 toPos, float fromA, float toA, bool deactivateAtEnd = false)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / duration));
            popupTransform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
            if (popupCanvasGroup) popupCanvasGroup.alpha = Mathf.Lerp(fromA, toA, k);
            yield return null;
        }
        popupTransform.localPosition = toPos;
        if (popupCanvasGroup) popupCanvasGroup.alpha = toA;
        if (deactivateAtEnd) SetActiveIfCanvas(false);
        animCo = null;
    }

    void SetActiveIfCanvas(bool state)
    {
        // If the popup is a Canvas root, toggling the GameObject helps performance when hidden.
        // Safe to leave always-on if you prefer; animation will still work.
        var canvas = popupTransform.GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode == RenderMode.WorldSpace)
            canvas.gameObject.SetActive(state);
    }
}
