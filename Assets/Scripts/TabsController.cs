using UnityEngine;
using UnityEngine.UI;

public class TabsController : MonoBehaviour
{
    [Header("Botones de categorías")]
    public Button vegetacionButton;
    public Button superficiesButton;
    public Button decoracionButton;

    [Header("Contenido de cada categoría")]
    public GameObject vegetacionContent;
    public GameObject superficiesContent;
    public GameObject decoracionContent;

    private void Start()
    {
        vegetacionButton.onClick.AddListener(() => MostrarCategoria(vegetacionContent));
        superficiesButton.onClick.AddListener(() => MostrarCategoria(superficiesContent));
        decoracionButton.onClick.AddListener(() => MostrarCategoria(decoracionContent));

        // Mostrar solo la primera categoría por defecto
        MostrarCategoria(vegetacionContent);
    }

    void MostrarCategoria(GameObject categoriaActiva)
    {
        Debug.Log("➡️ Mostrando categoría: " + categoriaActiva.name);

        vegetacionContent.SetActive(categoriaActiva == vegetacionContent);
        superficiesContent.SetActive(categoriaActiva == superficiesContent);
        decoracionContent.SetActive(categoriaActiva == decoracionContent);
    }
}