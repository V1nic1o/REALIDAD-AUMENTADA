using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARRaycastManager))]
public class PlaceObjectOnTap : MonoBehaviour
{
    [Header("Object Creation")]
    public LayerMask placeableLayer;
    public LayerMask environmentLayer;

    [Header("Interaction Settings")]
    [Tooltip("Tiempo en segundos a esperar después de seleccionar un objeto en el catálogo antes de poder colocarlo.")]
    [SerializeField] private float placementDelay = 0.5f;
    [Tooltip("Ajusta la sensibilidad del escalado. Valores más bajos son menos sensibles.")]
    public float scaleSpeed = 0.5f; // Valor reducido para menos sensibilidad
    public float minScale = 0.1f;
    public float maxScale = 2.0f;
    [Tooltip("Ajusta la sensibilidad de la rotación. Valores más bajos son menos sensibles. Se recomienda 0.5")]
    public float rotationSpeed = 0.5f; // Valor reducido para menos sensibilidad
    public float rotationDragThreshold = 2.0f;

    private GameObject selectedObject;
    private ARRaycastManager _raycastManager;
    private Material originalMaterial;
    private Material selectedMaterial;
    private bool isDraggingObject = false;

    private float lastTapTime = 0f;
    private readonly float doubleTapTimeThreshold = 0.3f;
    private GameObject lastTappedObject = null;

