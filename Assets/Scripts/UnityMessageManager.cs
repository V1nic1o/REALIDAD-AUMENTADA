// UnityMessageManager.cs
using UnityEngine;
using System; // Necesario para usar la clase Uri

public class UnityMessageManager : MonoBehaviour
{
    // Hacemos una instancia estática para un acceso fácil y global
    public static UnityMessageManager Instance { get; private set; }
    
    // Las variables estáticas para guardar la configuración
    public static string ApiBaseUrl { get; private set; }
    public static string AuthToken { get; private set; }

    // Awake se llama antes que Start. Es ideal para inicializar.
    private void Awake()
    {
        // Patrón Singleton: nos aseguramos de que solo haya una instancia de este manager.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Hacemos que este objeto persista entre escenas.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start se llama en el primer frame. Ideal para leer la URL de lanzamiento.
    private void Start()
    {
        // Obtenemos la URL con la que se lanzó la aplicación.
        string launchUrl = Application.absoluteURL;

        // Si la URL no está vacía y empieza con nuestro esquema, la procesamos.
        if (!string.IsNullOrEmpty(launchUrl) && launchUrl.StartsWith("jardinAR://"))
        {
            Debug.Log($"[UnityMessageManager] App lanzada con URL: {launchUrl}");
            ParseUrlAndSetConfig(launchUrl);
        }
        else
        {
            Debug.Log("[UnityMessageManager] App lanzada de forma normal (no por deep link).");
        }
    }

    private void ParseUrlAndSetConfig(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            // La propiedad 'Query' nos da la parte de los parámetros, ej: "?token=...&apiUrl=..."
            string query = uri.Query;

            if (!string.IsNullOrEmpty(query))
            {
                // Quitamos el '?' del principio
                query = query.Substring(1); 
                
                string[] parameters = query.Split('&');
                foreach (string param in parameters)
                {
                    string[] parts = param.Split('=');
                    if (parts.Length == 2)
                    {
                        string key = parts[0];
                        // Usamos Uri.UnescapeDataString para decodificar caracteres especiales
                        string value = Uri.UnescapeDataString(parts[1]);

                        if (key == "token")
                        {
                            SetAuthToken(value);
                        }
                        else if (key == "apiUrl")
                        {
                            SetApiBaseUrl(value);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UnityMessageManager] Error al parsear la URL: {ex.Message}");
        }
    }

    // Estas funciones ahora pueden ser llamadas internamente por el deep link,
    // o externamente si se usara el Plan A. Son reutilizables.
    public void SetApiBaseUrl(string url)
    {
        ApiBaseUrl = url;
        Debug.Log($"[UnityMessageManager] URL de la API establecida en: {ApiBaseUrl}");
    }

    public void SetAuthToken(string token)
    {
        AuthToken = token;
        Debug.Log($"[UnityMessageManager] Token de autenticación recibido y guardado.");
    }

    public void OnDesignSaved(string designId)
    {
        Debug.Log($"[UnityMessageManager] El diseño con ID {designId} fue guardado.");
    }
}