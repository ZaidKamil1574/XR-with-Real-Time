using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ShowCanvasOnHover : MonoBehaviour
{
    [Header("References")]
    public GameObject infoCanvas;   // The black canvas with traffic info

    private void Start()
    {
        if (infoCanvas != null)
            infoCanvas.SetActive(false); // Hide canvas at start
    }

    // Called when controller ray or XRInteractor hovers
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("XRController") || other.CompareTag("PlayerHand"))
        {
            if (infoCanvas != null)
                infoCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("XRController") || other.CompareTag("PlayerHand"))
        {
            if (infoCanvas != null)
                infoCanvas.SetActive(false);
        }
    }
}
