using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class MapMiniGameBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoguelikeMapGenerator mapGenerator;

    [Header("Mini Game")]
    [SerializeField] private string gomokuSceneName = "GomokuScene";
    [SerializeField] private bool consumeResultOnEnable = true;
    [SerializeField] private bool logProgression = true;

    [Header("Progress")]
    [SerializeField] private string currentNodeId = "N0_0";
    [SerializeField] private string lastClearedNodeId = string.Empty;

    public string CurrentNodeId
    {
        get { return currentNodeId; }
    }

    public string LastClearedNodeId
    {
        get { return lastClearedNodeId; }
    }

    private void OnEnable()
    {
        if (consumeResultOnEnable)
        {
            ConsumePendingMiniGameResult();
        }
    }

    [ContextMenu("Start Gomoku At Current Node")]
    public void StartGomokuAtCurrentNode()
    {
        StartGomokuAtNode(currentNodeId);
    }

    public void StartGomokuAtNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            Debug.LogWarning("MapMiniGameBridge: nodeId is empty.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(gomokuSceneName))
        {
            Debug.LogWarning("MapMiniGameBridge: gomokuSceneName is empty.", this);
            return;
        }

        string returnSceneName = gameObject.scene.IsValid()
            ? gameObject.scene.name
            : SceneManager.GetActiveScene().name;

        MiniGameFlowState.PrepareGomokuRequest(returnSceneName, nodeId);
        SceneManager.LoadScene(gomokuSceneName, LoadSceneMode.Single);
    }

    [ContextMenu("Consume Pending Mini Game Result")]
    public void ConsumePendingMiniGameResult()
    {
        bool won;
        string clearedNodeId;
        if (!MiniGameFlowState.TryConsumeGomokuResult(out won, out clearedNodeId))
        {
            return;
        }

        if (!won)
        {
            if (logProgression)
            {
                Debug.Log("MapMiniGameBridge: mini game failed, map position unchanged.", this);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(clearedNodeId))
        {
            if (logProgression)
            {
                Debug.Log("MapMiniGameBridge: mini game won without a node id, map position unchanged.", this);
            }
            return;
        }

        lastClearedNodeId = clearedNodeId;
        AdvanceToNextNode(clearedNodeId);
    }

    private void AdvanceToNextNode(string clearedNodeId)
    {
        mapGenerator = mapGenerator != null ? mapGenerator : GetComponent<RoguelikeMapGenerator>();

        if (mapGenerator == null || mapGenerator.NodesById == null || mapGenerator.NodesById.Count == 0)
        {
            currentNodeId = clearedNodeId;
            if (logProgression)
            {
                Debug.Log("MapMiniGameBridge: no map data found, current node stays at cleared node " + currentNodeId, this);
            }
            return;
        }

        RoguelikeMapGenerator.MapNode node;
        if (!mapGenerator.NodesById.TryGetValue(clearedNodeId, out node))
        {
            currentNodeId = clearedNodeId;
            if (logProgression)
            {
                Debug.Log("MapMiniGameBridge: cleared node not in map, current node set to " + currentNodeId, this);
            }
            return;
        }

        if (node.nextNodeIds == null || node.nextNodeIds.Count == 0)
        {
            currentNodeId = clearedNodeId;
            if (logProgression)
            {
                Debug.Log("MapMiniGameBridge: cleared node has no next node, progression ended at " + currentNodeId, this);
            }
            return;
        }

        currentNodeId = node.nextNodeIds[0];
        if (logProgression)
        {
            Debug.Log("MapMiniGameBridge: mini game won, moved to next node " + currentNodeId, this);
        }
    }
}
