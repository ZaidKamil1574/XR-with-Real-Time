using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GoogleMultiRouteTrafficController : MonoBehaviour
{
    [System.Serializable]
    public class RouteInfoUI
    {
        public string routeName;
        public string origin;
        public string destination;
        public TMP_Text statusText;
        public TMP_Text timeText;
        public TMP_Text distanceText;

        public GameObject heavyTrafficModel;  // Prefab for Heavy Traffic
        public GameObject moderateTrafficModel; // Prefab for Moderate Traffic
        public GameObject smoothFlowModel; // Prefab for Smooth Flow
    }

    [Header("Google API")]
    public string apiKey;

    [Header("Routes to Track")]
    public RouteInfoUI[] routes;

    [Header("Refresh Controls")]
    public Button refreshButton;
    public float refreshIntervalSeconds = 300f; // Every 5 minutes

    void Start()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(() => StartCoroutine(FetchAllRoutes()));

        StartCoroutine(FetchAllRoutes());
        InvokeRepeating(nameof(AutoRefresh), refreshIntervalSeconds, refreshIntervalSeconds);
    }

    void AutoRefresh()
    {
        StartCoroutine(FetchAllRoutes());
    }

    IEnumerator FetchAllRoutes()
    {
        foreach (var route in routes)
        {
            yield return FetchRouteData(route);
        }
    }

    IEnumerator FetchRouteData(RouteInfoUI route)
    {
        string url = $"https://maps.googleapis.com/maps/api/directions/json?origin={route.origin}&destination={route.destination}&departure_time=now&traffic_model=best_guess&key={apiKey}";
        UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            route.statusText.text = $"{route.routeName}\nError: {req.error}";
            route.timeText.text = "";
            route.distanceText.text = "";
            yield break;
        }

        try
        {
            string raw = req.downloadHandler.text;
            TrafficResponse data = JsonUtility.FromJson<Wrapper>($"{{\"data\":{raw}}}").data;
            var leg = data.routes[0].legs[0];

            float minutes = leg.duration_in_traffic.value / 60f;
            string distance = leg.distance.text;

            // Interpret traffic severity
            float delayRatio = (float)leg.duration_in_traffic.value / leg.duration.value;
            string status = delayRatio > 1.3f ? "Heavy Traffic" 
                          : delayRatio > 1.1f ? "Moderate Traffic" 
                          : "Smooth Flow"; 

            // Update UI
            route.statusText.text = $"{route.routeName}\nTraffic: {status}";
            route.timeText.text = $"Now: {minutes:F1} mins";
            route.distanceText.text = $"Distance: {distance}";

            // Update the active model based on the traffic condition
            SetTrafficModel(route, status);
        }
        catch (System.Exception ex)
        {
            route.statusText.text = $"{route.routeName}\nError: Parse Failed";
            Debug.LogError($"Traffic JSON parse error: {ex.Message}");
        }
    }

    void SetTrafficModel(RouteInfoUI route, string status)
    {
        // Deactivate all models first
        route.heavyTrafficModel.SetActive(false);
        route.moderateTrafficModel.SetActive(false);
        route.smoothFlowModel.SetActive(false);

        // Activate the model based on the traffic status
        if (status == "Heavy Traffic")
        {
            route.heavyTrafficModel.SetActive(true);
        }
        else if (status == "Moderate Traffic")
        {
            route.moderateTrafficModel.SetActive(true);
        }
        else if (status == "Smooth Flow")
        {
            route.smoothFlowModel.SetActive(true);
        }
    }

    // JSON Helpers
    [System.Serializable] public class Wrapper { public TrafficResponse data; }
    [System.Serializable] public class TrafficResponse { public Route[] routes; }
    [System.Serializable] public class Route { public Leg[] legs; }
    [System.Serializable] public class Leg
    {
        public Duration duration;
        public Duration duration_in_traffic;
        public Distance distance;
    }
    [System.Serializable] public class Duration { public int value; }
    [System.Serializable] public class Distance { public string text; public int value; }
}
