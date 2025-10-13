using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class UIManager : MonoBehaviour
{
    [Header("Referencias a Servicios")]
    public BackendService backendService;
    public PlaceObjectOnTap objectPlacer;

    [Header("Paneles Principales")]
    public GameObject catalogPanel;

    [Header("UI de Guardado de Diseño")]
    public GameObject saveDesignPanel;
    public TMP_InputField nameInputField;

    [Header("UI de Carga")]
    public GameObject loadingPanel;

    [Header("Botones de Acción Principales")]
    public GameObject openSavePanelButtonGO;
    public GameObject toggleCatalogButtonGO;

    public static bool IsUIOpen { get; private set; }
    
    private bool isSaving = false;

    void Start()
    {
        IsUIOpen = false;
        if (saveDesignPanel != null) saveDesignPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    public void OpenSavePanel()
    {
        IsUIOpen = true; 

        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(false);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(false);
        if (catalogPanel != null && catalogPanel.activeSelf) { catalogPanel.SetActive(false); }
        if (saveDesignPanel != null) { saveDesignPanel.SetActive(true); }
    }

    public void CloseSavePanel()
    {
        IsUIOpen = false; 

        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(true);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(true);
        if (saveDesignPanel != null) { saveDesignPanel.SetActive(false); }
    }

    public void OnConfirmSave()
    {
        if (isSaving) return;

        if (nameInputField == null || string.IsNullOrWhiteSpace(nameInputField.text))
        {
            Debug.LogWarning("El nombre del diseño no puede estar vacío.");
            return; 
        }
        
        StartCoroutine(SaveProcess());
    }

    private IEnumerator SaveProcess()
    {
        isSaving = true;

        string designName = nameInputField.text;
        nameInputField.text = "";

        Debug.Log($"Iniciando proceso de guardado para el diseño: {designName}");
        
        // --- INICIO DE LA LÓGICA DE CAPTURA LIMPIA ---

        // 1. Ocultamos toda la UI visible antes de la captura.
        if (saveDesignPanel != null) saveDesignPanel.SetActive(false);
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(false);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(false);
        if (catalogPanel != null) catalogPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false); // Nos aseguramos de que esté oculto

        // 2. Esperamos un frame para que la UI desaparezca de la pantalla.
        yield return new WaitForEndOfFrame();
        
        // 3. Tomamos la captura de pantalla "limpia".
        Texture2D screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] screenshotData = screenshotTexture.EncodeToPNG();
        Destroy(screenshotTexture);

        // 4. Volvemos a mostrar el panel de carga para el usuario.
        if (loadingPanel != null) loadingPanel.SetActive(true);
        IsUIOpen = true; // Mantenemos las interacciones bloqueadas mientras carga

        // --- FIN DE LA LÓGICA DE CAPTURA LIMPIA ---

        List<DesignItemData> items = new List<DesignItemData>();
        PlaceableObject[] placedObjects = FindObjectsByType<PlaceableObject>(FindObjectsSortMode.None);
        var itemGroups = placedObjects.GroupBy(obj => obj.itemName)
                                      .Select(group => new DesignItemData
                                      {
                                          item_name = group.Key,
                                          quantity = group.Count()
                                      });
        items.AddRange(itemGroups);

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

        // --- OCULTAMOS Y DESBLOQUEAMOS AL FINAL ---
        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(true); // Reactivamos los botones principales
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(true);
        
        IsUIOpen = false;
        isSaving = false;
    }
}