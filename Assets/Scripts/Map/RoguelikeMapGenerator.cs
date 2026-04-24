using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates a Slay-the-Spire-like map on a nodeyx[y, x] grid.
/// Default layout is 5 columns horizontally and 9 rows vertically.
/// </summary>
public sealed class RoguelikeMapGenerator : MonoBehaviour
{
    [Serializable]
    public sealed class MapNode
    {
        public int y;
        public int x;
        public string id;
        public List<string> nextNodeIds = new List<string>();

        [NonSerialized] public List<MapNode> nextNodes = new List<MapNode>();

        public MapNode(int y, int x)
        {
            this.y = y;
            this.x = x;
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
    private Material generatedNodeMaterial;
    private Material generatedLineMaterial;
    private Material generatedParchmentMaterial;
    private Texture2D generatedParchmentTexture;

    public void SetupAsTabletopParchment(Vector3 worldPosition, Quaternion worldRotation)
    {
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        transform.localScale = Vector3.one;
        generateOnStart = true;
        drawView = true;
        drawParchment = true;
        fitMapToParchment = true;
        logGeneratedData = false;
        parchmentSize = new Vector2(1.08f, 1.94f);
        parchmentPadding = 0.15f;
    }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateMap();
        }
    }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        NormalizeSettings();
        random = useRandomSeed ? new System.Random() : new System.Random(seed);

        nodeyx = new MapNode[rows, columns];
        nodesById.Clear();
        connectionKeys.Clear();

        List<int> startColumns = PickStartColumns();
        List<MapNode> activeNodes = new List<MapNode>(startColumns.Count);
        for (int i = 0; i < startColumns.Count; i++)
        {
            activeNodes.Add(GetOrCreateNode(0, startColumns[i]));
        }

        for (int y = 0; y < rows - 1; y++)
        {
            List<MapNode> rowParents = GetUniqueSortedParents(activeNodes, y);
            Dictionary<string, IntRange> chosenOutgoingRanges = new Dictionary<string, IntRange>();
            List<MapNode> nextActiveNodes = new List<MapNode>(rowParents.Count);

            for (int i = 0; i < rowParents.Count; i++)
            {
                MapNode parent = rowParents[i];
                IntRange safeTargetRange = GetSafeTargetRange(rowParents, i, chosenOutgoingRanges);
                int targetX = random.Next(safeTargetRange.min, safeTargetRange.max + 1);
                MapNode target = GetOrCreateNode(y + 1, targetX);

                AddUniqueConnection(parent, target);
                chosenOutgoingRanges[parent.id] = new IntRange(targetX, targetX);
                nextActiveNodes.Add(target);
            }

            activeNodes = nextActiveNodes;
        }

        if (drawView)
        {
            RebuildView();
        }

