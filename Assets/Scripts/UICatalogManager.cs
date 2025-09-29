using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class CatalogManager : MonoBehaviour
{
    [Header("Panel Principal")]
    public GameObject catalogPanel;
    public Button toggleButton;
    public Button closeButton;

    [Header("Iconos Opcionales")]
    public Sprite iconOpen;
    public Sprite iconClose;

    [Header("Contenido Dinámico del Catálogo")]
    public Transform contentContainer;
    public GameObject subcategoryPrefab;
    public GameObject itemCardPrefab;
    
    [Header("Configuración de Thumbnails (Ajuste Fino)")]
    [Tooltip("Ajusta la rotación del modelo.")]
    public Vector3 modelRotation = new Vector3(15, -30, 0);
    [Tooltip("Mueve el modelo ligeramente desde el centro para un ajuste final.")]
    public Vector3 positionFineTune = new Vector3(0, 0, 0);
    [Tooltip("Multiplicador de escala. 1 = tamaño normal, 1.5 = 50% más grande.")]
    public float scaleMultiplier = 1.0f;
    [Tooltip("Ajuste del zoom automático. 1 = encuadre perfecto, 1.2 = aleja la cámara (más borde).")]
    public float zoomMultiplier = 1.2f;

    private GameObject _thumbnailCameraRig;
    private bool isPanelOpen = false;
    private const string CATALOG_BASE_PATH = "Catalogo";
    public static GameObject SelectedModelPrefab { get; private set; }

    void Start()
    {
        catalogPanel.SetActive(false);
        if (toggleButton != null)
        {
            toggleButton.gameObject.SetActive(true);
            toggleButton.onClick.AddListener(OpenCatalogPanel);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseCatalogPanel);
        }
        isPanelOpen = false;
    }

    #region Lógica para Abrir y Cerrar el Panel

    public void OpenCatalogPanel()
    {
        isPanelOpen = true;
        catalogPanel.SetActive(true);
        if (toggleButton != null) toggleButton.gameObject.SetActive(false);
        if (toggleButton != null && iconOpen != null && iconClose != null) toggleButton.image.sprite = iconClose;
        
        DisplayCategory("Vegetacion");
        Debug.Log("🔼 Panel abierto");
    }

    public void CloseCatalogPanel()
    {
        isPanelOpen = false;
        catalogPanel.SetActive(false);
        if (toggleButton != null) toggleButton.gameObject.SetActive(true);
        if (toggleButton != null && iconOpen != null && iconClose != null) toggleButton.image.sprite = iconOpen;
        
        DestroyThumbnailCameraRig();
        Debug.Log("🔽 Panel cerrado");
    }

    #endregion

    #region Lógica del Catálogo Dinámico

    public void ShowVegetacionCategory() { DisplayCategory("Vegetacion"); }
    public void ShowSuperficiesCategory() { DisplayCategory("Superficies"); }
    public void ShowDecoracionCategory() { DisplayCategory("Decoracion"); }

    private void DisplayCategory(string categoryName)
    {
        DestroyThumbnailCameraRig();
        
        int thumbnailLayer = LayerMask.NameToLayer("ThumbnailLayer");
        if (thumbnailLayer == -1)
        {
            Debug.LogError("¡ERROR CRÍTICO! La capa 'ThumbnailLayer' no existe.");
            return;
        }

        _thumbnailCameraRig = CreateThumbnailCameraRig(thumbnailLayer);

        foreach (Transform child in contentContainer) Destroy(child.gameObject);

        string indexPath = $"{CATALOG_BASE_PATH}/{categoryName}/{categoryName}_index";
        TextAsset indexFile = Resources.Load<TextAsset>(indexPath);
        if (indexFile == null)
        {
            Debug.LogError($"No se encontró el archivo de índice en 'Resources/{indexPath}'.");
            return;
        }
        string[] subcategoryNames = indexFile.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);

        // --- INICIO DE LA MODIFICACIÓN ---
        // Forzamos al layout principal a reconstruirse para tener el ancho correcto al inicio.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);

        foreach (string subcategoryName in subcategoryNames)
        {
            GameObject subcategoryHeader = Instantiate(subcategoryPrefab, contentContainer);
            var titleText = subcategoryHeader.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (titleText != null) titleText.text = subcategoryName;

            GameObject gridContainer = new GameObject(subcategoryName + " Grid");
            gridContainer.transform.SetParent(contentContainer, false);
            GridLayoutGroup gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
            
            // 1. AUMENTAMOS LA ALTURA DE LA CELDA PARA DAR ESPACIO AL NOMBRE
            Vector2 cellSize = new Vector2(150, 180); // 150 para la imagen + 30 para el texto
            Vector2 spacing = new Vector2(15, 15);
            RectOffset padding = new RectOffset(10, 10, 10, 10);
            
            // 2. CALCULAMOS CUÁNTAS COLUMNAS CABEN EN EL ANCHO DEL PANEL
            float parentWidth = (contentContainer as RectTransform).rect.width;
            int columnCount = Mathf.FloorToInt(
                (parentWidth - padding.left - padding.right + spacing.x) / 
                (cellSize.x + spacing.x)
            );
            columnCount = Mathf.Max(1, columnCount); // Aseguramos que haya al menos 1 columna

            // 3. CONFIGURAMOS EL GRID CON LOS VALORES CALCULADOS
            gridLayout.padding = padding;
            gridLayout.cellSize = cellSize; 
            gridLayout.spacing = spacing;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; // Fijamos el número de columnas
            gridLayout.constraintCount = columnCount;

            ContentSizeFitter fitter = gridContainer.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // --- FIN DE LA MODIFICACIÓN ---

            string modelsPath = $"{CATALOG_BASE_PATH}/{categoryName}/{subcategoryName}";
            var models = Resources.LoadAll<GameObject>(modelsPath);

            foreach (var modelPrefab in models)
            {
                GameObject card = Instantiate(itemCardPrefab, gridContainer.transform);
                var cardText = card.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (cardText != null) cardText.text = modelPrefab.name;

                Image thumbnailImage = card.transform.Find("ItemImage").GetComponent<Image>();
                StartCoroutine(GenerateThumbnail(modelPrefab, thumbnailLayer, (sprite) => {
                    if (thumbnailImage != null && sprite != null) thumbnailImage.sprite = sprite;
                }));

                card.GetComponent<Button>().onClick.AddListener(() => OnModelSelected(modelPrefab));
            }
        }
    }
    
    private IEnumerator GenerateThumbnail(GameObject modelPrefab, int layer, System.Action<Sprite> callback)
    {
        if (_thumbnailCameraRig == null) { callback(null); yield break; }
        Camera cam = _thumbnailCameraRig.GetComponentInChildren<Camera>();

        GameObject modelInstance = Instantiate(modelPrefab);
        modelInstance.transform.localScale *= scaleMultiplier;
        SetLayerRecursively(modelInstance, layer);
        yield return null;

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (hasBounds) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; hasBounds = true; }
        }

        if (!hasBounds)
        {
            Destroy(modelInstance);
            callback(null);
            yield break;
        }
        
        float objectSize = Mathf.Max(bounds.size.x, bounds.size.y);
        
        float cameraSize = (objectSize / 2f);
        cam.orthographicSize = cameraSize * zoomMultiplier;
        if(cam.orthographicSize < 0.01f) cam.orthographicSize = 1f;
        
        GameObject wrapper = new GameObject("ModelWrapper");
        wrapper.transform.position = bounds.center;
        modelInstance.transform.SetParent(wrapper.transform, true);

        wrapper.transform.position = cam.transform.position + new Vector3(0, 0, 10) + positionFineTune;
        wrapper.transform.rotation = Quaternion.Euler(modelRotation);
        
        _thumbnailCameraRig.SetActive(true);
        yield return new WaitForEndOfFrame();

        RenderTexture rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        
        _thumbnailCameraRig.SetActive(false);

        Destroy(rt);
        Destroy(wrapper);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        callback(sprite);
    }

    private void OnModelSelected(GameObject modelPrefab)
    {
        SelectedModelPrefab = modelPrefab;
        Debug.Log($"✅ Modelo seleccionado: {modelPrefab.name}");
        CloseCatalogPanel();
    }
    
    private GameObject CreateThumbnailCameraRig(int layer)
    {
        GameObject rig = new GameObject("ThumbnailCameraRig_Internal");
        rig.transform.position = new Vector3(5000, 5000, 5000);
        Camera cam = rig.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.clear;
        cam.cullingMask = 1 << layer;
        cam.enabled = true;
        Light light = new GameObject("ThumbnailLight").AddComponent<Light>();
        light.transform.SetParent(rig.transform);
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        rig.SetActive(false);
        return rig;
    }
    
    private void DestroyThumbnailCameraRig()
    {
        if (_thumbnailCameraRig != null) Destroy(_thumbnailCameraRig);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    #endregion
}