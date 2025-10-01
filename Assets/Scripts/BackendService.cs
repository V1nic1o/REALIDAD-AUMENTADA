using System.Collections;
using System.Collections.Generic;
using System.Text; // --- AÑADIDO IMPORTANTE ---
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

// --- Clases para estructurar los datos del JSON ---
// Deben coincidir con los schemas de Pydantic en FastAPI

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
    [Header("Configuración del Backend")]
    public string apiBaseUrl = "http://127.0.0.1:8000"; // La URL de tu servidor FastAPI
    
    // Guardaremos el token aquí. Por ahora lo pondremos manualmente para probar.
    [TextArea] // Para que sea más fácil pegar el token en el Inspector
    public string authToken = "PEGA_UN_TOKEN_AQUÍ_PARA_PROBAR";

    // Función principal para guardar el diseño
    public IEnumerator SaveDesign(DesignCreateData designData, byte[] screenshotData)
    {
        // Creamos un formulario para enviar datos mixtos (JSON y un archivo)
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        // --- INICIO DE LA CORRECIÓN ---

        // 1. Convertimos la lista de items a un string JSON
        string designJson = JsonConvert.SerializeObject(designData);
        // 2. Convertimos ese string a un array de bytes usando UTF8
        byte[] jsonData = Encoding.UTF8.GetBytes(designJson);
        // 3. Añadimos la sección usando el constructor correcto (nombre, datos en bytes, tipo de contenido)
        formData.Add(new MultipartFormDataSection("design_data", jsonData, "application/json"));

        // --- FIN DE LA CORRECCIÓN ---


        // 4. Añadimos la captura de pantalla como un archivo binario
        if (screenshotData != null)
        {
            formData.Add(new MultipartFormFileSection("screenshot_file", screenshotData, "screenshot.png", "image/png"));
        }

        // 5. Creamos la petición web al endpoint de FastAPI
        string url = $"{apiBaseUrl}/designs/";
        UnityWebRequest request = UnityWebRequest.Post(url, formData);

        // 6. Añadimos el token de autenticación a la cabecera
        request.SetRequestHeader("Authorization", $"Bearer {authToken}");
        
        Debug.Log("Enviando datos al backend...");

        // 7. Enviamos la petición y esperamos la respuesta
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