    private float placementCooldown = 0f;
    private readonly float cooldownDuration = 0.2f;

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
        selectedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        selectedMaterial.color = Color.yellow;
    }

    void Update()
    {
        if (CatalogManager.IsOpen) return;

        if (placementCooldown > 0)
        {
            placementCooldown -= Time.deltaTime;
        }

        if (Input.touchCount == 2 && selectedObject != null)
        {
            isDraggingObject = false;
            HandleTwoFingerGestures();
            return;
        }

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Ray ray = Camera.main.ScreenPointToRay(touch.position);

            if (touch.phase == TouchPhase.Began)
            {
                if (EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;

                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placeableLayer))
                {
                    GameObject objectRoot = hit.transform.parent != null ? hit.transform.parent.gameObject : hit.transform.gameObject;
                    float timeSinceLastTap = Time.time - lastTapTime;

                    if (timeSinceLastTap <= doubleTapTimeThreshold && objectRoot == lastTappedObject)
                    {
                        SelectObject(objectRoot);
                        DeleteSelectedObject();
                        lastTapTime = 0f;
                    }
                    else
                    {
                        SelectObject(objectRoot);
                        isDraggingObject = true;
                        lastTapTime = Time.time;
                        lastTappedObject = objectRoot;
                    }
                    return;
                }
            }

            if (touch.phase == TouchPhase.Moved && isDraggingObject && selectedObject != null)
            {
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, environmentLayer))
                {
                    selectedObject.transform.position = hit.point;
                    selectedObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    // Al arrastrar, también lo mantenemos pegado al suelo
                    GroundObject(selectedObject);
                }
            }

            if (touch.phase == TouchPhase.Ended)
            {
                if (isDraggingObject) { isDraggingObject = false; return; }
                if (placementCooldown > 0f) return;

                if (Physics.Raycast(ray, out RaycastHit hitOnPlaceable, Mathf.Infinity, placeableLayer)) return;

                if (selectedObject != null)
                {
                    DeselectObject();
                }
                else if (CatalogManager.SelectedModelPrefab != null
                         && Time.time - CatalogManager.LastSelectionTime > placementDelay
                         && Physics.Raycast(ray, out RaycastHit placementHit, Mathf.Infinity, environmentLayer))
                {
                    StartCoroutine(PlaceNewObject_Coroutine(placementHit));
                }
            }
        }
    }

    private IEnumerator PlaceNewObject_Coroutine(RaycastHit placementHit)
    {
        GameObject newObject = Instantiate(CatalogManager.SelectedModelPrefab, placementHit.point, Quaternion.identity);
        yield return null;
        
        GroundObject(newObject);

        int placeableLayerIndex = LayerMask.NameToLayer("PlaceableLayer");
        if (placeableLayerIndex != -1)
        {
            SetLayerRecursively(newObject, placeableLayerIndex);
        }
        else
        {
            Debug.LogError("La capa 'PlaceableLayer' no existe.");
        }
        newObject.AddComponent<ARAnchor>();
    }

    Bounds GetObjectBounds(GameObject obj)
    {
        Bounds combinedBounds = new Bounds();
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }
        }
        return combinedBounds;
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform) { if (child != null) SetLayerRecursively(child.gameObject, newLayer); }
    }

    void HandleTwoFingerGestures()
    {
        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        bool isFingerZeroStationary = touchZero.deltaPosition.magnitude < rotationDragThreshold;
        bool isFingerOneStationary = touchOne.deltaPosition.magnitude < rotationDragThreshold;
        bool isFingerZeroMoving = touchZero.deltaPosition.magnitude > rotationDragThreshold;
        bool isFingerOneMoving = touchOne.deltaPosition.magnitude > rotationDragThreshold;

        if (isFingerZeroStationary && isFingerOneMoving) { HandleRotation(touchZero.position, touchOne.position, touchOne.position - touchOne.deltaPosition); }
        else if (isFingerOneStationary && isFingerZeroMoving) { HandleRotation(touchOne.position, touchZero.position, touchZero.position - touchZero.deltaPosition); }
        else if (isFingerZeroMoving && isFingerOneMoving) { HandleScaling(touchZero, touchOne); }
    }

    void HandleScaling(Touch touchZero, Touch touchOne)
    {
        Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
        Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;
        float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
        float currentMagnitude = (touchZero.position - touchOne.position).magnitude;
        float difference = currentMagnitude - prevMagnitude;
        
        // --- CORRECCIÓN DE SENSIBILIDAD ---
        // Se ajusta la fórmula para que el escalado sea más suave
        float scaleFactor = difference * (scaleSpeed / 100);
        
        Vector3 newScale = selectedObject.transform.localScale + Vector3.one * scaleFactor;
        newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
        newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
        newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);
        
        selectedObject.transform.localScale = newScale;

        // --- CORRECCIÓN DE POSICIÓN AL ESCALAR ---
        GroundObject(selectedObject);
    }

    void HandleRotation(Vector2 anchorPos, Vector2 orbitingPos, Vector2 orbitingPrevPos)
    {
        Vector2 prevDirection = (orbitingPrevPos - anchorPos).normalized;
        Vector2 currentDirection = (orbitingPos - anchorPos).normalized;
        float angle = Vector2.SignedAngle(prevDirection, currentDirection);
        
        // La sensibilidad se controla con la variable pública 'rotationSpeed'
        selectedObject.transform.Rotate(Vector3.up, -angle * rotationSpeed);
    }
    
    // --- FUNCIÓN 'GroundObject' MEJORADA ---
    void GroundObject(GameObject obj)
    {
        if (obj == null) return;
        
        // 1. Lanza un rayo desde un poco arriba del objeto hacia abajo para encontrar el suelo
        Ray downRay = new Ray(obj.transform.position + Vector3.up, Vector3.down);
        if (Physics.Raycast(downRay, out RaycastHit groundHit, 10.0f, environmentLayer))
        {
            // El punto exacto del suelo está en groundHit.point

            // 2. Calculamos los límites actuales del objeto
            Bounds bounds = GetObjectBounds(obj);
            if (bounds.size == Vector3.zero) return;

            // 3. Calculamos la diferencia entre el punto más bajo del objeto y el suelo real
            float offset = groundHit.point.y - bounds.min.y;

            // 4. Aplicamos esa diferencia para mover el objeto y que su base toque el suelo
            obj.transform.position += new Vector3(0, offset, 0);
        }
    }

    void SelectObject(GameObject obj)
    {
        if (selectedObject == obj) return;
        if (selectedObject != null) DeselectObject();

        selectedObject = obj;
        
        Renderer objectRenderer = selectedObject.GetComponentInChildren<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            objectRenderer.material = selectedMaterial;
        }
    }

    void DeselectObject()
    {
        if (selectedObject != null)
        {
            Renderer objectRenderer = selectedObject.GetComponentInChildren<Renderer>();
            if (objectRenderer != null && originalMaterial != null)
            {
                objectRenderer.material = originalMaterial;
            }
        }
        selectedObject = null;
        isDraggingObject = false;
    }

    public void DeleteSelectedObject()
    {
        if (selectedObject != null)
        {
            GameObject objectToDelete = selectedObject;
            DeselectObject();
            Destroy(objectToDelete);
            placementCooldown = cooldownDuration;
        }
    } 
}