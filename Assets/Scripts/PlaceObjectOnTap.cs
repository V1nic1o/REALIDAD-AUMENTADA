using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.EventSystems;


// Ya no necesitamos ARSubsystems para el raycast de planos
// using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PlaceObjectOnTap : MonoBehaviour
{
    [Header("Object Creation")]
    public GameObject gameObjectToInstantiate;
    public LayerMask placeableLayer;
    
    // --- NUEVA VARIABLE ---
    // La capa donde se generará la malla del entorno
    public LayerMask environmentLayer;

    [Header("Interaction Settings")]
    [Tooltip("La velocidad a la que el objeto cambia de tamaño. Con la fórmula suave, 1.0 es un buen valor inicial.")]
    public float scaleSpeed = 1.0f; 
    public float minScale = 0.1f;
    public float maxScale = 2.0f;
    public float rotationSpeed = 1.0f;
    [Tooltip("Cuánto puede moverse un dedo antes de que deje de considerarse un 'ancla' para rotar.")]
    public float rotationDragThreshold = 2.0f;

    private GameObject selectedObject; // Esta variable siempre guardará al PADRE
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
                // --- LÓGICA DE ARRASTRE ACTUALIZADA ---
                // Ahora arrastramos el objeto sobre la malla del entorno
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, environmentLayer))
                {
                    selectedObject.transform.position = hit.point;
                    // Hacemos que el objeto se adapte a la inclinación de la superficie
                    selectedObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
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
                // --- LÓGICA DE COLOCACIÓN ACTUALIZADA ---
                // Si no hay nada seleccionado, buscamos la malla del entorno para colocar un objeto nuevo
                else if (Physics.Raycast(ray, out RaycastHit placementHit, Mathf.Infinity, environmentLayer))
                {
                    GameObject newObject = Instantiate(gameObjectToInstantiate, placementHit.point, Quaternion.FromToRotation(Vector3.up, placementHit.normal));
                    newObject.AddComponent<ARAnchor>(); // Añadimos el ancla para estabilidad
                }
            }
        }
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
        float scaleFactor = difference * (scaleSpeed / 1000);
        Vector3 newScale = selectedObject.transform.localScale + Vector3.one * scaleFactor;
        newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
        newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
        newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);
        selectedObject.transform.localScale = newScale;
    }

    void HandleRotation(Vector2 anchorPos, Vector2 orbitingPos, Vector2 orbitingPrevPos)
    {
        Vector2 prevDirection = (orbitingPrevPos - anchorPos).normalized;
        Vector2 currentDirection = (orbitingPos - anchorPos).normalized;
        float angle = Vector2.SignedAngle(prevDirection, currentDirection);
        selectedObject.transform.Rotate(Vector3.up, -angle * rotationSpeed);
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
            if (objectRenderer != null)
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