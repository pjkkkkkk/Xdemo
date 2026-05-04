using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

[RequireComponent(typeof(Collider2D))]
public sealed class StoreHotspotSceneLoader : MonoBehaviour
{
    [SerializeField] private string m_TargetSceneName = "GoblinShopShelf2DScene";
    [SerializeField] private Renderer m_HoverRenderer;
    [SerializeField] private Transform m_FloatTarget;
    [SerializeField] private Renderer m_TextureSwapRenderer;
    [SerializeField] private Texture m_NormalTexture;
    [SerializeField] private Texture m_HoverTexture;
    [SerializeField] private bool m_RequestMapInkPrintBeforeLoad;
    [SerializeField] private Color m_HoverColor = new Color(1f, 0.72f, 0.22f, 0.24f);
    [SerializeField, Range(0.01f, 0.2f)] private float m_HoverLift = 0.06f;
    [SerializeField, Range(0.1f, 8f)] private float m_PulseSpeed = 3.2f;
    [SerializeField, Range(0.02f, 0.35f)] private float m_PulseAmount = 0.08f;

    private Collider2D m_Collider;
    private Material m_RuntimeHoverMaterial;
    private Material m_RuntimeSwapMaterial;
    private Vector3 m_BaseFloatLocalPosition;
    private bool m_IsHovering;

    private void Awake()
    {
        m_Collider = GetComponent<Collider2D>();

        if (m_FloatTarget == null && m_HoverRenderer != null)
        {
            m_FloatTarget = m_HoverRenderer.transform;
        }

        if (m_FloatTarget != null)
        {
            m_BaseFloatLocalPosition = m_FloatTarget.localPosition;
        }

        if (m_HoverRenderer != null)
        {
            m_RuntimeHoverMaterial = new Material(m_HoverRenderer.sharedMaterial);
            m_RuntimeHoverMaterial.name = m_HoverRenderer.sharedMaterial.name + "_Runtime";
            ConfigureTransparentMaterial(m_RuntimeHoverMaterial);
            m_HoverRenderer.sharedMaterial = m_RuntimeHoverMaterial;
            ApplyHoverAlpha(0f);
        }

        if (m_TextureSwapRenderer != null && m_TextureSwapRenderer.sharedMaterial != null)
        {
            m_RuntimeSwapMaterial = new Material(m_TextureSwapRenderer.sharedMaterial);
            m_RuntimeSwapMaterial.name = m_TextureSwapRenderer.sharedMaterial.name + "_Runtime";
            m_TextureSwapRenderer.sharedMaterial = m_RuntimeSwapMaterial;

            if (m_NormalTexture == null)
            {
                m_NormalTexture = GetMaterialTexture(m_RuntimeSwapMaterial);
            }

            ApplySwapTexture(false);
        }
    }

    private void OnDestroy()
    {
        if (m_RuntimeHoverMaterial != null)
        {
            Destroy(m_RuntimeHoverMaterial);
        }

        if (m_RuntimeSwapMaterial != null)
        {
            Destroy(m_RuntimeSwapMaterial);
        }
    }

    private void Update()
    {
        if (!TryReadPointer(out Vector2 screenPosition, out bool wasPressedThisFrame))
        {
            SetHovering(false);
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            SetHovering(false);
            return;
        }

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
        bool hovering = hit.collider == m_Collider;
        SetHovering(hovering);

        if (hovering && wasPressedThisFrame)
        {
            LoadTargetScene();
        }
    }

