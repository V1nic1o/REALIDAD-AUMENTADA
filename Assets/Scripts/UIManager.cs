using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    [Header("Referencias a Servicios")]
    public BackendService backendService; // Referencia a nuestro nuevo script
    public PlaceObjectOnTap objectPlacer; // Referencia al script que conoce la capa

    [Header("Paneles Principales")]
    public GameObject catalogPanel;

    [Header("UI de Guardado de Diseño")]
    public GameObject saveDesignPanel;
    public TMP_InputField nameInputField;
    
    [Header("Botones de Acción Principales")]
    public GameObject openSavePanelButtonGO;
    public GameObject toggleCatalogButtonGO;

    void Start()
    {
        // (El Start se queda como lo tienes, conectando los botones desde el Inspector)
        saveDesignPanel.SetActive(false);
    }

    public void OpenSavePanel()
    {
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(false);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(false);
        if (catalogPanel != null && catalogPanel.activeSelf) { catalogPanel.SetActive(false); }
        if (saveDesignPanel != null) { saveDesignPanel.SetActive(true); }
    }

    public void CloseSavePanel()
    {
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(true);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(true);
        if (saveDesignPanel != null) { saveDesignPanel.SetActive(false); }
    }

    // Esta función ahora inicia una corrutina
    public void OnConfirmSave()
    {
        if (nameInputField == null || string.IsNullOrWhiteSpace(nameInputField.text))
        {
            Debug.LogWarning("El nombre del diseño no puede estar vacío.");
            return; 
        }
        
        // Iniciamos la corrutina que hará todo el trabajo
        StartCoroutine(SaveProcess());
    }

    private IEnumerator SaveProcess()
    {
        string designName = nameInputField.text;
        Debug.Log($"Iniciando proceso de guardado para el diseño: {designName}");
        
        // Cerramos el panel para que no salga en la foto
        CloseSavePanel();
        
        // --- 1. Contar los Objetos en la Escena ---
        List<DesignItemData> items = new List<DesignItemData>();

        // --- CORRECCIÓN ---
        // Usamos el método nuevo que recomienda Unity
        PlaceableObject[] placedObjects = FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);

        // Agrupamos por nombre y contamos
        var itemGroups = placedObjects.GroupBy(obj => obj.itemName)
                                      .Select(group => new DesignItemData
                                      {
                                          item_name = group.Key,
                                          quantity = group.Count()
                                      });
        items.AddRange(itemGroups);

        // --- 2. Esperar un frame y Tomar la Captura de Pantalla ---
        yield return new WaitForEndOfFrame();
        
        Texture2D screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] screenshotData = screenshotTexture.EncodeToPNG();
        Destroy(screenshotTexture); // Liberar memoria

        // --- 3. Crear el Objeto de Datos y Llamar al BackendService ---
        DesignCreateData designData = new DesignCreateData
        {
            name = designName,
            items = items
        };

        if (backendService != null)
        {
            yield return StartCoroutine(backendService.SaveDesign(designData, screenshotData));
        }
        else
        {
            Debug.LogError("BackendService no está asignado en el UIManager.");
        }
    }
}