using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System;

public class CityDashboardController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text losAngelesText;
    public TMP_Text pasadenaText;
    public TMP_Text sanDiegoText;
    public TMP_Text debugConsole;

    [Header("API Keys")]
    public string censusApiKey = "3a36eeefb7537a4da8f2206f863cced7147415e4";

    void Start()
    {
        StartCoroutine(FetchPopulationData());
        InvokeRepeating(nameof(RefreshData), 0, 900); // every 15 min
    }

    void RefreshData()
    {
        StartCoroutine(FetchPopulationData());
    }

    IEnumerator FetchPopulationData()
    {
        string url = $"https://api.census.gov/data/2021/acs/acs5?get=B01003_001E,NAME&for=place:*&in=state:06&key={censusApiKey}";
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            LogError("Census API Error: " + www.error);
            SetAllCityText("Population data could not be retrieved.");
        }
        else
        {
            string rawJson = www.downloadHandler.text;
            rawJson = rawJson.Replace("[[", "").Replace("]]", "");
            string[] rows = rawJson.Split(new[] { "],[" }, StringSplitOptions.None);

            string losAngeles = FindPopulation(rows, "Los Angeles city, California");
            string pasadena = FindPopulation(rows, "Pasadena city, California");
            string sanDiego = FindPopulation(rows, "San Diego city, California");

            losAngelesText.text = losAngeles;
            pasadenaText.text = pasadena;
            sanDiegoText.text = sanDiego;
        }
    }

    string FindPopulation(string[] rows, string cityFullName)
    {
        foreach (string row in rows)
        {
            string cleaned = row.Replace("\"", "").Trim();
            string[] parts = cleaned.Split(',');

            if (parts.Length >= 2 && parts[1].Trim().Equals(cityFullName, StringComparison.OrdinalIgnoreCase))
            {
                return $"{cityFullName} Population: {parts[0]}";
            }
        }

        return $"Population data for {cityFullName} not found.";
    }

    void SetAllCityText(string msg)
    {
        losAngelesText.text = msg;
        pasadenaText.text = msg;
        sanDiegoText.text = msg;
    }

    void LogError(string msg)
    {
        Debug.LogError(msg);
        if (debugConsole != null)
            debugConsole.text = "<color=red>" + msg + "</color>\n" + debugConsole.text;
    }
}