    private void SetHovering(bool hovering)
    {
        if (m_IsHovering != hovering)
        {
            m_IsHovering = hovering;
            ApplySwapTexture(m_IsHovering);
        }

        if (m_FloatTarget == null || m_HoverRenderer == null)
        {
            return;
        }

        if (!m_IsHovering)
        {
            m_FloatTarget.localPosition = Vector3.Lerp(m_FloatTarget.localPosition, m_BaseFloatLocalPosition, Time.unscaledDeltaTime * 14f);
            ApplyHoverAlpha(Mathf.MoveTowards(GetHoverAlpha(), 0f, Time.unscaledDeltaTime * 6f));
            return;
        }

        float pulse = (Mathf.Sin(Time.unscaledTime * m_PulseSpeed) + 1f) * 0.5f;
        float alpha = m_HoverColor.a + (pulse * m_PulseAmount);
        Vector3 targetPosition = m_BaseFloatLocalPosition + Vector3.up * (m_HoverLift + pulse * m_HoverLift * 0.45f);

        m_FloatTarget.localPosition = Vector3.Lerp(m_FloatTarget.localPosition, targetPosition, Time.unscaledDeltaTime * 12f);
        ApplyHoverAlpha(alpha);
    }

    private void LoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(m_TargetSceneName))
        {
            Debug.LogWarning("[StoreHotspotSceneLoader] Target scene name is empty.", this);
            return;
        }

        if (!CanLoadScene(m_TargetSceneName))
        {
            Debug.LogWarning($"[StoreHotspotSceneLoader] Scene '{m_TargetSceneName}' is not available in Build Settings.", this);
            return;
        }

        if (m_RequestMapInkPrintBeforeLoad)
        {
            RoguelikeMapLaunchRequest.RequestInkPrintOnNextGameplayScene();
        }

        SceneManager.LoadScene(m_TargetSceneName, LoadSceneMode.Single);
    }

    private void ApplySwapTexture(bool hovering)
    {
        if (m_RuntimeSwapMaterial == null)
        {
            return;
        }

        Texture texture = hovering && m_HoverTexture != null ? m_HoverTexture : m_NormalTexture;
        if (texture == null)
        {
            return;
        }

        SetMaterialTexture(m_RuntimeSwapMaterial, texture);
    }

    private static bool CanLoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return true;
        }

#if UNITY_EDITOR
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (!buildScenes[i].enabled)
            {
                continue;
            }

            string buildScenePath = buildScenes[i].path;
            if (string.Equals(buildScenePath, sceneName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(System.IO.Path.GetFileNameWithoutExtension(buildScenePath), sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
#endif

        return false;
    }

    private bool TryReadPointer(out Vector2 screenPosition, out bool wasPressedThisFrame)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            wasPressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                screenPosition = touch.position.ReadValue();
                wasPressedThisFrame = touch.press.wasPressedThisFrame;
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        wasPressedThisFrame = Input.GetMouseButtonDown(0);
        return true;
#else
        screenPosition = default;
        wasPressedThisFrame = false;
        return false;
#endif
    }

    private float GetHoverAlpha()
    {
        if (m_RuntimeHoverMaterial == null)
        {
            return 0f;
        }

        if (m_RuntimeHoverMaterial.HasProperty("_BaseColor"))
        {
            return m_RuntimeHoverMaterial.GetColor("_BaseColor").a;
        }

        if (m_RuntimeHoverMaterial.HasProperty("_Color"))
        {
            return m_RuntimeHoverMaterial.GetColor("_Color").a;
        }

        return 0f;
    }

    private void ApplyHoverAlpha(float alpha)
    {
        if (m_RuntimeHoverMaterial == null)
        {
            return;
        }

        Color color = m_HoverColor;
        color.a = Mathf.Clamp01(alpha);

        if (m_RuntimeHoverMaterial.HasProperty("_BaseColor"))
        {
            m_RuntimeHoverMaterial.SetColor("_BaseColor", color);
        }

        if (m_RuntimeHoverMaterial.HasProperty("_Color"))
        {
            m_RuntimeHoverMaterial.SetColor("_Color", color);
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Texture GetMaterialTexture(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            return material.GetTexture("_BaseMap");
        }

        if (material.HasProperty("_MainTex"))
        {
            return material.GetTexture("_MainTex");
        }

        return material.mainTexture;
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        material.mainTexture = texture;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }
}
