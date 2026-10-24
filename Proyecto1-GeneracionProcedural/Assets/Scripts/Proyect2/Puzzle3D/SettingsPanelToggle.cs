using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de configuracion con icono de apertura y boton X de cierre.
/// - Al pulsar el icono: el icono se oculta y el panel se abre con animacion.
/// - Al pulsar la X del panel: el panel se cierra y el icono vuelve a aparecer.
/// Respeta la escala que el panel ya tiene en el editor.
/// IMPORTANTE: poner este script en el Canvas (o en otro objeto que nunca se desactive),
/// NO en el icono ni en el panel, porque ambos se desactivan.
/// </summary>
public class SettingsPanelToggle : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Panel que contiene sliders y botones (Pivot X=1, Y=0)")]
    public RectTransform panel;
    [Tooltip("Boton del icono que abre el panel (se oculta mientras el panel esta abierto)")]
    public Button openButton;
    [Tooltip("Boton X dentro del panel que lo cierra")]
    public Button closeButton;

    [Header("Animacion")]
    [Tooltip("Activo: el panel crece desde 0 hasta su escala original. Inactivo: aparece con fundido, sin tocar la escala")]
    public bool animateScale = true;
    [Range(0.05f, 1f)] public float openDuration = 0.28f;
    [Range(0.05f, 1f)] public float closeDuration = 0.18f;
    public bool startOpen = false;

    private CanvasGroup canvasGroup;
    private Coroutine running;
    private bool isOpen;

    private Vector3 originalScale = Vector3.one;   // Escala del panel tal como esta en el editor
    private float progress;                         // 0 = cerrado, 1 = abierto

    private void Awake()
    {
        if (panel == null) return;

        // Si el script esta en el icono o en el panel, al desactivarlos se cortaria la animacion
        if ((openButton != null && openButton.gameObject == gameObject) || panel.gameObject == gameObject)
        {
            Debug.LogWarning("SettingsPanelToggle: ponlo en el Canvas, no en el icono ni en el panel.", this);
        }

        // Se guarda la escala ANTES de tocar nada. Si el panel quedo guardado con escala 0, se usa 1.
        originalScale = panel.localScale;
        if (Mathf.Approximately(originalScale.x, 0f) || Mathf.Approximately(originalScale.y, 0f))
            originalScale = Vector3.one;

        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();

        isOpen = startOpen;
        ApplyInstant(isOpen);

        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    // Se puede conectar a On Click si prefieres no usar los campos openButton / closeButton
    public void Open()
    {
        if (panel == null || isOpen) return;
        isOpen = true;
        Play(true);
    }

    public void Close()
    {
        if (panel == null || !isOpen) return;
        isOpen = false;
        Play(false);
    }

    private void Play(bool opening)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(opening));
    }

    private IEnumerator Animate(bool opening)
    {
        // Al abrir, el icono desaparece
        if (opening) SetOpenButtonVisible(false);

        panel.gameObject.SetActive(true);

        // Mientras anima no se puede interactuar con el panel
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float from = progress;                          // Parte desde donde este (sirve si se pulsa a mitad)
        float to = opening ? 1f : 0f;
        float duration = opening ? openDuration : closeDuration;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;     // No depende de Time.timeScale
            float k = Mathf.Clamp01(t);
            float eased = (opening && animateScale) ? EaseOutBack(k) : k;
            Apply(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }

        Apply(to);

        if (opening)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            panel.gameObject.SetActive(false);
            SetOpenButtonVisible(true);                 // Al terminar de cerrar, vuelve el icono
        }
        running = null;
    }

    private void SetOpenButtonVisible(bool visible)
    {
        if (openButton != null) openButton.gameObject.SetActive(visible);
    }

    // p = progreso de la animacion (0 a 1, puede pasarse un poco por el rebote)
    private void Apply(float p)
    {
        p = Mathf.Max(0f, p);
        progress = Mathf.Clamp01(p);

        if (animateScale)
        {
            // La escala va de 0 a la escala original del panel
            panel.localScale = new Vector3(originalScale.x * p, originalScale.y * p, originalScale.z);
        }
        else
        {
            // Sin animar escala: queda fija en el valor del editor
            panel.localScale = originalScale;
        }

        canvasGroup.alpha = Mathf.Clamp01(p);
    }

    private void ApplyInstant(bool open)
    {
        Apply(open ? 1f : 0f);
        canvasGroup.interactable = open;
        canvasGroup.blocksRaycasts = open;
        panel.gameObject.SetActive(open);
        SetOpenButtonVisible(!open);
    }

    // Rebote suave al final de la apertura
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}