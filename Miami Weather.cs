using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

public class MultiCityWeatherController : MonoBehaviour
{
    [Serializable]
    public class CityWeatherUI
    {
        [Header("Geo")]
        public string cityName;
        public float latitude;   // e.g., 33.8317
        public float longitude;  // e.g., -118.2817  (NOTE: negative in US West)

        [Header("Separate UI Fields")]
        public TMP_Text cityTitleText;    // "WEATHER IN CARSON"
        public TMP_Text conditionText;    // "Overcast"
        public TMP_Text temperatureText;  // "70°F"
        public TMP_Text uvText;           // "UV INDEX: 0"
        public TMP_Text timestampText;    // "Aug 31, 2025 09:31 PM"

        [Header("Optional combined block (legacy big panel)")]
        public TMP_Text combinedWeatherText; // optional

        [Header("Icon")]
        public RawImage weatherIcon;
    }

    [Header("Cities")]
    public CityWeatherUI[] cities;

    [Header("Misc")]
    public TMP_Text debugConsole;
    public Button refreshButton;
    public WeatherEnvironmentController envController;

    [Header("Config")]
    [Tooltip("Meteosource API key")]
    public string apiKey = "z9y3uvc040njcuxlyi7jxhqicfeqczd47hucv9bv";
    [Tooltip("Seconds between auto-refreshes")]
    public float refreshInterval = 600f;
    [Tooltip("Try to correct clearly wrong positive longitudes for US West (e.g., 118 -> -118).")]
    public bool autoFixWestLongitude = true;

    const string Units = "us"; // Fahrenheit

    void Start()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(() => StartCoroutine(FetchAllCities()));

        StartCoroutine(FetchAllCities());
        InvokeRepeating(nameof(StartCityLoop), refreshInterval, refreshInterval);
    }

    void StartCityLoop() => StartCoroutine(FetchAllCities());

    IEnumerator FetchAllCities()
    {
        foreach (var city in cities)
            yield return FetchWeather(city);
    }

    IEnumerator FetchWeather(CityWeatherUI city)
    {
        // Basic sanity guard for Western Hemisphere coords entered without '-' (common inspector typo)
        float lon = city.longitude;
        if (autoFixWestLongitude && lon > 0f && lon >= 60f && lon <= 180f)
        {
            LogWarning($"[AutoFix] '{city.cityName}' had positive longitude {lon}. Assuming Western Hemisphere -> using {-lon}.");
            lon = -lon;
        }

        string apiUrl = $"https://www.meteosource.com/api/v1/free/point?lat={city.latitude}&lon={lon}&sections=current&timezone=auto&language=en&units={Units}&key={apiKey}";
        Log($"Requesting weather for {city.cityName}: {apiUrl}");

        UnityWebRequest www = UnityWebRequest.Get(apiUrl);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            LogError($"Weather API Error for {city.cityName}: {www.error}");
            SetErrorUI(city, "Weather Error");
            yield break;
        }

        string json = www.downloadHandler.text;
        try
        {
            WeatherDataWrapper data = JsonUtility.FromJson<WeatherDataWrapper>(json);
            var w = data.current;

            // UI pieces
            string cityTitle = $"WEATHER IN {city.cityName.ToUpper()}";
            string condition = ToTitleCase(w.summary);
            string tempStr = $"{Mathf.RoundToInt(w.temperature)}°F";
            string uvStr = $"UV INDEX: {Mathf.RoundToInt(w.uv_index)}";

            // Timestamp: we don’t get per-city local time back in free response, so display “now” (client time).
            // If you prefer fixed offsets per city, replace with your GetUtcOffset() logic.
            string timeStr = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");

            if (city.cityTitleText)    city.cityTitleText.text    = cityTitle;
            if (city.conditionText)    city.conditionText.text    = condition;
            if (city.temperatureText)  city.temperatureText.text  = tempStr;
            if (city.uvText)           city.uvText.text           = uvStr;
            if (city.timestampText)    city.timestampText.text    = timeStr;

            if (city.combinedWeatherText)
                city.combinedWeatherText.text =
                    $"{cityTitle}:\n{condition}\nTemp: {tempStr}\n{uvStr}\n{timeStr}";

            // Drive your scene environment
            envController?.ApplyWeather(w.summary.ToLower(), DateTime.Now.Hour);

            // Icon
            if (!string.IsNullOrEmpty(w.icon) && city.weatherIcon != null)
            {
                string iconUrl = $"https://www.meteosource.com/static/img/weather_icons/{w.icon}.png";
                StartCoroutine(LoadIcon(iconUrl, city.weatherIcon));
            }
        }
        catch (Exception ex)
        {
            LogError($"JSON Parse Error for {city.cityName}: {ex.Message}");
            SetErrorUI(city, "Weather Error");
        }
    }

    IEnumerator LoadIcon(string url, RawImage image)
    {
        UnityWebRequest iconReq = UnityWebRequestTexture.GetTexture(url);
        yield return iconReq.SendWebRequest();

        if (iconReq.result == UnityWebRequest.Result.Success)
        {
            Texture2D icon = ((DownloadHandlerTexture)iconReq.downloadHandler).texture;
            image.texture = icon;
        }
        else
        {
            LogError("Icon Load Error: " + iconReq.error);
        }
    }

    // --- Helpers ---
    void SetErrorUI(CityWeatherUI city, string msg)
    {
        if (city.cityTitleText) city.cityTitleText.text = $"WEATHER IN {city.cityName.ToUpper()}";
        if (city.conditionText) city.conditionText.text = msg;
        if (city.temperatureText) city.temperatureText.text = "";
        if (city.uvText) city.uvText.text = "";
        if (city.timestampText) city.timestampText.text = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
        if (city.combinedWeatherText) city.combinedWeatherText.text = $"{city.cityName}\n{msg}";
    }

    static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLower());
    }

    void Log(string msg)
    {
        Debug.Log(msg);
        if (debugConsole != null) debugConsole.text = msg + "\n" + debugConsole.text;
    }
    void LogWarning(string msg)
    {
        Debug.LogWarning(msg);
        if (debugConsole != null) debugConsole.text = "<color=yellow>" + msg + "</color>\n" + debugConsole.text;
    }
    void LogError(string msg)
    {
        Debug.LogError(msg);
        if (debugConsole != null) debugConsole.text = "<color=red>" + msg + "</color>\n" + debugConsole.text;
    }

    // --- DTOs ---
    [Serializable] public class WeatherDataWrapper { public CurrentWeather current; }
    [Serializable] public class CurrentWeather
    {
        public string summary;
        public float temperature; // already °F with units=us
        public string icon;
        public float uv_index;
    }
}
