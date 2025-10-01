using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using System.IO;

public class CatalogManager : MonoBehaviour
{
    [Header("Panel Principal")]
    public GameObject catalogPanel;
    public Button toggleButton;
    public Button closeButton;

       // --- AÑADE LA NUEVA LÍNEA AQUÍ ---
    [Header("Botones Externos a Ocultar")]
    public GameObject openSavePanelButtonGO;

    [Header("Iconos Opcionales")]
    public Sprite iconOpen;
    public Sprite iconClose;

    [Header("Contenido Dinámico del Catálogo")]
    public Transform contentContainer;
    public GameObject subcategoryPrefab;
    public GameObject itemCardPrefab;
    public GameObject thumbnailCameraPrefab;
    
    [Header("Configuración de Thumbnails (Ajuste Fino)")]
    public Vector3 modelRotation = new Vector3(15, -30, 0);
    public Vector3 positionFineTune = new Vector3(0, 0, 0);
    public float scaleMultiplier = 1.0f;
    public float zoomMultiplier = 1.2f;

    private GameObject _thumbnailCameraRig;
    private const string CATALOG_BASE_PATH = "Catalogo";
    public static GameObject SelectedModelPrefab { get; private set; }
    private Coroutine _displayCategoryCoroutine;
    
    private string _thumbnailCachePath;
    private Dictionary<string, Sprite> _runtimeCache = new Dictionary<string, Sprite>();

    public static bool IsOpen { get; private set; }
    
    // --- NUEVA VARIABLE PARA EL TIEMPO DE ESPERA ---
    public static float LastSelectionTime { get; private set; }

    void Start()
    {
        IsOpen = false;
        LastSelectionTime = Time.time; // Inicializamos el tiempo
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

        _thumbnailCachePath = Path.Combine(Application.persistentDataPath, "ThumbnailCache");
        if (!Directory.Exists(_thumbnailCachePath))
        {
            Directory.CreateDirectory(_thumbnailCachePath);
        }
    }

    #region Lógica para Abrir y Cerrar el Panel
    public void OpenCatalogPanel()
    {
        IsOpen = true;
        catalogPanel.SetActive(true);
        if (toggleButton != null) toggleButton.gameObject.SetActive(false);

        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(false);
        DisplayCategory("Vegetacion");
    }

    public void CloseCatalogPanel()
    {
        IsOpen = false;
        catalogPanel.SetActive(false);
        if (toggleButton != null) toggleButton.gameObject.SetActive(true);

        if (openSavePanelButtonGO != null) openSavePanelButtonGO.SetActive(true);
        DestroyThumbnailCameraRig();
    }
    #endregion

    #region Lógica del Catálogo Dinámico
    
    public void ShowVegetacionCategory() { DisplayCategory("Vegetacion"); }
    public void ShowSuperficiesCategory() { DisplayCategory("Superficies"); }
    public void ShowDecoracionCategory() { DisplayCategory("Decoracion"); }

    public void DisplayCategory(string categoryName)
    {
        if (_displayCategoryCoroutine != null)
        {
            StopCoroutine(_displayCategoryCoroutine);
        }
        _displayCategoryCoroutine = StartCoroutine(DisplayCategory_Coroutine(categoryName));
    }

    private IEnumerator DisplayCategory_Coroutine(string categoryName)
    {
        DestroyThumbnailCameraRig();
        int thumbnailLayer = LayerMask.NameToLayer("ThumbnailLayer");
        if (thumbnailLayer == -1) { Debug.LogError("La capa 'ThumbnailLayer' no existe."); yield break; }

        _thumbnailCameraRig = CreateThumbnailCameraRig(thumbnailLayer);
        if (_thumbnailCameraRig == null) yield break;

        foreach (Transform child in contentContainer) Destroy(child.gameObject);

        string indexPath = $"{CATALOG_BASE_PATH}/{categoryName}/{categoryName}_index";
        TextAsset indexFile = Resources.Load<TextAsset>(indexPath);
        if (indexFile == null) { Debug.LogError($"No se encontró el índice en 'Resources/{indexPath}'."); yield break; }
        
        string[] subcategoryNames = indexFile.text.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);

