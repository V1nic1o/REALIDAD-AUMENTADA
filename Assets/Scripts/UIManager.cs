using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Paneles Principales")]
    public GameObject catalogPanel;

    [Header("UI de Guardado de Diseño")]
    public GameObject saveDesignPanel;
    public TMP_InputField nameInputField;
    
    // --- NUEVAS VARIABLES ---
    [Header("Botones de Acción Principales")]
    public GameObject openSavePanelButtonGO; // El GameObject del botón "Guardar Diseño"
    public GameObject toggleCatalogButtonGO; // El GameObject del botón "Abrir Catálogo"

    void Start()
    {
        // Asegurarse de que el panel de guardado esté oculto al inicio
        if (saveDesignPanel != null)
        {
            saveDesignPanel.SetActive(false);
        }
    }

    // --- MÉTODOS PÚBLICOS ---
    // Estas funciones son llamadas por los botones desde el Inspector.

    public void OpenSavePanel()
    {
        // Ocultamos los botones principales
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(false);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(false);

        // Cerramos el catálogo si está abierto
        if (catalogPanel != null && catalogPanel.activeSelf)
        {
            catalogPanel.SetActive(false);
        }

        // Mostramos el panel de guardado
        if (saveDesignPanel != null)
        {
            saveDesignPanel.SetActive(true);
        }
    }

    public void CloseSavePanel()
    {
        // Mostramos de nuevo los botones principales
        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(true);
        if (toggleCatalogButtonGO != null) toggleCatalogButtonGO.SetActive(true);

        // Ocultamos el panel de guardado
        if (saveDesignPanel != null)
        {
            saveDesignPanel.SetActive(false);
        }
    }

    public void OnConfirmSave()
    {
        if (nameInputField == null || string.IsNullOrWhiteSpace(nameInputField.text))
        {
            Debug.LogWarning("El nombre del diseño no puede estar vacío.");
            return; 
        }

        string designName = nameInputField.text;
        Debug.Log($"Iniciando proceso de guardado para el diseño: {designName}");
        
        // --- PRÓXIMO PASO: AQUÍ LLAMAREMOS A LA LÓGICA PARA GUARDAR EN EL BACKEND ---
        
        // Cerramos el panel (esto automáticamente volverá a mostrar los botones)
        CloseSavePanel();
    }
}