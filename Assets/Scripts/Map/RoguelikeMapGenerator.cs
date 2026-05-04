using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generates a Slay-the-Spire-like map on a nodeyx[y, x] grid.
/// Default layout is 5 columns horizontally and 9 rows vertically.
/// </summary>
public sealed class RoguelikeMapGenerator : MonoBehaviour
{
    private const string c_DefaultShopSceneName = "GoblinShop2DScene";
    private const string c_DefaultUrpUnlitShader = "Universal Render Pipeline/Unlit";
    private const string c_DefaultUrpLitShader = "Universal Render Pipeline/Lit";
    private const string c_StandardShader = "Standard";
    private const string c_GeneratedAssetsFolder = "Assets/Generated";
    private const string c_GeneratedSceneFolder = "Assets/Generated/OutOfMatchScene";
    private const string c_GeneratedMaterialsFolder = "Assets/Generated/OutOfMatchScene/Materials";
    private static readonly string[] s_DefaultShopScenePool = { c_DefaultShopSceneName };

    public enum MapNodeKind
    {
        Start,
        Question,
        Shop,
        Campfire,
        Elite,
        Boss
    }

    [Serializable]
    public sealed class MapNode
    {
        public int y;
        public int x;
        public string id;
        public MapNodeKind kind;
        public List<string> nextNodeIds = new List<string>();

        [NonSerialized] public List<MapNode> nextNodes = new List<MapNode>();

        public MapNode(int y, int x, MapNodeKind kind)
        {
            this.y = y;
            this.x = x;
            this.kind = kind;
            id = BuildId(y, x);
        }
    }

    private struct IntRange
    {
        public int min;
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public bool IsValid
        {
            get { return min <= max; }
        }
    }

    [Header("Grid")]
    [SerializeField] private int rows = 9;
    [SerializeField] private int columns = 5;
    [SerializeField] private int startColumnMin = 1;
    [SerializeField] private int startColumnMax = 3;
    [SerializeField] private int startPickCount = 4;

    [Header("Random")]
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed = 20260423;

    [Header("View")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool createParchmentOnStart = true;
    [SerializeField] private bool consumeLaunchPrintRequestOnStart;
    [SerializeField] private bool drawView = true;
    [SerializeField] private Vector2 nodeSpacing = new Vector2(0.2f, 0.2f);
    [SerializeField] private float nodeRadius = 0.032f;
    [SerializeField] private float lineWidth = 0.012f;
    [SerializeField] private Transform viewRoot;
    [SerializeField] private Material nodeMaterial;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color nodeColor = new Color(0.28f, 0.12f, 0.035f, 1f);
    [SerializeField] private Color lineColor = new Color(0.22f, 0.09f, 0.025f, 1f);
    [SerializeField] private Color labelColor = new Color(0.18f, 0.07f, 0.02f, 1f);
    [SerializeField] private bool drawLabels;

    [Header("Parchment")]
    [SerializeField] private bool drawParchment = true;
    [SerializeField] private bool fitMapToParchment = true;
    [SerializeField] private Vector2 parchmentSize = new Vector2(1.08f, 1.94f);
    [SerializeField] private float parchmentPadding = 0.15f;
    [SerializeField] private float parchmentEdgeJitter = 0.025f;
    [SerializeField] private int parchmentEdgeSegments = 18;
    [SerializeField] private Color parchmentLightColor = new Color(0.86f, 0.68f, 0.39f, 1f);
    [SerializeField] private Color parchmentDarkColor = new Color(0.46f, 0.25f, 0.095f, 1f);
    [SerializeField] private Color parchmentBurnColor = new Color(0.13f, 0.055f, 0.018f, 1f);

    [Header("Node Icon Atlas")]
    [SerializeField] private bool useNodeIconAtlas = true;
    [SerializeField] private Texture2D nodeIconAtlas;
    [SerializeField] private bool mirrorNodeIconAtlasHorizontally = true;
    [SerializeField] private Rect questionIconRect = new Rect(0f, 0f, 0.2f, 1f);
    [SerializeField] private Rect shopIconRect = new Rect(0.2f, 0f, 0.2f, 1f);
    [SerializeField] private Rect campfireIconRect = new Rect(0.4f, 0f, 0.2f, 1f);
    [SerializeField] private Rect eliteIconRect = new Rect(0.6f, 0f, 0.2f, 1f);
    [SerializeField] private Rect bossIconRect = new Rect(0.8f, 0f, 0.2f, 1f);

    [Header("Node Click")]
    [SerializeField] private bool enableNodeClick = true;
    [SerializeField, Range(0.3f, 1.2f)] private float nodeClickBoundsScale = 0.72f;
    [SerializeField] private string[] questionNodeScenePool = new string[0];
    [SerializeField] private string[] shopNodeScenePool = { c_DefaultShopSceneName };
    [SerializeField] private string[] campfireNodeScenePool = new string[0];
    [SerializeField] private string[] eliteNodeScenePool = new string[0];
    [SerializeField] private string[] bossNodeScenePool = new string[0];

    [Header("Player Piece")]
    [SerializeField] private string playerPieceName = "wizard";
    [SerializeField] private bool movePlayerPieceOnValidNodeClick = true;
    [SerializeField] private bool loadSceneAfterPlayerPieceMove = true;
    [SerializeField, Range(0f, 0.03f)] private float playerPieceSurfaceOffset = 0f;
    [SerializeField, Range(0.04f, 0.3f)] private float playerPieceHopDistance = 0.12f;
    [SerializeField, Range(0.02f, 0.24f)] private float playerPieceLiftHeight = 0.075f;
    [SerializeField, Range(0.05f, 0.5f)] private float playerPieceHopSeconds = 0.16f;
    [SerializeField, Range(0f, 0.16f)] private float playerPieceLandingPauseSeconds = 0.035f;

    [Header("Node Hover")]
    [SerializeField] private bool enableNodeHoverScale = true;
    [SerializeField, Range(1f, 1.25f)] private float nodeHoverScaleMultiplier = 1.1f;
    [SerializeField, Range(4f, 30f)] private float nodeHoverScaleLerpSpeed = 14f;

    [Header("Debug")]
    [SerializeField] private bool logGeneratedData = true;

    // Required public grid. Empty slots stay null.
    public MapNode[,] nodeyx;

    public IReadOnlyDictionary<string, MapNode> NodesById
    {
        get { return nodesById; }
    }

    public IReadOnlyCollection<string> ConnectionKeys
    {
        get { return connectionKeys; }
    }

    private readonly Dictionary<string, MapNode> nodesById = new Dictionary<string, MapNode>();
    private readonly HashSet<string> connectionKeys = new HashSet<string>();
    private System.Random random;
    private int lastHandledNodeClickFrame = -1;
    private MapNode currentPlayerNode;
    private Transform playerPiece;
    private Coroutine playerPieceMoveRoutine;
    private bool isPlayerPieceMoving;
    private RoguelikeMapNodeClickTarget hoveredNodeTarget;
    private Material generatedNodeMaterial;
    private Material generatedLineMaterial;
    private Material generatedParchmentMaterial;
    private Material generatedIconAtlasMaterial;
    private Texture2D generatedParchmentTexture;

    public void SetupAsTabletopParchment(Vector3 worldPosition, Quaternion worldRotation)
    {
        SetupAsTabletopParchment(worldPosition, worldRotation, new Vector2(1.08f, 1.94f), true);
    }

    public void SetupAsTabletopParchment(Vector3 worldPosition, Quaternion worldRotation, Vector2 mapSize, bool consumeLaunchPrintRequest)
    {
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        ApplyTabletopParchmentSettings(mapSize, consumeLaunchPrintRequest);
    }

    public void SetupAsLocalTabletopParchment(Vector3 localPosition, Quaternion localRotation, Vector2 mapSize, bool consumeLaunchPrintRequest)
    {
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        ApplyTabletopParchmentSettings(mapSize, consumeLaunchPrintRequest);
    }

    public void SetStartBuildEnabled(bool enabled)
    {
        generateOnStart = enabled;
        createParchmentOnStart = enabled;
    }

    public void SetNodeIconAtlas(Texture2D atlas)
    {
        nodeIconAtlas = atlas;
    }

    public void SetNodeScenePools(
        string[] questionScenes,
        string[] shopScenes,
        string[] campfireScenes,
        string[] eliteScenes,
        string[] bossScenes)
    {
        questionNodeScenePool = CopyScenePool(questionScenes);
        shopNodeScenePool = CopyScenePool(shopScenes);
        campfireNodeScenePool = CopyScenePool(campfireScenes);
        eliteNodeScenePool = CopyScenePool(eliteScenes);
        bossNodeScenePool = CopyScenePool(bossScenes);
    }

    public bool TryGetStartNodeWorldPosition(out Vector3 worldPosition)
    {
        MapNode startNode = FindStartNode();
        return TryGetNodeWorldPosition(startNode, out worldPosition);
    }

    public bool TryGetNodeWorldPosition(MapNode node, out Vector3 worldPosition)
    {
        if (node == null)
        {
            worldPosition = Vector3.zero;
            return false;
        }

        worldPosition = transform.TransformPoint(GridToLocalPosition(node.y, node.x, 0.04f));
        return true;
    }

    public void SetPlayerPiece(Transform piece)
    {
        playerPiece = piece;
        if (currentPlayerNode == null)
        {
            currentPlayerNode = FindStartNode();
        }

        SnapPlayerPieceToCurrentNode();
    }

    public void HandleNodeClicked(MapNode node)
    {
        if (!enableNodeClick || node == null || isPlayerPieceMoving)
        {
            return;
        }

        if (lastHandledNodeClickFrame == Time.frameCount)
        {
            return;
        }

        if (currentPlayerNode == null)
        {
            currentPlayerNode = FindStartNode();
        }

        if (!IsValidNextPlayerNode(node))
        {
            return;
        }

        lastHandledNodeClickFrame = Time.frameCount;

        if (movePlayerPieceOnValidNodeClick && ResolvePlayerPiece() != null)
        {
            if (playerPieceMoveRoutine != null)
            {
                StopCoroutine(playerPieceMoveRoutine);
            }

            playerPieceMoveRoutine = StartCoroutine(MovePlayerPieceToNodeRoutine(node));
            return;
        }

        currentPlayerNode = node;
        RecordPlayerProgress(node);
        TryLoadSceneForNode(node);
    }

    private bool IsValidNextPlayerNode(MapNode node)
    {
        if (currentPlayerNode == null || node == null || node.y != currentPlayerNode.y + 1)
        {
            return false;
        }

        return currentPlayerNode.nextNodes != null && currentPlayerNode.nextNodes.Contains(node);
    }

    private void RestorePlayerProgress()
    {
        MapNode startNode = FindStartNode();
        currentPlayerNode = startNode;

        string savedNodeId = RoguelikeMapLaunchRequest.GetCurrentMapNodeId();
        if (!string.IsNullOrWhiteSpace(savedNodeId) && nodesById.TryGetValue(savedNodeId, out MapNode savedNode))
        {
            currentPlayerNode = savedNode;
        }
        else if (!string.IsNullOrWhiteSpace(savedNodeId))
        {
            RoguelikeMapLaunchRequest.ClearMapNodeHistory();
        }

        RecordPlayerProgress(currentPlayerNode);
    }

    private static void RecordPlayerProgress(MapNode node)
    {
        if (node == null)
        {
            return;
        }

        RoguelikeMapLaunchRequest.RecordVisitedMapNode(node.id);
    }

    private void TryLoadSceneForNode(MapNode node)
    {
        if (!loadSceneAfterPlayerPieceMove || node == null)
        {
            return;
        }

        string sceneName = PickSceneFromPool(GetScenePool(node.kind));
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[RoguelikeMapGenerator] Node '{node.id}' ({node.kind}) was clicked, but its scene pool is empty.", this);
            return;
        }

        if (!CanLoadScene(sceneName))
        {
            Debug.LogWarning($"[RoguelikeMapGenerator] Node '{node.id}' selected scene '{sceneName}', but it is not available in Build Settings.", this);
            return;
        }

        Debug.Log($"[RoguelikeMapGenerator] Loading scene '{sceneName}' from clicked node '{node.id}' ({node.kind}).", this);
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private IEnumerator MovePlayerPieceToNodeRoutine(MapNode targetNode)
    {
        isPlayerPieceMoving = true;

        Transform piece = ResolvePlayerPiece();
        Vector3 targetPosition;
        if (piece != null && TryGetPlayerPieceTargetPosition(piece, targetNode, out targetPosition))
        {
            yield return MovePlayerPieceInHops(piece, targetPosition);
        }

        currentPlayerNode = targetNode;
        RecordPlayerProgress(targetNode);
        isPlayerPieceMoving = false;
        playerPieceMoveRoutine = null;
        TryLoadSceneForNode(targetNode);
    }

    private IEnumerator MovePlayerPieceInHops(Transform piece, Vector3 targetPosition)
    {
        Vector3 startPosition = piece.position;
        Vector3 flatDelta = targetPosition - startPosition;
        flatDelta.y = 0f;

        int hopCount = Mathf.Max(1, Mathf.CeilToInt(flatDelta.magnitude / Mathf.Max(0.01f, playerPieceHopDistance)));
        Vector3 hopStart = startPosition;
        for (int i = 1; i <= hopCount; i++)
        {
            Vector3 hopEnd = Vector3.Lerp(startPosition, targetPosition, i / (float)hopCount);
            yield return MovePlayerPieceHop(piece, hopStart, hopEnd);
            hopStart = hopEnd;

            if (playerPieceLandingPauseSeconds > 0f && i < hopCount)
            {
                yield return new WaitForSeconds(playerPieceLandingPauseSeconds);
            }
        }

        piece.position = targetPosition;
    }

    private IEnumerator MovePlayerPieceHop(Transform piece, Vector3 startPosition, Vector3 endPosition)
    {
        float seconds = Mathf.Max(0.01f, playerPieceHopSeconds);
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float smoothT = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, smoothT);
            position.y += Mathf.Sin(smoothT * Mathf.PI) * playerPieceLiftHeight;
            piece.position = position;
            yield return null;
        }

