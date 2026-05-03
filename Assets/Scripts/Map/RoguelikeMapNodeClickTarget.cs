using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RoguelikeMapNodeClickTarget : MonoBehaviour
{
    private RoguelikeMapGenerator owner;
    private RoguelikeMapGenerator.MapNode node;

    public void Initialize(RoguelikeMapGenerator mapGenerator, RoguelikeMapGenerator.MapNode mapNode)
    {
        owner = mapGenerator;
        node = mapNode;
    }

    public void Click()
    {
        if (owner == null || node == null)
        {
            return;
        }

        owner.HandleNodeClicked(node);
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