        foreach (string subcategoryName in subcategoryNames)
        {
            GameObject subcategoryHeader = Instantiate(subcategoryPrefab, contentContainer);
            subcategoryHeader.GetComponentInChildren<TextMeshProUGUI>().text = subcategoryName;

            GameObject gridContainer = new GameObject(subcategoryName + " Grid");
            gridContainer.transform.SetParent(contentContainer, false);
            GridLayoutGroup gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
            
            Vector2 cellSize = new Vector2(150, 180);
            Vector2 spacing = new Vector2(15, 15);
            RectOffset padding = new RectOffset(10, 10, 10, 10);
            float parentWidth = (contentContainer as RectTransform).rect.width;
            int columnCount = Mathf.FloorToInt((parentWidth - padding.left - padding.right + spacing.x) / (cellSize.x + spacing.x));
            columnCount = Mathf.Max(1, columnCount);

            gridLayout.padding = padding;
            gridLayout.cellSize = cellSize; 
            gridLayout.spacing = spacing;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columnCount;

            ContentSizeFitter fitter = gridContainer.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            string modelsPath = $"{CATALOG_BASE_PATH}/{categoryName}/{subcategoryName}";
            var models = Resources.LoadAll<GameObject>(modelsPath);

            foreach (var modelPrefab in models)
            {
                GameObject card = Instantiate(itemCardPrefab, gridContainer.transform);
                card.GetComponentInChildren<TextMeshProUGUI>().text = modelPrefab.name;
                Image thumbnailImage = card.transform.Find("ItemImage").GetComponent<Image>();
                
                yield return StartCoroutine(GetOrGenerateThumbnail(modelPrefab, thumbnailLayer, (sprite) => {
                    if (card != null && thumbnailImage != null && sprite != null)
                    {
                        thumbnailImage.sprite = sprite;
                        thumbnailImage.color = Color.white;
                    }
                }));

                card.GetComponent<Button>().onClick.AddListener(() => OnModelSelected(modelPrefab));
            }
        }
    }
    
    private IEnumerator GetOrGenerateThumbnail(GameObject modelPrefab, int layer, System.Action<Sprite> callback)
    {
        string modelName = modelPrefab.name;

        if (_runtimeCache.ContainsKey(modelName)) { callback(_runtimeCache[modelName]); yield break; }

        string cacheFilePath = Path.Combine(_thumbnailCachePath, modelName + ".png");
        if (File.Exists(cacheFilePath))
        {
            byte[] fileData = File.ReadAllBytes(cacheFilePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            _runtimeCache[modelName] = sprite;
            callback(sprite);
            yield break;
        }

        yield return StartCoroutine(GenerateThumbnailAndCache(modelPrefab, layer, cacheFilePath, (sprite) => {
            if (sprite != null) { _runtimeCache[modelName] = sprite; }
            callback(sprite);
        }));
    }
    
    private IEnumerator GenerateThumbnailAndCache(GameObject modelPrefab, int layer, string cacheFilePath, System.Action<Sprite> callback)
    {
        if (_thumbnailCameraRig == null) { callback(null); yield break; }
        
        _thumbnailCameraRig.transform.position = new Vector3(5000, 5000, 5000);
        Camera cam = _thumbnailCameraRig.GetComponentInChildren<Camera>();
        GameObject modelInstance = Instantiate(modelPrefab);
        modelInstance.transform.localScale *= scaleMultiplier;
        SetLayerRecursively(modelInstance, layer);
        yield return null;
        Bounds bounds = new Bounds(modelInstance.transform.position, Vector3.zero);
        bool hasBounds = false;
        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (!r.enabled) continue; 
            if (hasBounds) bounds.Encapsulate(r.bounds);
            else { bounds = r.bounds; hasBounds = true; }
        }

        if (!hasBounds) { Destroy(modelInstance); callback(null); yield break; }
        
        float objectSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float cameraSize = (objectSize / 2f);
        cam.orthographicSize = cameraSize * zoomMultiplier;
        if(cam.orthographicSize < 0.01f) cam.orthographicSize = 1f;
        
        GameObject wrapper = new GameObject("ModelWrapper");
        wrapper.transform.position = bounds.center;
        modelInstance.transform.SetParent(wrapper.transform, true);
        wrapper.transform.position = _thumbnailCameraRig.transform.position + Vector3.forward * 10 + positionFineTune;
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

        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(cacheFilePath, pngData);
        
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        callback(sprite);
    }
    
    // --- FUNCIÓN MODIFICADA ---
    private void OnModelSelected(GameObject modelPrefab)
    {
        SelectedModelPrefab = modelPrefab;
        LastSelectionTime = Time.time; // Guardamos la hora de la selección
        CloseCatalogPanel();
    }
    
    private GameObject CreateThumbnailCameraRig(int layer)
    {
        if (thumbnailCameraPrefab == null) { Debug.LogError("El prefab 'Thumbnail Camera Prefab' no está asignado."); return null; }
        GameObject rig = Instantiate(thumbnailCameraPrefab);
        rig.name = "ThumbnailCameraRig_Instanciado";
        Camera cam = rig.GetComponentInChildren<Camera>();
        if(cam != null) { cam.enabled = false; cam.cullingMask = 1 << layer; }
        return rig;
    }
    
    private void DestroyThumbnailCameraRig()
    {
        if (_thumbnailCameraRig != null) Destroy(_thumbnailCameraRig);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) { SetLayerRecursively(child.gameObject, layer); }
    }
    #endregion
}