        piece.position = endPosition;
    }

    private bool TryGetPlayerPieceTargetPosition(Transform piece, MapNode node, out Vector3 targetPosition)
    {
        Vector3 nodeWorldPosition;
        if (!TryGetNodeWorldPosition(node, out nodeWorldPosition))
        {
            targetPosition = Vector3.zero;
            return false;
        }

        Vector3 targetBottomCenter = new Vector3(
            nodeWorldPosition.x,
            nodeWorldPosition.y + playerPieceSurfaceOffset,
            nodeWorldPosition.z);

        Bounds bounds;
        if (TryGetRendererBounds(piece, out bounds))
        {
            Vector3 currentBottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            targetPosition = piece.position + (targetBottomCenter - currentBottomCenter);
            return true;
        }

        targetPosition = targetBottomCenter;
        return true;
    }

    private void SnapPlayerPieceToCurrentNode()
    {
        Transform piece = ResolvePlayerPiece();
        Vector3 targetPosition;
        if (piece == null || !TryGetPlayerPieceTargetPosition(piece, currentPlayerNode, out targetPosition))
        {
            return;
        }

        piece.position = targetPosition;
    }

    private Transform ResolvePlayerPiece()
    {
        if (playerPiece != null)
        {
            return playerPiece;
        }

        if (string.IsNullOrWhiteSpace(playerPieceName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.hideFlags != HideFlags.None || !candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(candidate.name, playerPieceName, StringComparison.OrdinalIgnoreCase))
            {
                playerPiece = candidate;
                return playerPiece;
            }
        }

        return null;
    }

    private MapNode FindStartNode()
    {
        foreach (KeyValuePair<string, MapNode> pair in nodesById)
        {
            MapNode node = pair.Value;
            if (node != null && node.kind == MapNodeKind.Start)
            {
                return node;
            }
        }

        return null;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root != null ? root.position : Vector3.zero, Vector3.zero);
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void ApplyTabletopParchmentSettings(Vector2 mapSize, bool consumeLaunchPrintRequest)
    {
        transform.localScale = Vector3.one;
        rows = 11;
        columns = 7;
        startColumnMin = 2;
        startColumnMax = 4;
        startPickCount = 4;
        generateOnStart = true;
        createParchmentOnStart = true;
        consumeLaunchPrintRequestOnStart = consumeLaunchPrintRequest;
        drawView = true;
        drawParchment = true;
        fitMapToParchment = true;
        drawLabels = false;
        logGeneratedData = false;
        nodeColor = new Color(0.12f, 0.055f, 0.018f, 1f);
        lineColor = new Color(0.13f, 0.06f, 0.02f, 1f);
        labelColor = new Color(0.12f, 0.055f, 0.018f, 1f);
        parchmentSize = new Vector2(Mathf.Max(0.2f, mapSize.x), Mathf.Max(0.2f, mapSize.y));
        parchmentPadding = Mathf.Clamp(Mathf.Min(parchmentSize.x, parchmentSize.y) * 0.14f, 0.1f, 0.22f);
        parchmentEdgeJitter = Mathf.Clamp(Mathf.Min(parchmentSize.x, parchmentSize.y) * 0.018f, 0.006f, 0.018f);
        parchmentLightColor = new Color(0.9f, 0.78f, 0.52f, 1f);
        parchmentDarkColor = new Color(0.62f, 0.43f, 0.22f, 1f);
        parchmentBurnColor = new Color(0.22f, 0.11f, 0.035f, 1f);
    }

    private void Start()
    {
        if (generateOnStart && ShouldPrintInkOnStart())
        {
            GenerateMap();
            return;
        }

        if (createParchmentOnStart)
        {
            BuildBlankParchment();
        }
    }

    private void Update()
    {
        if (!enableNodeClick || nodesById.Count == 0)
        {
            SetHoveredNode(null);
            return;
        }

        HandleNodeHover();

        Vector2 screenPosition;
        if (!TryConsumePrimaryClick(out screenPosition))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        RoguelikeMapNodeClickTarget clickTarget = hit.collider.GetComponentInParent<RoguelikeMapNodeClickTarget>();
        if (clickTarget != null)
        {
            clickTarget.Click();
        }
    }

    private void HandleNodeHover()
    {
        if (!enableNodeHoverScale)
        {
            SetHoveredNode(null);
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetHoveredNode(null);
            return;
        }

        Vector2 screenPosition;
        if (!TryReadPointerPosition(out screenPosition))
        {
            SetHoveredNode(null);
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            SetHoveredNode(null);
            return;
        }

        Ray ray = camera.ScreenPointToRay(screenPosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            SetHoveredNode(null);
            return;
        }

        SetHoveredNode(hit.collider.GetComponentInParent<RoguelikeMapNodeClickTarget>());
    }

    private void SetHoveredNode(RoguelikeMapNodeClickTarget target)
    {
        if (hoveredNodeTarget == target)
        {
            return;
        }

        if (hoveredNodeTarget != null)
        {
            hoveredNodeTarget.SetHovering(false);
        }

        hoveredNodeTarget = target;
        if (hoveredNodeTarget != null)
        {
            hoveredNodeTarget.SetHovering(true);
        }
    }

    private bool ShouldPrintInkOnStart()
    {
        if (!consumeLaunchPrintRequestOnStart)
        {
            return true;
        }

        return RoguelikeMapLaunchRequest.ConsumeInkPrintOnNextGameplayScene();
    }

    private bool TryConsumePrimaryClick(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#endif

        try
        {
            if (Input.GetMouseButtonUp(0))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            // Project may be configured for the new Input System only.
        }

        return false;
    }

    private bool TryReadPointerPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

        try
        {
            screenPosition = Input.mousePosition;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private string[] GetScenePool(MapNodeKind kind)
    {
        switch (kind)
        {
            case MapNodeKind.Shop:
                return HasUsableScene(shopNodeScenePool) ? shopNodeScenePool : s_DefaultShopScenePool;
            case MapNodeKind.Campfire:
                return campfireNodeScenePool;
            case MapNodeKind.Elite:
                return eliteNodeScenePool;
            case MapNodeKind.Boss:
                return bossNodeScenePool;
            case MapNodeKind.Question:
                return questionNodeScenePool;
            default:
                return null;
        }
    }

    private static bool HasUsableScene(string[] scenePool)
    {
        if (scenePool == null)
        {
            return false;
        }

        for (int i = 0; i < scenePool.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(scenePool[i]))
            {
                return true;
            }
        }

        return false;
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
            if (string.Equals(buildScenePath, sceneName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(System.IO.Path.GetFileNameWithoutExtension(buildScenePath), sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
#endif

        return false;
    }

    private static string PickSceneFromPool(string[] scenePool)
    {
        if (scenePool == null || scenePool.Length == 0)
        {
            return null;
        }

        List<string> candidates = new List<string>(scenePool.Length);
        for (int i = 0; i < scenePool.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(scenePool[i]))
            {
                candidates.Add(scenePool[i].Trim());
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private static string[] CopyScenePool(string[] source)
    {
        if (source == null || source.Length == 0)
        {
            return new string[0];
        }

        string[] copy = new string[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    [ContextMenu("Build Blank Parchment")]
    public void BuildBlankParchment()
    {
        NormalizeSettings();
        nodeyx = new MapNode[rows, columns];
        nodesById.Clear();
        connectionKeys.Clear();
        currentPlayerNode = null;
        SetHoveredNode(null);

        if (drawView)
        {
            RebuildParchmentOnlyView();
        }
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        NormalizeSettings();
        seed = RoguelikeMapLaunchRequest.EnsureMapRunSeed(useRandomSeed, seed);
        random = new System.Random(seed);

        nodeyx = new MapNode[rows, columns];
        nodesById.Clear();
        connectionKeys.Clear();

        List<List<MapNode>> rowNodes = BuildConvergingMapRows();
        AssignShopNodes(rowNodes);
        ConnectConvergingMapRows(rowNodes);
        RestorePlayerProgress();

        if (drawView)
        {
            RebuildView();
        }

        SnapPlayerPieceToCurrentNode();

        if (logGeneratedData)
        {
            Debug.Log(BuildDebugLog());
        }
    }

    private List<List<MapNode>> BuildConvergingMapRows()
    {
        List<List<MapNode>> rowNodes = new List<List<MapNode>>(rows);
        for (int i = 0; i < rows; i++)
        {
            rowNodes.Add(new List<MapNode>());
        }

        int centerX = columns / 2;
        int convergenceRow = GetConvergenceRow();
        int campfireRow = rows - 2;

        rowNodes[0].Add(GetOrCreateNode(0, centerX, MapNodeKind.Start));
        rowNodes[1].AddRange(CreateRowNodes(1, PickSpreadColumns(3, false), MapNodeKind.Question));

        for (int y = 2; y < convergenceRow; y++)
        {
            int count = PickRouteRowCount(y, convergenceRow);
            rowNodes[y].AddRange(CreateRowNodes(y, PickSpreadColumns(count, true), MapNodeKind.Question));
        }

        rowNodes[convergenceRow].Add(GetOrCreateNode(convergenceRow, centerX, MapNodeKind.Elite));

        for (int y = convergenceRow + 1; y < campfireRow; y++)
        {
            int count = PickRouteRowCount(y, convergenceRow);
            rowNodes[y].AddRange(CreateRowNodes(y, PickSpreadColumns(count, true), MapNodeKind.Question));
        }

        rowNodes[campfireRow].AddRange(CreateRowNodes(campfireRow, PickSpreadColumns(3, false), MapNodeKind.Campfire));
        rowNodes[rows - 1].Add(GetOrCreateNode(rows - 1, centerX, MapNodeKind.Boss));

        return rowNodes;
    }

    private int GetConvergenceRow()
    {
        return Mathf.Clamp(rows / 2, 3, rows - 4);
    }

    private int PickRouteRowCount(int row, int convergenceRow)
    {
        if (columns < 7 || Mathf.Abs(row - convergenceRow) <= 1)
        {
            return 3;
        }

        return random.NextDouble() < 0.35d ? 4 : 3;
    }

    private List<MapNode> CreateRowNodes(int row, List<int> xColumns, MapNodeKind kind)
    {
        List<MapNode> result = new List<MapNode>(xColumns.Count);
        for (int i = 0; i < xColumns.Count; i++)
        {
            result.Add(GetOrCreateNode(row, xColumns[i], kind));
        }

        result.Sort(delegate(MapNode a, MapNode b)
        {
            return a.x.CompareTo(b.x);
        });
        return result;
    }

    private List<int> PickSpreadColumns(int count, bool allowJitter)
    {
        int safeCount = Mathf.Clamp(count, 1, Mathf.Max(1, columns - 2));
        int minX = 1;
        int maxX = columns - 2;
        List<int> result = new List<int>(safeCount);

        for (int i = 0; i < safeCount; i++)
        {
            float t = safeCount == 1 ? 0.5f : i / (float)(safeCount - 1);
            int x = Mathf.RoundToInt(Mathf.Lerp(minX, maxX, t));

            if (allowJitter && random.NextDouble() < 0.45d)
            {
                x += random.Next(-1, 2);
            }

            x = Mathf.Clamp(x, minX, maxX);
            AddNearestUnusedColumn(result, x, minX, maxX);
        }

        result.Sort();
        return result;
    }

    private static void AddNearestUnusedColumn(List<int> columnsList, int preferred, int minX, int maxX)
    {
        if (!columnsList.Contains(preferred))
        {
            columnsList.Add(preferred);
            return;
        }

        for (int distance = 1; distance <= maxX - minX; distance++)
        {
            int left = preferred - distance;
            if (left >= minX && !columnsList.Contains(left))
            {
                columnsList.Add(left);
                return;
            }

            int right = preferred + distance;
            if (right <= maxX && !columnsList.Contains(right))
            {
                columnsList.Add(right);
                return;
            }
        }
    }

    private void AssignShopNodes(List<List<MapNode>> rowNodes)
    {
        List<MapNode> candidates = new List<MapNode>();
        int convergenceRow = GetConvergenceRow();
        int campfireRow = rows - 2;

        for (int y = 2; y < campfireRow; y++)
        {
            if (y == convergenceRow)
            {
                continue;
            }

            for (int i = 0; i < rowNodes[y].Count; i++)
            {
                if (rowNodes[y][i].kind == MapNodeKind.Question)
                {
                    candidates.Add(rowNodes[y][i]);
                }
            }
        }

        int shopCount = Mathf.Min(candidates.Count, random.Next(2, 4));
        for (int i = 0; i < shopCount; i++)
        {
            int index = random.Next(i, candidates.Count);
            MapNode temporary = candidates[i];
            candidates[i] = candidates[index];
            candidates[index] = temporary;
            candidates[i].kind = MapNodeKind.Shop;
        }
    }

    private void ConnectConvergingMapRows(List<List<MapNode>> rowNodes)
    {
        for (int y = 0; y < rowNodes.Count - 1; y++)
        {
            ConnectRows(rowNodes[y], rowNodes[y + 1]);
        }
    }

    private void ConnectRows(List<MapNode> fromNodes, List<MapNode> toNodes)
    {
        if (fromNodes.Count == 0 || toNodes.Count == 0)
        {
            return;
        }

        if (fromNodes.Count == 1)
        {
            for (int i = 0; i < toNodes.Count; i++)
            {
                AddUniqueConnection(fromNodes[0], toNodes[i]);
            }
            return;
        }

        if (toNodes.Count == 1)
        {
            for (int i = 0; i < fromNodes.Count; i++)
            {
                AddUniqueConnection(fromNodes[i], toNodes[0]);
            }
            return;
        }

        List<Vector2Int> plannedEdges = new List<Vector2Int>();
        int[] baseTargetIndices = new int[fromNodes.Count];
        for (int i = 0; i < fromNodes.Count; i++)
        {
            int targetIndex = Mathf.RoundToInt(i * (toNodes.Count - 1) / (float)(fromNodes.Count - 1));
            baseTargetIndices[i] = targetIndex;
            TryAddNonCrossingConnection(plannedEdges, fromNodes, toNodes, i, targetIndex);
        }

        for (int i = 0; i < toNodes.Count; i++)
        {
            int parentIndex = Mathf.RoundToInt(i * (fromNodes.Count - 1) / (float)(toNodes.Count - 1));
            TryAddNonCrossingConnection(plannedEdges, fromNodes, toNodes, parentIndex, i);
        }

        for (int i = 0; i < fromNodes.Count; i++)
        {
            if (random.NextDouble() >= 0.32d)
            {
                continue;
            }

            int direction = random.NextDouble() < 0.5d ? -1 : 1;
            int branchIndex = baseTargetIndices[i] + direction;
            if (branchIndex < 0 || branchIndex >= toNodes.Count)
            {
                branchIndex = baseTargetIndices[i] - direction;
            }

            TryAddNonCrossingConnection(plannedEdges, fromNodes, toNodes, i, branchIndex);
        }
    }

    private bool TryAddNonCrossingConnection(
        List<Vector2Int> plannedEdges,
        List<MapNode> fromNodes,
        List<MapNode> toNodes,
        int fromIndex,
        int targetIndex)
    {
        if (fromIndex < 0 || fromIndex >= fromNodes.Count || targetIndex < 0 || targetIndex >= toNodes.Count)
        {
            return false;
        }

        for (int i = 0; i < plannedEdges.Count; i++)
        {
            Vector2Int existing = plannedEdges[i];
            if (existing.x == fromIndex && existing.y == targetIndex)
            {
                return false;
            }

            if (fromIndex < existing.x && targetIndex > existing.y)
            {
                return false;
            }

            if (fromIndex > existing.x && targetIndex < existing.y)
            {
                return false;
            }
        }

        plannedEdges.Add(new Vector2Int(fromIndex, targetIndex));
        AddUniqueConnection(fromNodes[fromIndex], toNodes[targetIndex]);
        return true;
    }

    private void NormalizeSettings()
    {
        rows = Mathf.Max(7, rows);
        columns = Mathf.Max(5, columns);
        startPickCount = Mathf.Max(1, startPickCount);
        startColumnMin = Mathf.Clamp(startColumnMin, 0, columns - 1);
        startColumnMax = Mathf.Clamp(startColumnMax, startColumnMin, columns - 1);

        if (fitMapToParchment)
        {
            float mapWidth = Mathf.Max(0.1f, parchmentSize.x - (parchmentPadding * 2f));
            float mapHeight = Mathf.Max(0.1f, parchmentSize.y - (parchmentPadding * 2f));
            nodeSpacing = new Vector2(mapWidth / Mathf.Max(1, columns - 1), mapHeight / Mathf.Max(1, rows - 1));
            float inkScale = Mathf.Min(nodeSpacing.x, nodeSpacing.y);
            nodeRadius = inkScale * 0.16f;
            lineWidth = inkScale * 0.06f;
        }
    }

    private List<int> PickStartColumns()
    {
        List<int> result = new List<int>(startPickCount);
        int previous = int.MinValue;
        int guard = 0;

        while (result.Count < startPickCount && guard < 1000)
        {
            guard++;
            int x = random.Next(startColumnMin, startColumnMax + 1);

            // Consecutive duplicate picks are ignored and do not consume one of the four picks.
            if (result.Count > 0 && x == previous)
            {
                continue;
            }

            result.Add(x);
            previous = x;
        }

        return result;
    }

    private static string BuildId(int y, int x)
    {
        return "N" + y + "_" + x;
    }

    private MapNode GetOrCreateNode(int y, int x)
    {
        return GetOrCreateNode(y, x, MapNodeKind.Question);
    }

    private MapNode GetOrCreateNode(int y, int x, MapNodeKind kind)
    {
        x = Mathf.Clamp(x, 0, columns - 1);
        string id = BuildId(y, x);

        MapNode node;
        if (nodesById.TryGetValue(id, out node))
        {
            node.kind = kind;
            return node;
        }

        node = new MapNode(y, x, kind);
        nodesById.Add(id, node);
        nodeyx[y, x] = node;
        return node;
    }

    private List<MapNode> GetUniqueSortedParents(List<MapNode> activeNodes, int row)
    {
        Dictionary<string, MapNode> unique = new Dictionary<string, MapNode>();
        for (int i = 0; i < activeNodes.Count; i++)
        {
            MapNode node = activeNodes[i];
            if (node.y == row && !unique.ContainsKey(node.id))
            {
                unique.Add(node.id, node);
            }
        }

        List<MapNode> parents = new List<MapNode>(unique.Values);
        parents.Sort(delegate(MapNode a, MapNode b)
        {
            return a.x.CompareTo(b.x);
        });
        return parents;
    }

    private IntRange GetSafeTargetRange(
        List<MapNode> rowParents,
        int parentIndex,
        Dictionary<string, IntRange> chosenOutgoingRanges)
    {
        MapNode parent = rowParents[parentIndex];

        // Physical step range: dx can only be -1, 0, or 1.
        IntRange range = new IntRange(
            Mathf.Max(0, parent.x - 1),
            Mathf.Min(columns - 1, parent.x + 1));

        int leftOutgoingMax;
        if (TryGetLeftNeighborOutgoingMax(rowParents, parentIndex, chosenOutgoingRanges, out leftOutgoingMax))
        {
            range.min = Mathf.Max(range.min, leftOutgoingMax);
        }

        int rightOutgoingMin;
        if (TryGetRightNeighborOutgoingMin(rowParents, parentIndex, chosenOutgoingRanges, out rightOutgoingMin))
        {
            range.max = Mathf.Min(range.max, rightOutgoingMin);
        }

        if (range.IsValid)
        {
            return range;
        }

        // A defensive fallback. With left-to-right generation this should not happen.
        return new IntRange(
            Mathf.Max(0, parent.x - 1),
            Mathf.Min(columns - 1, parent.x + 1));
    }

    private static bool TryGetLeftNeighborOutgoingMax(
        List<MapNode> rowParents,
        int parentIndex,
        Dictionary<string, IntRange> chosenOutgoingRanges,
        out int outgoingMax)
    {
        for (int i = parentIndex - 1; i >= 0; i--)
        {
            IntRange range;
            if (chosenOutgoingRanges.TryGetValue(rowParents[i].id, out range))
            {
                outgoingMax = range.max;
                return true;
            }
        }

        outgoingMax = 0;
        return false;
    }

    private static bool TryGetRightNeighborOutgoingMin(
        List<MapNode> rowParents,
        int parentIndex,
        Dictionary<string, IntRange> chosenOutgoingRanges,
        out int outgoingMin)
    {
        for (int i = parentIndex + 1; i < rowParents.Count; i++)
        {
            IntRange range;
            if (chosenOutgoingRanges.TryGetValue(rowParents[i].id, out range))
            {
                outgoingMin = range.min;
                return true;
            }
        }

        outgoingMin = 0;
        return false;
    }

    private bool AddUniqueConnection(MapNode from, MapNode to)
    {
        string key = from.id + ">" + to.id;

        // String-key interception removes duplicate edges while still allowing convergence.
        if (!connectionKeys.Add(key))
        {
            return false;
        }

        from.nextNodeIds.Add(to.id);
        from.nextNodes.Add(to);
        return true;
    }

    private void RebuildView()
    {
        Transform root = EnsureViewRoot();
        ClearChildren(root);

        if (drawParchment)
        {
            CreateParchmentView(root);
        }

        foreach (KeyValuePair<string, MapNode> pair in nodesById)
        {
            MapNode from = pair.Value;
            for (int i = 0; i < from.nextNodes.Count; i++)
            {
                CreateLineView(root, from, from.nextNodes[i]);
            }
        }

        foreach (KeyValuePair<string, MapNode> pair in nodesById)
        {
            CreateNodeView(root, pair.Value);
        }
    }

    private void RebuildParchmentOnlyView()
    {
        Transform root = EnsureViewRoot();
        ClearChildren(root);

        if (drawParchment)
        {
            CreateParchmentView(root);
        }
    }

    private Transform EnsureViewRoot()
    {
        if (viewRoot != null)
        {
            return viewRoot;
        }

        Transform existing = transform.Find("Generated Map View");
        if (existing != null)
        {
            viewRoot = existing;
            return viewRoot;
        }

        GameObject root = new GameObject("Generated Map View");
        root.transform.SetParent(transform, false);
        viewRoot = root.transform;
        return viewRoot;
    }

    private void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private GameObject CreateNodeView(Transform root, MapNode node)
    {
        GameObject nodeObject = new GameObject(node.id);
        nodeObject.name = node.id + "_" + node.kind;
        nodeObject.transform.SetParent(root, false);
        nodeObject.transform.localPosition = GridToLocalPosition(node.y, node.x, 0.026f);

        CreateNodeIcon(nodeObject.transform, node.kind);
        CreateNodeClickTarget(nodeObject, node);

        if (drawLabels)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(nodeObject.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, nodeRadius * 1.8f, nodeRadius * 0.8f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = node.y + "," + node.x;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = nodeRadius * 1.6f;
            label.color = labelColor;
        }

        return nodeObject;
    }

    private void CreateNodeClickTarget(GameObject nodeObject, MapNode node)
    {
        if (!enableNodeClick || nodeObject == null || node == null)
        {
            return;
        }

        float maxRadius = Mathf.Min(nodeSpacing.x, nodeSpacing.y) * 0.32f;
        float clickRadius = Mathf.Clamp(
            GetNodeIconMaxSize(node.kind) * nodeClickBoundsScale * 0.5f,
            nodeRadius * 1.15f,
            maxRadius);
        SphereCollider collider = nodeObject.AddComponent<SphereCollider>();
        collider.center = Vector3.zero;
        collider.radius = clickRadius;

        RoguelikeMapNodeClickTarget target = nodeObject.AddComponent<RoguelikeMapNodeClickTarget>();
        target.Initialize(this, node, enableNodeHoverScale, nodeHoverScaleMultiplier, nodeHoverScaleLerpSpeed);
    }

    private void CreateNodeIcon(Transform parent, MapNodeKind kind)
    {
        float ordinarySize = Mathf.Min(nodeSpacing.x, nodeSpacing.y) * 0.72f;

        switch (kind)
        {
            case MapNodeKind.Start:
                CreateStartIcon(parent, ordinarySize);
                break;
            case MapNodeKind.Shop:
                if (TryCreateAtlasNodeIcon(parent, kind, ordinarySize))
                {
                    break;
                }

                CreateShopIcon(parent, ordinarySize);
                break;
            case MapNodeKind.Campfire:
                if (TryCreateAtlasNodeIcon(parent, kind, ordinarySize))
                {
                    break;
                }

                CreateCampfireIcon(parent, ordinarySize);
                break;
            case MapNodeKind.Elite:
                if (TryCreateAtlasNodeIcon(parent, kind, ordinarySize))
                {
                    break;
                }

                CreateEliteIcon(parent, ordinarySize);
                break;
            case MapNodeKind.Boss:
                if (TryCreateAtlasNodeIcon(parent, kind, ordinarySize * 3f))
                {
                    break;
                }

                CreateBossSkullIcon(parent, ordinarySize * 3f);
                break;
            default:
                if (TryCreateAtlasNodeIcon(parent, MapNodeKind.Question, ordinarySize))
                {
                    break;
                }

                CreateQuestionIcon(parent, ordinarySize);
                break;
        }
    }

    private bool TryCreateAtlasNodeIcon(Transform parent, MapNodeKind kind, float maxSize)
    {
        if (!useNodeIconAtlas || nodeIconAtlas == null)
        {
            return false;
        }

        Rect rect = GetNodeIconRect(kind);
        float aspect = Mathf.Max(0.1f, (rect.width * nodeIconAtlas.width) / Mathf.Max(1f, rect.height * nodeIconAtlas.height));
        float width = maxSize;
        float height = maxSize;

        if (aspect > 1f)
        {
            height = maxSize / aspect;
        }
        else
        {
            width = maxSize * aspect;
        }

        GameObject iconObject = new GameObject(kind + "AtlasIcon");
        iconObject.transform.SetParent(parent, false);
        iconObject.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = iconObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateAtlasQuadMesh(width, height, rect, mirrorNodeIconAtlasHorizontally);

        MeshRenderer renderer = iconObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ResolveIconAtlasMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return true;
    }

    private Rect GetNodeIconRect(MapNodeKind kind)
    {
        switch (kind)
        {
            case MapNodeKind.Shop:
                return shopIconRect;
            case MapNodeKind.Campfire:
                return campfireIconRect;
            case MapNodeKind.Elite:
                return eliteIconRect;
            case MapNodeKind.Boss:
                return bossIconRect;
            default:
                return questionIconRect;
        }
    }

    private static Mesh CreateAtlasQuadMesh(float width, float height, Rect uvRect, bool mirrorHorizontally)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        List<Vector3> vertices = new List<Vector3>
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f)
        };
        float leftU = mirrorHorizontally ? uvRect.xMax : uvRect.xMin;
        float rightU = mirrorHorizontally ? uvRect.xMin : uvRect.xMax;
        List<Vector2> uvs = new List<Vector2>
        {
            new Vector2(leftU, uvRect.yMin),
            new Vector2(leftU, uvRect.yMax),
            new Vector2(rightU, uvRect.yMax),
            new Vector2(rightU, uvRect.yMin)
        };

        Mesh mesh = new Mesh();
        mesh.name = "Node Icon Atlas Quad";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateStartIcon(Transform parent, float size)
    {
        float width = size * 0.08f;
        CreateInkEllipse(parent, "StartRingOuter", Vector2.zero, size * 0.36f, size * 0.36f, 0f, 360f, 28, width);
        CreateInkEllipse(parent, "StartRingInner", Vector2.zero, size * 0.2f, size * 0.2f, 0f, 360f, 24, width * 0.85f);
        CreateInkDot(parent, "StartCenter", Vector2.zero, size * 0.055f);
    }

    private void CreateQuestionIcon(Transform parent, float size)
    {
        float width = size * 0.085f;
        List<Vector2> curve = new List<Vector2>();
        for (int i = 0; i <= 14; i++)
        {
            float angle = Mathf.Lerp(135f, -80f, i / 14f) * Mathf.Deg2Rad;
            curve.Add(new Vector2(Mathf.Cos(angle) * size * 0.3f, (Mathf.Sin(angle) * size * 0.31f) + (size * 0.12f)));
        }

        CreateInkStroke(parent, "QuestionHook", curve, width);
        CreateInkStroke(parent, "QuestionStem", new List<Vector2>
        {
            new Vector2(size * 0.06f, -size * 0.16f),
            new Vector2(0f, -size * 0.28f)
        }, width);
        CreateInkDot(parent, "QuestionDot", new Vector2(0f, -size * 0.44f), size * 0.07f);
    }

    private void CreateShopIcon(Transform parent, float size)
    {
        float width = size * 0.065f;
        CreateInkStroke(parent, "ShopRoof", new List<Vector2>
        {
            new Vector2(-size * 0.46f, size * 0.12f),
            new Vector2(-size * 0.26f, size * 0.42f),
            new Vector2(size * 0.26f, size * 0.42f),
            new Vector2(size * 0.46f, size * 0.12f),
            new Vector2(-size * 0.46f, size * 0.12f)
        }, width);
        CreateInkStroke(parent, "ShopCounter", new List<Vector2>
        {
            new Vector2(-size * 0.38f, -size * 0.18f),
            new Vector2(size * 0.38f, -size * 0.18f),
            new Vector2(size * 0.32f, -size * 0.38f),
            new Vector2(-size * 0.32f, -size * 0.38f),
            new Vector2(-size * 0.38f, -size * 0.18f)
        }, width);
        CreateInkStroke(parent, "ShopLeftPost", new List<Vector2>
        {
            new Vector2(-size * 0.3f, size * 0.1f),
            new Vector2(-size * 0.3f, -size * 0.18f)
        }, width);
        CreateInkStroke(parent, "ShopRightPost", new List<Vector2>
        {
            new Vector2(size * 0.3f, size * 0.1f),
            new Vector2(size * 0.3f, -size * 0.18f)
        }, width);
        CreateInkStroke(parent, "ShopAwning", new List<Vector2>
        {
            new Vector2(-size * 0.22f, size * 0.36f),
            new Vector2(-size * 0.28f, size * 0.13f),
            new Vector2(0f, size * 0.4f),
            new Vector2(0f, size * 0.13f),
            new Vector2(size * 0.22f, size * 0.36f),
            new Vector2(size * 0.28f, size * 0.13f)
        }, width * 0.75f);
        CreateInkDot(parent, "ShopBottle", new Vector2(-size * 0.12f, -size * 0.02f), size * 0.045f);
        CreateInkDot(parent, "ShopCoin", new Vector2(size * 0.12f, -size * 0.03f), size * 0.04f);
    }

    private void CreateCampfireIcon(Transform parent, float size)
    {
        float width = size * 0.07f;
        CreateInkStroke(parent, "CampfireLogA", new List<Vector2>
        {
            new Vector2(-size * 0.36f, -size * 0.35f),
            new Vector2(size * 0.36f, -size * 0.18f)
        }, width);
        CreateInkStroke(parent, "CampfireLogB", new List<Vector2>
        {
            new Vector2(size * 0.36f, -size * 0.35f),
            new Vector2(-size * 0.36f, -size * 0.18f)
        }, width);
        CreateInkStroke(parent, "CampfireOuterFlame", new List<Vector2>
        {
            new Vector2(0f, -size * 0.16f),
            new Vector2(-size * 0.22f, size * 0.1f),
            new Vector2(-size * 0.06f, size * 0.44f),
            new Vector2(size * 0.08f, size * 0.13f),
            new Vector2(size * 0.24f, size * 0.32f),
            new Vector2(size * 0.18f, -size * 0.03f),
            new Vector2(0f, -size * 0.16f)
        }, width);
        CreateInkStroke(parent, "CampfireInnerFlame", new List<Vector2>
        {
            new Vector2(-size * 0.03f, -size * 0.06f),
            new Vector2(size * 0.02f, size * 0.24f),
            new Vector2(size * 0.12f, size * 0.03f)
        }, width * 0.72f);
    }

    private void CreateEliteIcon(Transform parent, float size)
    {
        float width = size * 0.07f;
        CreateInkEllipse(parent, "EliteLeftHorn", new Vector2(-size * 0.2f, size * 0.2f), size * 0.24f, size * 0.18f, 40f, 205f, 12, width);
        CreateInkEllipse(parent, "EliteRightHorn", new Vector2(size * 0.2f, size * 0.2f), size * 0.24f, size * 0.18f, -25f, 140f, 12, width);
        CreateInkStroke(parent, "EliteHead", new List<Vector2>
        {
            new Vector2(-size * 0.28f, size * 0.16f),
            new Vector2(-size * 0.18f, -size * 0.2f),
            new Vector2(0f, -size * 0.38f),
            new Vector2(size * 0.18f, -size * 0.2f),
            new Vector2(size * 0.28f, size * 0.16f),
            new Vector2(0f, size * 0.34f),
            new Vector2(-size * 0.28f, size * 0.16f)
        }, width);
        CreateInkDot(parent, "EliteLeftEye", new Vector2(-size * 0.09f, size * 0.02f), size * 0.045f);
        CreateInkDot(parent, "EliteRightEye", new Vector2(size * 0.09f, size * 0.02f), size * 0.045f);
        CreateInkStroke(parent, "EliteClaws", new List<Vector2>
        {
            new Vector2(-size * 0.38f, -size * 0.22f),
            new Vector2(-size * 0.22f, -size * 0.06f),
            new Vector2(size * 0.22f, -size * 0.06f),
            new Vector2(size * 0.38f, -size * 0.22f)
        }, width * 0.8f);
    }

    private void CreateBossSkullIcon(Transform parent, float size)
    {
        float width = size * 0.045f;
        CreateInkEllipse(parent, "BossLeftHorn", new Vector2(-size * 0.34f, size * 0.19f), size * 0.26f, size * 0.28f, 60f, 245f, 18, width);
        CreateInkEllipse(parent, "BossRightHorn", new Vector2(size * 0.34f, size * 0.19f), size * 0.26f, size * 0.28f, -65f, 120f, 18, width);
        CreateInkStroke(parent, "BossSkullOutline", new List<Vector2>
        {
            new Vector2(-size * 0.28f, size * 0.28f),
            new Vector2(-size * 0.36f, size * 0.02f),
            new Vector2(-size * 0.22f, -size * 0.34f),
            new Vector2(0f, -size * 0.48f),
            new Vector2(size * 0.22f, -size * 0.34f),
            new Vector2(size * 0.36f, size * 0.02f),
            new Vector2(size * 0.28f, size * 0.28f),
            new Vector2(0f, size * 0.42f),
            new Vector2(-size * 0.28f, size * 0.28f)
        }, width);
        CreateInkDot(parent, "BossLeftEye", new Vector2(-size * 0.12f, size * 0.02f), size * 0.055f);
        CreateInkDot(parent, "BossRightEye", new Vector2(size * 0.12f, size * 0.02f), size * 0.055f);
        CreateInkStroke(parent, "BossNose", new List<Vector2>
        {
            new Vector2(0f, -size * 0.08f),
            new Vector2(-size * 0.05f, -size * 0.22f),
            new Vector2(size * 0.05f, -size * 0.22f),
            new Vector2(0f, -size * 0.08f)
        }, width * 0.85f);
        CreateInkStroke(parent, "BossTeeth", new List<Vector2>
        {
            new Vector2(-size * 0.1f, -size * 0.34f),
            new Vector2(-size * 0.04f, -size * 0.44f),
            new Vector2(0f, -size * 0.34f),
            new Vector2(size * 0.04f, -size * 0.44f),
            new Vector2(size * 0.1f, -size * 0.34f)
        }, width * 0.7f);
    }

    private static Mesh CreateInkDiscMesh(float radius, int segments)
    {
        int safeSegments = Mathf.Clamp(segments, 8, 48);
        List<Vector3> vertices = new List<Vector3>(safeSegments + 1);
        List<Vector2> uvs = new List<Vector2>(safeSegments + 1);
        List<int> triangles = new List<int>(safeSegments * 6);

        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < safeSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / safeSegments;
            Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vertices.Add(new Vector3(point.x, point.y, 0f));
            uvs.Add(new Vector2((point.x / (radius * 2f)) + 0.5f, (point.y / (radius * 2f)) + 0.5f));
        }

        for (int i = 1; i <= safeSegments; i++)
        {
            int next = i == safeSegments ? 1 : i + 1;
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(next);
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(i);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Ink Disc Mesh";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateInkStroke(Transform parent, string name, List<Vector2> points, float width)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        CreateInkMeshObject(parent, name, CreateInkStrokeMesh(points, width), Vector3.zero);
    }

    private void CreateInkEllipse(
        Transform parent,
        string name,
        Vector2 center,
        float radiusX,
        float radiusY,
        float startAngle,
        float endAngle,
        int segments,
        float width)
    {
        int safeSegments = Mathf.Clamp(segments, 4, 48);
        List<Vector2> points = new List<Vector2>(safeSegments + 1);

        for (int i = 0; i <= safeSegments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)safeSegments) * Mathf.Deg2Rad;
            points.Add(new Vector2(
                center.x + (Mathf.Cos(angle) * radiusX),
                center.y + (Mathf.Sin(angle) * radiusY)));
        }

        CreateInkStroke(parent, name, points, width);
    }

    private void CreateInkDot(Transform parent, string name, Vector2 center, float radius)
    {
        CreateInkMeshObject(parent, name, CreateInkDiscMesh(radius, 18), new Vector3(center.x, center.y, 0.001f));
    }

    private void CreateInkMeshObject(Transform parent, string name, Mesh mesh, Vector3 localPosition)
    {
        GameObject inkObject = new GameObject(name);
        inkObject.transform.SetParent(parent, false);
        inkObject.transform.localPosition = localPosition;

        MeshFilter meshFilter = inkObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = inkObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ResolveMaterial(nodeMaterial, nodeColor, ref generatedNodeMaterial, "GeneratedMapNodeInkMat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Mesh CreateInkStrokeMesh(List<Vector2> points, float width)
    {
        List<Vector3> vertices = new List<Vector3>((points.Count - 1) * 4);
        List<int> triangles = new List<int>((points.Count - 1) * 12);

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 from = points[i];
            Vector2 to = points[i + 1];
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            int baseIndex = vertices.Count;
            vertices.Add(new Vector3(from.x - normal.x, from.y - normal.y, 0f));
            vertices.Add(new Vector3(from.x + normal.x, from.y + normal.y, 0f));
            vertices.Add(new Vector3(to.x + normal.x, to.y + normal.y, 0f));
            vertices.Add(new Vector3(to.x - normal.x, to.y - normal.y, 0f));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 2);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Ink Stroke Mesh";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateLineView(Transform root, MapNode from, MapNode to)
    {
        Mesh routeMesh = CreateStylizedRouteMesh(from, to);
        if (routeMesh == null)
        {
            return;
        }

        GameObject lineObject = new GameObject("Line_" + from.id + "_to_" + to.id);
        lineObject.transform.SetParent(root, false);

        MeshFilter meshFilter = lineObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = routeMesh;

        MeshRenderer renderer = lineObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ResolveMaterial(lineMaterial, lineColor, ref generatedLineMaterial, "GeneratedMapLineInkMat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private Mesh CreateStylizedRouteMesh(MapNode fromNode, MapNode toNode)
    {
        Vector3 from = GridToLocalPosition(fromNode.y, fromNode.x, 0.014f);
        Vector3 to = GridToLocalPosition(toNode.y, toNode.x, 0.014f);
        Vector2 flatFrom = new Vector2(from.x, from.y);
        Vector2 flatTo = new Vector2(to.x, to.y);
        Vector2 direction = flatTo - flatFrom;
        float fullLength = direction.magnitude;
        if (fullLength < 0.0001f)
        {
            return null;
        }

        Vector2 normalizedDirection = direction / fullLength;
        float fromClearance = GetRouteClearance(fromNode.kind);
        float toClearance = GetRouteClearance(toNode.kind);
        float combinedClearance = fromClearance + toClearance;
        if (combinedClearance > fullLength * 0.82f)
        {
            float scale = (fullLength * 0.82f) / combinedClearance;
            fromClearance *= scale;
            toClearance *= scale;
        }

        Vector2 trimmedFrom = flatFrom + (normalizedDirection * fromClearance);
        Vector2 trimmedTo = flatTo - (normalizedDirection * toClearance);
        float routeLength = Vector2.Distance(trimmedFrom, trimmedTo);
        if (routeLength < lineWidth * 2f)
        {
            return null;
        }

        System.Random routeRandom = new System.Random(BuildStableRouteSeed(fromNode, toNode));
        Vector2 routeDirection = (trimmedTo - trimmedFrom).normalized;
        Vector2 routeNormal = new Vector2(-routeDirection.y, routeDirection.x);
        float inkScale = Mathf.Min(nodeSpacing.x, nodeSpacing.y);
        float dashLengthBase = Mathf.Clamp(inkScale * 0.07f, lineWidth * 2.4f, lineWidth * 5.8f);
        float gapLengthBase = dashLengthBase * 0.82f;
        float jitterAmplitude = Mathf.Min(inkScale * 0.018f, lineWidth * 1.35f);
        List<Vector3> vertices = new List<Vector3>(48);
        List<int> triangles = new List<int>(144);
        float cursor = 0f;

        while (cursor < routeLength)
        {
            float dashLength = dashLengthBase * Mathf.Lerp(0.72f, 1.28f, (float)routeRandom.NextDouble());
            float dashEnd = Mathf.Min(routeLength, cursor + dashLength);
            if (dashEnd - cursor > lineWidth * 0.75f)
            {
                float startT = cursor / routeLength;
                float endT = dashEnd / routeLength;
                Vector2 dashFrom = Vector2.Lerp(trimmedFrom, trimmedTo, startT);
                Vector2 dashTo = Vector2.Lerp(trimmedFrom, trimmedTo, endT);
                float fromJitter = ((float)routeRandom.NextDouble() - 0.5f) * jitterAmplitude;
                float toJitter = ((float)routeRandom.NextDouble() - 0.5f) * jitterAmplitude;
                float width = lineWidth * Mathf.Lerp(0.92f, 1.42f, (float)routeRandom.NextDouble());
                AppendInkDash(vertices, triangles, dashFrom + (routeNormal * fromJitter), dashTo + (routeNormal * toJitter), width, from.z);
            }

            float gapLength = gapLengthBase * Mathf.Lerp(0.58f, 1.18f, (float)routeRandom.NextDouble());
            cursor = dashEnd + gapLength;
        }

        if (vertices.Count == 0)
        {
            return null;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Dashed Ink Route Mesh";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float GetRouteClearance(MapNodeKind kind)
    {
        float size = GetNodeIconMaxSize(kind);
        float clearanceScale = kind == MapNodeKind.Boss ? 0.43f : 0.38f;
        return (size * clearanceScale) + (lineWidth * 1.6f);
    }

    private float GetNodeIconMaxSize(MapNodeKind kind)
    {
        float ordinarySize = Mathf.Min(nodeSpacing.x, nodeSpacing.y) * 0.72f;
        return kind == MapNodeKind.Boss ? ordinarySize * 3f : ordinarySize;
    }

    private static void AppendInkDash(List<Vector3> vertices, List<int> triangles, Vector2 from, Vector2 to, float width, float z)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int baseIndex = vertices.Count;
        vertices.Add(new Vector3(from.x - normal.x, from.y - normal.y, z));
        vertices.Add(new Vector3(from.x + normal.x, from.y + normal.y, z));
        vertices.Add(new Vector3(to.x + normal.x, to.y + normal.y, z));
        vertices.Add(new Vector3(to.x - normal.x, to.y - normal.y, z));

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 3);
        triangles.Add(baseIndex + 2);
    }

    private static int BuildStableRouteSeed(MapNode from, MapNode to)
    {
        unchecked
        {
            int value = 23;
            value = (value * 31) + from.y;
            value = (value * 31) + from.x;
            value = (value * 31) + to.y;
            value = (value * 31) + to.x;
            return value;
        }
    }

    private Vector3 GridToLocalPosition(int y, int x, float z = 0.018f)
    {
        float offsetX = (columns - 1) * nodeSpacing.x * 0.5f;
        float offsetY = (rows - 1) * nodeSpacing.y * 0.5f;
        return new Vector3(x * nodeSpacing.x - offsetX, y * nodeSpacing.y - offsetY, z);
    }

    private void CreateParchmentView(Transform root)
    {
        GameObject parchment = new GameObject("Old Vertical Parchment");
        parchment.transform.SetParent(root, false);
        parchment.transform.localPosition = new Vector3(0f, 0f, -0.004f);

        MeshFilter meshFilter = parchment.AddComponent<MeshFilter>();
        Mesh mesh = CreateParchmentMesh();
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = parchment.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ResolveParchmentMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        MeshCollider meshCollider = parchment.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }

    private Mesh CreateParchmentMesh()
    {
        int segments = Mathf.Clamp(parchmentEdgeSegments, 4, 64);
        float halfWidth = parchmentSize.x * 0.5f;
        float halfHeight = parchmentSize.y * 0.5f;
        System.Random edgeRandom = new System.Random(seed ^ 0x4f1bbcdc);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));

        AddParchmentEdge(vertices, uvs, edgeRandom, new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight), segments);
        AddParchmentEdge(vertices, uvs, edgeRandom, new Vector2(halfWidth, -halfHeight), new Vector2(halfWidth, halfHeight), segments);
        AddParchmentEdge(vertices, uvs, edgeRandom, new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight), segments);
        AddParchmentEdge(vertices, uvs, edgeRandom, new Vector2(-halfWidth, halfHeight), new Vector2(-halfWidth, -halfHeight), segments);

        List<int> triangles = new List<int>();
        int edgeCount = vertices.Count - 1;
        for (int i = 1; i <= edgeCount; i++)
        {
            int next = i == edgeCount ? 1 : i + 1;
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(next);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Procedural Old Parchment Mesh";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddParchmentEdge(
        List<Vector3> vertices,
        List<Vector2> uvs,
        System.Random edgeRandom,
        Vector2 from,
        Vector2 to,
        int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            Vector2 point = Vector2.Lerp(from, to, t);
            Vector2 jitter = new Vector2(
                ((float)edgeRandom.NextDouble() - 0.5f) * parchmentEdgeJitter,
                ((float)edgeRandom.NextDouble() - 0.5f) * parchmentEdgeJitter);

            Vector2 jaggedPoint = point + jitter;
            vertices.Add(new Vector3(jaggedPoint.x, jaggedPoint.y, 0f));
            uvs.Add(new Vector2(
                Mathf.InverseLerp(-parchmentSize.x * 0.5f, parchmentSize.x * 0.5f, point.x),
                Mathf.InverseLerp(-parchmentSize.y * 0.5f, parchmentSize.y * 0.5f, point.y)));
        }
    }

    private Material ResolveParchmentMaterial()
    {
        if (generatedParchmentMaterial != null)
        {
            return generatedParchmentMaterial;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            generatedParchmentTexture = ResolveEditorParchmentTexture();
            generatedParchmentMaterial = CreateOrUpdateEditorTextureMaterial("GeneratedMapParchmentMat", generatedParchmentTexture, Color.white, false);
            return generatedParchmentMaterial;
        }
#endif

        Shader shader = ResolveMapShader();
        generatedParchmentMaterial = new Material(shader);
        generatedParchmentMaterial.name = "Generated Old Parchment Material";
        generatedParchmentTexture = CreateParchmentTexture(512, 896);
        generatedParchmentMaterial.hideFlags = HideFlags.DontSaveInEditor;
        ConfigureTextureMaterial(generatedParchmentMaterial, generatedParchmentTexture, Color.white, false);

        return generatedParchmentMaterial;
    }

    private Material ResolveIconAtlasMaterial()
    {
        if (generatedIconAtlasMaterial != null)
        {
            return generatedIconAtlasMaterial;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            generatedIconAtlasMaterial = CreateOrUpdateEditorTextureMaterial("GeneratedMapIconAtlasMat", nodeIconAtlas, Color.white, true);
            return generatedIconAtlasMaterial;
        }
#endif

        Shader shader = ResolveMapShader();
        generatedIconAtlasMaterial = new Material(shader);
        generatedIconAtlasMaterial.name = "Generated Node Icon Atlas Material";
        generatedIconAtlasMaterial.hideFlags = HideFlags.DontSaveInEditor;
        ConfigureTextureMaterial(generatedIconAtlasMaterial, nodeIconAtlas, Color.white, true);
        return generatedIconAtlasMaterial;
    }

    private static Shader ResolveMapShader()
    {
        Shader shader = Shader.Find(c_DefaultUrpUnlitShader);
        if (shader == null)
        {
            shader = Shader.Find(c_DefaultUrpLitShader);
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find(c_StandardShader);
        }

        return shader;
    }

    private static void ConfigureTextureMaterial(Material material, Texture texture, Color color, bool transparent)
    {
        if (material == null)
        {
            return;
        }

        material.mainTexture = texture;
        material.color = color;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (transparent)
        {
            ConfigureTransparentMaterial(material, texture);
            return;
        }

        ConfigureOpaqueMaterial(material);
    }

    private static void ConfigureOpaqueMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 1f);
        }

        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = -1;
    }

    private static void ConfigureTransparentMaterial(Material material, Texture texture)
    {
        if (material == null)
        {
            return;
        }

        material.mainTexture = texture;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
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

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private Texture2D CreateParchmentTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Generated Old Parchment Texture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        const int stainCount = 22;
        Vector2[] stainCenters = new Vector2[stainCount];
        float[] stainRadii = new float[stainCount];
        float[] stainStrengths = new float[stainCount];
        System.Random textureRandom = new System.Random(seed ^ 0x1c2d3e4f);

        for (int i = 0; i < stainCount; i++)
        {
            stainCenters[i] = new Vector2((float)textureRandom.NextDouble(), (float)textureRandom.NextDouble());
            stainRadii[i] = Mathf.Lerp(0.035f, 0.22f, (float)textureRandom.NextDouble());
            stainStrengths[i] = Mathf.Lerp(0.06f, 0.28f, (float)textureRandom.NextDouble());
        }

        Color parchmentHazeColor = Color.Lerp(parchmentLightColor, Color.white, 0.18f);
        Color creaseColor = Color.Lerp(parchmentDarkColor, parchmentBurnColor, 0.32f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                float broadCloud = Mathf.PerlinNoise(u * 2.15f + 8.3f, v * 2.7f + 3.2f);
                float haze = Mathf.SmoothStep(0.24f, 0.78f, broadCloud) * 0.34f;
                float shadowCloud = Mathf.SmoothStep(0.52f, 0.92f, Mathf.PerlinNoise(u * 4.1f + 17.7f, v * 3.35f + 5.6f)) * 0.1f;
                float fiber = Mathf.PerlinNoise(u * 16.5f, v * 22.0f) * 0.15f;
                fiber += Mathf.PerlinNoise(u * 56.0f + 9.1f, v * 7.0f + 2.3f) * 0.075f;
                fiber += Mathf.PerlinNoise(u * 125.0f + 4.7f, v * 91.0f + 12.9f) * 0.035f;
                float verticalGrain = Mathf.Sin((u * 72f) + (Mathf.PerlinNoise(v * 7f, 0.37f) * 4f)) * 0.022f;
                float crossGrain = Mathf.Sin((v * 95f) + (Mathf.PerlinNoise(u * 6.2f, 0.73f) * 4f)) * 0.01f;
                float paperTone = Mathf.Clamp01(fiber + verticalGrain + crossGrain);

                Color color = Color.Lerp(parchmentLightColor, parchmentDarkColor, paperTone);
                color = Color.Lerp(color, parchmentDarkColor, shadowCloud);
                color = Color.Lerp(color, parchmentHazeColor, haze);

                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float edgeNoise = Mathf.PerlinNoise(u * 30.0f + 2.9f, v * 30.0f + 11.7f) * 0.035f;
                float burnedEdge = 1f - Mathf.SmoothStep(0.008f, 0.15f + edgeNoise, edge);
                float charRim = 1f - Mathf.SmoothStep(0.0f, 0.035f + edgeNoise, edge);
                color = Color.Lerp(color, parchmentBurnColor, burnedEdge * 0.72f);
                color = Color.Lerp(color, Color.black, charRim * 0.42f);

                float centerVignette = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.18f);
                color = Color.Lerp(color, parchmentDarkColor, centerVignette * 0.06f);

                for (int i = 0; i < stainCount; i++)
                {
                    float distance = Vector2.Distance(new Vector2(u, v), stainCenters[i]);
                    float stain = 1f - Mathf.SmoothStep(0f, stainRadii[i], distance);
                    float stainGrain = Mathf.PerlinNoise((u + i) * 18.0f, (v - i) * 18.0f);
                    color = Color.Lerp(color, parchmentDarkColor, stain * stainStrengths[i] * Mathf.Lerp(0.55f, 1.15f, stainGrain));
                }

                float foldA = 1f - Mathf.SmoothStep(0.0f, 0.011f, Mathf.Abs(u - 0.515f));
                float foldB = 1f - Mathf.SmoothStep(0.0f, 0.012f, Mathf.Abs(v - 0.49f));
                float foldStrength = (foldA * 0.07f) + (foldB * 0.052f);
                color = Color.Lerp(color, creaseColor, foldStrength);

                float fleck = Hash01(x, y, seed ^ 0x5eED123);
                if (fleck > 0.994f)
                {
                    color = Color.Lerp(color, parchmentBurnColor, Mathf.Lerp(0.18f, 0.42f, fleck));
                }
                else if (fleck < 0.012f)
                {
                    color = Color.Lerp(color, parchmentHazeColor, 0.18f);
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static float Hash01(int x, int y, int seedValue)
    {
        unchecked
        {
            uint hash = (uint)seedValue;
            hash ^= (uint)(x * 374761393);
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)(y * 668265263);
            hash *= 2246822519u;
            hash ^= hash >> 15;
            return (hash & 0x00ffffff) / 16777215f;
        }
    }

    private Material ResolveMaterial(Material source, Color color, ref Material generatedMaterial, string editorAssetName)
    {
        if (source != null)
        {
            return source;
        }

        if (generatedMaterial != null)
        {
            return generatedMaterial;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            generatedMaterial = CreateOrUpdateEditorColorMaterial(editorAssetName, color);
            return generatedMaterial;
        }
#endif

        Shader shader = ResolveMapShader();
        generatedMaterial = new Material(shader);
        generatedMaterial.hideFlags = HideFlags.DontSaveInEditor;
        ConfigureTextureMaterial(generatedMaterial, null, color, false);
        return generatedMaterial;
    }

#if UNITY_EDITOR
    private Texture2D ResolveEditorParchmentTexture()
    {
        EnsureEditorFolder(c_GeneratedAssetsFolder);
        EnsureEditorFolder(c_GeneratedSceneFolder);
        EnsureEditorFolder(c_GeneratedMaterialsFolder);

        string texturePath = $"{c_GeneratedMaterialsFolder}/GeneratedMapParchmentTexture.asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture != null)
        {
            return texture;
        }

        texture = CreateParchmentTexture(512, 896);
        texture.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(texture, texturePath);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private Material CreateOrUpdateEditorTextureMaterial(string assetName, Texture texture, Color color, bool transparent)
    {
        Material material = CreateOrLoadEditorMaterial(assetName);
        ConfigureTextureMaterial(material, texture, color, transparent);
        EditorUtility.SetDirty(material);
        return material;
    }

    private Material CreateOrUpdateEditorColorMaterial(string assetName, Color color)
    {
        Material material = CreateOrLoadEditorMaterial(assetName);
        ConfigureTextureMaterial(material, null, color, false);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrLoadEditorMaterial(string assetName)
    {
        EnsureEditorFolder(c_GeneratedAssetsFolder);
        EnsureEditorFolder(c_GeneratedSceneFolder);
        EnsureEditorFolder(c_GeneratedMaterialsFolder);

        Shader shader = ResolveMapShader();
        string materialPath = $"{c_GeneratedMaterialsFolder}/{assetName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = assetName
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        material.hideFlags = HideFlags.None;
        return material;
    }

    private static void EnsureEditorFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] segments = folderPath.Split('/');
        string current = segments[0];

        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }
#endif

    private string BuildDebugLog()
    {
        List<string> lines = new List<string>();
        lines.Add("RoguelikeMapGenerator generated " + nodesById.Count + " nodes and " + connectionKeys.Count + " unique edges.");

        for (int y = 0; y < rows; y++)
        {
            List<string> rowNodes = new List<string>();
            for (int x = 0; x < columns; x++)
            {
                rowNodes.Add(nodeyx[y, x] == null ? "." : "O");
            }

            lines.Add("y=" + y + " " + string.Join(" ", rowNodes.ToArray()));
        }

        lines.Add("Edges: " + string.Join(", ", new List<string>(connectionKeys).ToArray()));
        return string.Join(Environment.NewLine, lines.ToArray());
    }
}
