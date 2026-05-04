using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RoguelikeMapNodeClickTarget : MonoBehaviour
{
    private RoguelikeMapGenerator owner;
    private RoguelikeMapGenerator.MapNode node;
    private Vector3 baseLocalScale = Vector3.one;
    private bool enableHoverScale = true;
    private bool isHovering;
    private float hoverScaleMultiplier = 1.1f;
    private float hoverScaleLerpSpeed = 14f;

    public void Initialize(RoguelikeMapGenerator mapGenerator, RoguelikeMapGenerator.MapNode mapNode)
    {
        Initialize(mapGenerator, mapNode, true, 1.1f, 14f);
    }

    public void Initialize(
        RoguelikeMapGenerator mapGenerator,
        RoguelikeMapGenerator.MapNode mapNode,
        bool hoverScaleEnabled,
        float hoverScale,
        float hoverLerpSpeed)
    {
        owner = mapGenerator;
        node = mapNode;
        enableHoverScale = hoverScaleEnabled;
        hoverScaleMultiplier = Mathf.Max(1f, hoverScale);
        hoverScaleLerpSpeed = Mathf.Max(1f, hoverLerpSpeed);
        CaptureBaseScale();
    }

    public void Click()
    {
        if (owner == null || node == null)
        {
            return;
        }

        owner.HandleNodeClicked(node);
    }

    public void SetHovering(bool hovering)
    {
        isHovering = enableHoverScale && hovering;
    }

    private void Awake()
    {
        CaptureBaseScale();
    }

    private void OnDisable()
    {
        isHovering = false;
        transform.localScale = baseLocalScale;
    }

    private void Update()
    {
        if (!enableHoverScale)
        {
            return;
        }

        Vector3 targetScale = baseLocalScale * (isHovering ? hoverScaleMultiplier : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * hoverScaleLerpSpeed);
    }

    private void CaptureBaseScale()
    {
        baseLocalScale = transform.localScale;
    }

    private void OnMouseUpAsButton()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Click();
    }
}
