using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

[System.Serializable]
public class DesignItemData
{
    public string item_name;
    public int quantity;
}

[System.Serializable]
public class DesignCreateData
{
    public string name;
    public List<DesignItemData> items;
}

public class BackendService : MonoBehaviour
{
    public IEnumerator SaveDesign(DesignCreateData designData, byte[] screenshotData)
    {
        string apiUrl = UnityMessageManager.ApiBaseUrl;
        string token = UnityMessageManager.AuthToken;

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token))
        {
            Debug.LogError("¡Error crítico! La URL de la API o el Token de autenticación no han sido establecidos.");
            yield break;
        }

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        
        string designJson = JsonConvert.SerializeObject(designData);
        byte[] jsonData = Encoding.UTF8.GetBytes(designJson);
        formData.Add(new MultipartFormDataSection("design_data", jsonData, "application/json"));

        if (screenshotData != null)
        {
            formData.Add(new MultipartFormFileSection("screenshot_file", screenshotData, "screenshot.png", "image/png"));
        }

        string url = $"{apiUrl}/designs/";
        UnityWebRequest request = UnityWebRequest.Post(url, formData);

        // --- INICIO DE LA SOLUCIÓN ---
        // Aumentamos el tiempo de espera a 60 segundos para darle tiempo al servidor gratuito de "despertar".
        request.timeout = 60;
        // --- FIN DE LA SOLUCIÓN ---

        request.SetRequestHeader("Authorization", $"Bearer {token}");
        
        Debug.Log("Enviando datos al backend... (Esperando hasta 60 segundos)");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("¡Diseño guardado con éxito! Respuesta: " + request.downloadHandler.text);
        }
        else
        {
            Debug.LogError("Error al guardar el diseño: " + request.error);
            Debug.LogError("Respuesta del servidor: " + request.downloadHandler.text);
        }
    }
}