        if (logGeneratedData)
        {
            Debug.Log(BuildDebugLog());
        }
    }

    private void NormalizeSettings()
    {
        rows = Mathf.Max(2, rows);
        columns = Mathf.Max(3, columns);
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
        x = Mathf.Clamp(x, 0, columns - 1);
        string id = BuildId(y, x);

        MapNode node;
        if (nodesById.TryGetValue(id, out node))
        {
            return node;
        }

        node = new MapNode(y, x);
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

        Dictionary<string, GameObject> views = new Dictionary<string, GameObject>();
        foreach (KeyValuePair<string, MapNode> pair in nodesById)
        {
            views.Add(pair.Key, CreateNodeView(root, pair.Value));
        }

        foreach (KeyValuePair<string, MapNode> pair in nodesById)
        {
            MapNode from = pair.Value;
            for (int i = 0; i < from.nextNodes.Count; i++)
            {
                CreateLineView(root, from, from.nextNodes[i]);
            }
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
        GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nodeObject.name = node.id;
        nodeObject.transform.SetParent(root, false);
        nodeObject.transform.localPosition = GridToLocalPosition(node.y, node.x);
        nodeObject.transform.localScale = Vector3.one * nodeRadius;

        Renderer renderer = nodeObject.GetComponent<Renderer>();
        renderer.sharedMaterial = ResolveMaterial(nodeMaterial, nodeColor, ref generatedNodeMaterial);

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

    private void CreateLineView(Transform root, MapNode from, MapNode to)
    {
        GameObject lineObject = new GameObject("Line_" + from.id + "_to_" + to.id);
        lineObject.transform.SetParent(root, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.numCapVertices = 4;
        line.sharedMaterial = ResolveMaterial(lineMaterial, lineColor, ref generatedLineMaterial);
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.SetPosition(0, GridToLocalPosition(from.y, from.x));
        line.SetPosition(1, GridToLocalPosition(to.y, to.x));
    }

    private Vector3 GridToLocalPosition(int y, int x)
    {
        float offsetX = (columns - 1) * nodeSpacing.x * 0.5f;
        float offsetY = (rows - 1) * nodeSpacing.y * 0.5f;
        return new Vector3(x * nodeSpacing.x - offsetX, y * nodeSpacing.y - offsetY, 0.018f);
    }

    private void CreateParchmentView(Transform root)
    {
        GameObject parchment = new GameObject("Old Vertical Parchment");
        parchment.transform.SetParent(root, false);
        parchment.transform.localPosition = new Vector3(0f, 0f, -0.004f);

        MeshFilter meshFilter = parchment.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = CreateParchmentMesh();

        MeshRenderer renderer = parchment.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ResolveParchmentMaterial();
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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        generatedParchmentMaterial = new Material(shader);
        generatedParchmentMaterial.name = "Generated Old Parchment Material";
        generatedParchmentTexture = CreateParchmentTexture(256, 448);
        generatedParchmentMaterial.mainTexture = generatedParchmentTexture;
        generatedParchmentMaterial.color = Color.white;
        return generatedParchmentMaterial;
    }

    private Texture2D CreateParchmentTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Generated Old Parchment Texture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        const int stainCount = 12;
        Vector2[] stainCenters = new Vector2[stainCount];
        float[] stainRadii = new float[stainCount];
        float[] stainStrengths = new float[stainCount];
        System.Random textureRandom = new System.Random(seed ^ 0x1c2d3e4f);

        for (int i = 0; i < stainCount; i++)
        {
            stainCenters[i] = new Vector2((float)textureRandom.NextDouble(), (float)textureRandom.NextDouble());
            stainRadii[i] = Mathf.Lerp(0.045f, 0.16f, (float)textureRandom.NextDouble());
            stainStrengths[i] = Mathf.Lerp(0.08f, 0.24f, (float)textureRandom.NextDouble());
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                float fiber = Mathf.PerlinNoise(u * 16.5f, v * 22.0f) * 0.12f;
                fiber += Mathf.PerlinNoise(u * 56.0f + 9.1f, v * 7.0f + 2.3f) * 0.055f;
                float verticalGrain = Mathf.Sin((u * 55f) + (Mathf.PerlinNoise(v * 5f, 0.37f) * 3f)) * 0.018f;

                Color color = Color.Lerp(parchmentLightColor, parchmentDarkColor, Mathf.Clamp01(fiber + verticalGrain));

                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float burnedEdge = 1f - Mathf.SmoothStep(0.0f, 0.13f, edge);
                color = Color.Lerp(color, parchmentBurnColor, burnedEdge * 0.72f);

                for (int i = 0; i < stainCount; i++)
                {
                    float distance = Vector2.Distance(new Vector2(u, v), stainCenters[i]);
                    float stain = 1f - Mathf.SmoothStep(0f, stainRadii[i], distance);
                    color = Color.Lerp(color, parchmentDarkColor, stain * stainStrengths[i]);
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private Material ResolveMaterial(Material source, Color color, ref Material generatedMaterial)
    {
        if (source != null)
        {
            return source;
        }

        if (generatedMaterial != null)
        {
            return generatedMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        generatedMaterial = new Material(shader);
        generatedMaterial.color = color;
        generatedMaterial.hideFlags = HideFlags.DontSaveInEditor;
        return generatedMaterial;
    }

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
