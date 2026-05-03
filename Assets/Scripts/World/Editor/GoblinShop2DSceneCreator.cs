using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GoblinShop2DSceneCreator
{
    private const string ScenePath = "Assets/Scenes/GoblinShop2DScene.unity";
    private const string ShelfScenePath = "Assets/Scenes/GoblinShopShelf2DScene.unity";
    private const string StoreSceneName = "GoblinShop2DScene";
    private const string ShelfSceneName = "GoblinShopShelf2DScene";
    private const string ReturnSceneName = "SampleScene";
    private const string GeneratedRoot = "Assets/Generated";
    private const string AssetRoot = "Assets/Generated/GoblinShop2D";
    private const string MaterialsRoot = "Assets/Generated/GoblinShop2D/Materials";
    private const string MeshesRoot = "Assets/Generated/GoblinShop2D/Meshes";
    private const string StoreIllustrationPath = "Assets/Resources/store/store.jpg";
    private const string StoreHighlightIllustrationPath = "Assets/Resources/store/highlight.png";
    private const string StoreCounterInterfacePath = "Assets/Resources/store/desktop.png";

    private static Mesh quadMesh;
    private static Mesh triangleMesh;
    private static Mesh circleMesh;
    private static Transform artRoot;
    private static bool usingStoreIllustration;

    private static readonly Vector2[] CounterHotspotPixels =
    {
        new Vector2(262f, 494f),
        new Vector2(238f, 428f),
        new Vector2(236f, 358f),
        new Vector2(268f, 315f),
        new Vector2(284f, 252f),
        new Vector2(333f, 218f),
        new Vector2(350f, 160f),
        new Vector2(393f, 140f),
        new Vector2(447f, 162f),
        new Vector2(468f, 244f),
        new Vector2(535f, 255f),
        new Vector2(552f, 342f),
        new Vector2(540f, 425f),
        new Vector2(566f, 484f),
        new Vector2(452f, 501f),
        new Vector2(328f, 514f)
    };

    [MenuItem("Tools/XDemo/Create Goblin Shop Flow Scenes")]
    public static void CreateShopFlowScenes()
    {
        CreateScene();
        CreateShelfScene();
        EnsureShopScenesInBuildSettings();
    }

    [MenuItem("Tools/XDemo/Create Goblin Shop 2D Scene")]
    public static void CreateScene()
    {
        EnsureFolders();
        EnsureMeshes();

        Scene scene = OpenOrCreateScene(ScenePath);
        GameObject rootObject = GameObject.Find("GoblinShop2D_ArtRoot");
        if (rootObject == null)
        {
            rootObject = new GameObject("GoblinShop2D_ArtRoot");
        }

        artRoot = rootObject.transform;
        ClearGeneratedChildren(artRoot);

        BuildSceneArt();
        ConfigureCameraAndLighting();

        SaveSceneAndAssets(scene);
        EnsureShopScenesInBuildSettings();
    }

    [MenuItem("Tools/XDemo/Create Goblin Shop Shelf 2D Scene")]
    public static void CreateShelfScene()
    {
        EnsureFolders();
        EnsureMeshes();

        Scene scene = OpenOrCreateScene(ShelfScenePath);
        GameObject rootObject = GameObject.Find("GoblinShopShelf2D_ArtRoot");
        if (rootObject == null)
        {
            rootObject = new GameObject("GoblinShopShelf2D_ArtRoot");
        }

        artRoot = rootObject.transform;
        ClearGeneratedChildren(artRoot);

        Texture2D counterInterface = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreCounterInterfacePath);
        if (counterInterface == null)
        {
            counterInterface = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreIllustrationPath);
        }

        if (counterInterface != null)
        {
            usingStoreIllustration = true;
            BuildShelfViewScene(counterInterface);
        }
        else
        {
            usingStoreIllustration = false;
            BuildSceneArt();
        }

        ConfigureCameraAndLighting();
        SaveSceneAndAssets(scene);
        EnsureShopScenesInBuildSettings();
    }

    private static Scene OpenOrCreateScene(string scenePath)
    {
        if (File.Exists(scenePath))
        {
            return EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, scenePath);
        return scene;
    }

    private static void SaveSceneAndAssets(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ClearGeneratedChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedRoot))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }

        if (!AssetDatabase.IsValidFolder(AssetRoot))
        {
            AssetDatabase.CreateFolder(GeneratedRoot, "GoblinShop2D");
        }

        if (!AssetDatabase.IsValidFolder(MaterialsRoot))
        {
            AssetDatabase.CreateFolder(AssetRoot, "Materials");
        }

        if (!AssetDatabase.IsValidFolder(MeshesRoot))
        {
            AssetDatabase.CreateFolder(AssetRoot, "Meshes");
        }
    }

    private static void EnsureMeshes()
    {
        quadMesh = LoadOrCreateMesh("Quad", CreateQuadMesh);
        triangleMesh = LoadOrCreateMesh("Triangle", CreateTriangleMesh);
        circleMesh = LoadOrCreateMesh("Circle", CreateCircleMesh);

        OverwriteMesh(quadMesh, CreateQuadMesh());
        OverwriteMesh(triangleMesh, CreateTriangleMesh());
        OverwriteMesh(circleMesh, CreateCircleMesh());
    }

    private static Mesh LoadOrCreateMesh(string name, System.Func<Mesh> create)
    {
        string path = MeshesRoot + "/" + name + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null)
        {
            return mesh;
        }

        mesh = create();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void OverwriteMesh(Mesh target, Mesh source)
    {
        target.Clear();
        target.vertices = source.vertices;
        target.uv = source.uv;
        target.triangles = source.triangles;
        target.RecalculateNormals();
        target.RecalculateBounds();
        EditorUtility.SetDirty(target);
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Quad";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateTriangleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Triangle";
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0f, 0.5f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, new Vector2(0.5f, 1f) };
        mesh.triangles = new[] { 0, 2, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateCircleMesh()
    {
        const int segments = 48;
        List<Vector3> vertices = new List<Vector3>(segments + 1);
        List<int> triangles = new List<int>(segments * 3);
        vertices.Add(Vector3.zero);

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f));
        }

        for (int i = 1; i <= segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i == segments ? 1 : i + 1);
            triangles.Add(i);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Circle";
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildSceneArt()
    {
        usingStoreIllustration = false;
        Texture2D storeIllustration = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreIllustrationPath);
        if (storeIllustration != null)
        {
            usingStoreIllustration = true;
            BuildStoreIllustrationScene(storeIllustration);
            return;
        }

        Material wall = MaterialAsset("DungeonWall", new Color(0.075f, 0.084f, 0.092f, 1f));
        Material wallAlt = MaterialAsset("DungeonWallAlt", new Color(0.105f, 0.115f, 0.12f, 1f));
        Material floor = MaterialAsset("DungeonFloor", new Color(0.09f, 0.075f, 0.066f, 1f));
        Material floorAlt = MaterialAsset("DungeonFloorAlt", new Color(0.12f, 0.095f, 0.075f, 1f));
        Material wood = MaterialAsset("CounterWood", new Color(0.26f, 0.13f, 0.052f, 1f));
        Material darkWood = MaterialAsset("DarkWood", new Color(0.12f, 0.065f, 0.028f, 1f));
        Material skin = MaterialAsset("GoblinSkin", new Color(0.22f, 0.55f, 0.18f, 1f));
        Material skinDark = MaterialAsset("GoblinShadow", new Color(0.055f, 0.16f, 0.055f, 1f));
        Material nose = MaterialAsset("GoblinNose", new Color(0.56f, 0.72f, 0.22f, 1f));
        Material ink = MaterialAsset("InkOutline", new Color(0.018f, 0.015f, 0.011f, 1f));
        Material eye = MaterialAsset("EyeBlack", new Color(0.01f, 0.008f, 0.005f, 1f));
        Material cloth = MaterialAsset("ShopCloth", new Color(0.42f, 0.055f, 0.045f, 1f));
        Material sign = MaterialAsset("OldSign", new Color(0.42f, 0.26f, 0.11f, 1f));
        Material gold = MaterialAsset("Gold", new Color(0.93f, 0.65f, 0.16f, 1f));
        Material red = MaterialAsset("PotionRed", new Color(0.78f, 0.08f, 0.08f, 1f));
        Material blue = MaterialAsset("PotionBlue", new Color(0.12f, 0.36f, 0.82f, 1f));
        Material green = MaterialAsset("PotionGreen", new Color(0.18f, 0.68f, 0.38f, 1f));
        Material glass = MaterialAsset("BottleGlass", new Color(0.62f, 0.85f, 0.78f, 0.85f));
        Material flame = MaterialAsset("Flame", new Color(1f, 0.47f, 0.08f, 1f));
        Material flameCore = MaterialAsset("FlameCore", new Color(1f, 0.86f, 0.28f, 1f));
        Material shadow = MaterialAsset("SoftShadow", new Color(0f, 0f, 0f, 0.42f));
        Material bone = MaterialAsset("Bone", new Color(0.72f, 0.66f, 0.5f, 1f));

        Rect("BackWall_Base", new Vector3(0f, 0.8f, 1.6f), new Vector3(16f, 8.6f, 1f), wall);
        Rect("Floor_Base", new Vector3(0f, -3.15f, 1.4f), new Vector3(16f, 3.1f, 1f), floor);
        Rect("BackArch_Shadow", new Vector3(0f, 0.05f, 1.2f), new Vector3(5.8f, 5.6f, 1f), ink);
        Circle("BackArch_Top", new Vector3(0f, 2.85f, 1.15f), new Vector3(5.8f, 2.3f, 1f), ink);
        Rect("BackArch_Inner", new Vector3(0f, -0.05f, 1.05f), new Vector3(5.2f, 5.2f, 1f), wall);
        Circle("BackArch_InnerTop", new Vector3(0f, 2.65f, 1f), new Vector3(5.2f, 2.05f, 1f), wallAlt);

        for (int row = 0; row < 6; row++)
        {
            float y = 3.9f - row * 0.86f;
            float offset = row % 2 == 0 ? -7.2f : -6.55f;
            for (int col = 0; col < 11; col++)
            {
                float x = offset + col * 1.42f;
                Material material = (row + col) % 3 == 0 ? wallAlt : wall;
                Rect("WallStone_" + row + "_" + col, new Vector3(x, y, 0.88f), new Vector3(1.28f, 0.66f, 1f), material);
            }
        }

        for (int i = 0; i < 12; i++)
        {
            float x = -7.3f + i * 1.34f;
            Rect("FloorSlab_Back_" + i, new Vector3(x, -2.35f, 0.78f), new Vector3(1.15f, 0.48f, 1f), i % 2 == 0 ? floor : floorAlt);
            Rect("FloorSlab_Front_" + i, new Vector3(x + 0.34f, -3.05f, 0.75f), new Vector3(1.2f, 0.5f, 1f), i % 2 == 0 ? floorAlt : floor);
        }

        Rect("ShopAwning_Back", new Vector3(0f, 1.6f, -0.15f), new Vector3(6.3f, 0.35f, 1f), darkWood);
        for (int i = 0; i < 7; i++)
        {
            Triangle("AwningPennant_" + i, new Vector3(-2.7f + i * 0.9f, 1.24f, -0.25f), new Vector3(0.75f, 0.72f, 1f), i % 2 == 0 ? cloth : sign, 180f);
        }

        Rect("ShopCounter_Shadow", new Vector3(0f, -2.3f, -0.65f), new Vector3(7.3f, 0.35f, 1f), shadow);
        Rect("Counter_Back", new Vector3(0f, -1.95f, -0.7f), new Vector3(7.1f, 1.25f, 1f), darkWood);
        Rect("Counter_Front", new Vector3(0f, -2.2f, -0.9f), new Vector3(6.8f, 1.05f, 1f), wood);
        for (int i = 0; i < 6; i++)
        {
            Rect("Counter_Plank_" + i, new Vector3(-2.85f + i * 1.14f, -2.2f, -1f), new Vector3(0.08f, 1.02f, 1f), darkWood);
        }

        Rect("Counter_Top", new Vector3(0f, -1.55f, -1.05f), new Vector3(7.25f, 0.26f, 1f), sign);
        Rect("ShopSign", new Vector3(0f, 2.35f, -0.45f), new Vector3(3.1f, 0.72f, 1f), sign);
        Rect("ShopSign_TopEdge", new Vector3(0f, 2.72f, -0.55f), new Vector3(3.25f, 0.08f, 1f), darkWood);
        Text("ShopSign_Text", "ODD GOODS", new Vector3(0f, 2.25f, -0.75f), 0.28f, new Color(0.11f, 0.055f, 0.02f, 1f));

        BuildShelves(wood, darkWood, red, blue, green, glass, bone);
        BuildGoblin(skin, skinDark, nose, eye, cloth, bone, shadow);
        BuildCounterDetails(gold, bone, ink);
        BuildTorches(darkWood, flame, flameCore, shadow);

        Rect("LeftPillar", new Vector3(-7.45f, 0.05f, -1.8f), new Vector3(0.85f, 7.9f, 1f), ink);
        Rect("RightPillar", new Vector3(7.45f, 0.05f, -1.8f), new Vector3(0.85f, 7.9f, 1f), ink);
        Rect("CeilingBeam", new Vector3(0f, 4.58f, -1.9f), new Vector3(16f, 0.7f, 1f), ink);
        Circle("Foreground_Vignette_Left", new Vector3(-7.9f, -3.35f, -2f), new Vector3(4.2f, 2.1f, 1f), shadow);
        Circle("Foreground_Vignette_Right", new Vector3(7.9f, -3.35f, -2f), new Vector3(4.2f, 2.1f, 1f), shadow);
    }

    private static void BuildStoreIllustrationScene(Texture2D illustration)
    {
        Texture2D highlightIllustration = AssetDatabase.LoadAssetAtPath<Texture2D>(StoreHighlightIllustrationPath);
        Material image = TexturedMaterialAsset("StoreIllustration", illustration);
        Material backplate = MaterialAsset("StoreIllustrationBackplate", new Color(0.012f, 0.01f, 0.008f, 1f));
        Material frame = MaterialAsset("StoreIllustrationFrame", new Color(0.008f, 0.006f, 0.004f, 0.72f));
        Material vignette = MaterialAsset("StoreIllustrationVignette", new Color(0f, 0f, 0f, 0.22f));

        float imageHeight = 10f;
        float imageWidth = imageHeight * illustration.width / Mathf.Max(1f, illustration.height);

        Rect("StoreIllustration_Backplate", new Vector3(0f, 0f, 0.16f), new Vector3(imageWidth + 0.32f, imageHeight + 0.32f, 1f), backplate);
        GameObject backdrop = Rect("StoreIllustration_Backdrop", new Vector3(0f, 0f, 0f), new Vector3(imageWidth, imageHeight, 1f), image);

        Rect("StoreIllustration_TopShade", new Vector3(0f, imageHeight * 0.5f - 0.2f, -0.16f), new Vector3(imageWidth, 0.42f, 1f), frame);
        Rect("StoreIllustration_BottomShade", new Vector3(0f, -imageHeight * 0.5f + 0.18f, -0.16f), new Vector3(imageWidth, 0.36f, 1f), frame);
        Circle("StoreIllustration_LeftVignette", new Vector3(-imageWidth * 0.54f, -3.4f, -0.22f), new Vector3(4.3f, 2.5f, 1f), vignette);
        Circle("StoreIllustration_RightVignette", new Vector3(imageWidth * 0.54f, -3.4f, -0.22f), new Vector3(4.3f, 2.5f, 1f), vignette);

        BuildCounterHotspot(illustration, highlightIllustration, backdrop.GetComponent<MeshRenderer>(), imageWidth, imageHeight);
        BuildExitButton(imageWidth, imageHeight);
    }

    private static void BuildShelfViewScene(Texture2D illustration)
    {
        Material image = TexturedMaterialAsset("ShelfIllustration", illustration);
        Material backplate = MaterialAsset("ShelfIllustrationBackplate", new Color(0.012f, 0.01f, 0.008f, 1f));
        Material vignette = MaterialAsset("ShelfIllustrationVignette", new Color(0f, 0f, 0f, 0.18f));

        float imageHeight = 10f;
        float imageWidth = imageHeight * illustration.width / Mathf.Max(1f, illustration.height);

        Rect("ShelfIllustration_Backplate", new Vector3(0f, 0f, 0.16f), new Vector3(imageWidth + 0.36f, imageHeight + 0.36f, 1f), backplate);
        Rect("ShelfIllustration_Backdrop", new Vector3(0f, 0f, 0f), new Vector3(imageWidth, imageHeight, 1f), image);
        Circle("ShelfIllustration_LeftVignette", new Vector3(-imageWidth * 0.32f, -2.8f, -0.22f), new Vector3(3.1f, 2.6f, 1f), vignette);
        Circle("ShelfIllustration_RightVignette", new Vector3(imageWidth * 0.5f, -2.6f, -0.22f), new Vector3(3.5f, 2.8f, 1f), vignette);

        BuildMerchantDialogueBox(imageWidth, imageHeight);
        BuildExitButton(imageWidth, imageHeight);
    }

    private static void BuildCounterHotspot(Texture2D illustration, Texture2D highlightIllustration, Renderer backdropRenderer, float imageWidth, float imageHeight)
    {
        Vector2[] points = ConvertImagePixelsToWorldPoints(CounterHotspotPixels, illustration.width, illustration.height, imageWidth, imageHeight);

        GameObject hotspot = new GameObject("StoreCounterHotspot");
        hotspot.transform.SetParent(artRoot, false);
        hotspot.transform.localPosition = new Vector3(0f, 0f, -0.42f);

        PolygonCollider2D collider = hotspot.AddComponent<PolygonCollider2D>();
        collider.points = points;

        StoreHotspotSceneLoader loader = hotspot.AddComponent<StoreHotspotSceneLoader>();
        SerializedObject serializedLoader = new SerializedObject(loader);
        serializedLoader.FindProperty("m_TargetSceneName").stringValue = ShelfSceneName;
        serializedLoader.FindProperty("m_TextureSwapRenderer").objectReferenceValue = backdropRenderer;
        serializedLoader.FindProperty("m_NormalTexture").objectReferenceValue = illustration;
        serializedLoader.FindProperty("m_HoverTexture").objectReferenceValue = highlightIllustration;
        serializedLoader.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildExitButton(float imageWidth, float imageHeight)
    {
        Material shadowMaterial = MaterialAsset("StoreExitButtonShadow", new Color(0.015f, 0.011f, 0.008f, 0.88f));
        Material frameMaterial = MaterialAsset("StoreExitButtonFrame", new Color(0.055f, 0.034f, 0.02f, 0.96f));
        Material edgeMaterial = MaterialAsset("StoreExitButtonPixelEdge", new Color(0.82f, 0.56f, 0.22f, 1f));
        Material fillMaterial = MaterialAsset("StoreExitButtonInset", new Color(0.16f, 0.085f, 0.038f, 0.94f));
        Material highlightMaterial = MaterialAsset("StoreExitButtonTopLight", new Color(1f, 0.78f, 0.34f, 0.92f));
        float x = -imageWidth * 0.5f + 0.92f;
        float y = imageHeight * 0.5f - 0.55f;

        GameObject button = new GameObject("StoreExitButton");
        button.transform.SetParent(artRoot, false);
        button.transform.localPosition = new Vector3(x, y, -1.45f);

        ParentTo(Rect("StoreExitButton_Shadow", new Vector3(x + 0.07f, y - 0.07f, -1.42f), new Vector3(1.44f, 0.7f, 1f), shadowMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_Frame", new Vector3(x, y, -1.48f), new Vector3(1.38f, 0.64f, 1f), frameMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_Fill", new Vector3(x, y, -1.54f), new Vector3(1.1f, 0.4f, 1f), fillMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_TopEdge", new Vector3(x, y + 0.25f, -1.6f), new Vector3(0.92f, 0.08f, 1f), highlightMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_BottomEdge", new Vector3(x, y - 0.25f, -1.6f), new Vector3(0.92f, 0.08f, 1f), edgeMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_LeftEdge", new Vector3(x - 0.55f, y, -1.6f), new Vector3(0.08f, 0.34f, 1f), edgeMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_RightEdge", new Vector3(x + 0.55f, y, -1.6f), new Vector3(0.08f, 0.34f, 1f), edgeMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_CornerA", new Vector3(x - 0.62f, y + 0.25f, -1.61f), new Vector3(0.14f, 0.14f, 1f), frameMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_CornerB", new Vector3(x + 0.62f, y + 0.25f, -1.61f), new Vector3(0.14f, 0.14f, 1f), frameMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_CornerC", new Vector3(x - 0.62f, y - 0.25f, -1.61f), new Vector3(0.14f, 0.14f, 1f), frameMaterial), button.transform);
        ParentTo(Rect("StoreExitButton_CornerD", new Vector3(x + 0.62f, y - 0.25f, -1.61f), new Vector3(0.14f, 0.14f, 1f), frameMaterial), button.transform);
        ParentTo(Text("StoreExitButton_LabelShadow", "\u79bb\u5f00", new Vector3(x + 0.025f, y - 0.04f, -1.72f), 0.18f, new Color(0.035f, 0.02f, 0.012f, 1f)), button.transform);
        ParentTo(Text("StoreExitButton_Label", "\u79bb\u5f00", new Vector3(x, y - 0.025f, -1.76f), 0.18f, new Color(1f, 0.84f, 0.45f, 1f)), button.transform);

        BoxCollider2D collider = button.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.38f, 0.64f);

        StoreHotspotSceneLoader loader = button.AddComponent<StoreHotspotSceneLoader>();
        SerializedObject serializedLoader = new SerializedObject(loader);
        serializedLoader.FindProperty("m_TargetSceneName").stringValue = ReturnSceneName;
        serializedLoader.FindProperty("m_RequestMapInkPrintBeforeLoad").boolValue = true;
        serializedLoader.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildMerchantDialogueBox(float imageWidth, float imageHeight)
    {
        Material shadowMaterial = MaterialAsset("StoreDialogueShadow", new Color(0.015f, 0.01f, 0.007f, 0.78f));
        Material frameMaterial = MaterialAsset("StoreDialogueFrame", new Color(0.06f, 0.035f, 0.018f, 0.96f));
        Material fillMaterial = MaterialAsset("StoreDialogueFill", new Color(0.31f, 0.18f, 0.09f, 0.92f));
        Material insetMaterial = MaterialAsset("StoreDialogueInset", new Color(0.12f, 0.07f, 0.04f, 0.96f));
        Material edgeMaterial = MaterialAsset("StoreDialoguePixelEdge", new Color(0.84f, 0.58f, 0.24f, 0.95f));

        float x = 1.45f;
        float y = imageHeight * 0.5f - 0.75f;
        float width = Mathf.Min(6.8f, imageWidth - 2.5f);
        float height = 0.86f;

        GameObject bubble = new GameObject("StoreMerchantDialogue");
        bubble.transform.SetParent(artRoot, false);
        bubble.transform.localPosition = new Vector3(x, y, -1.45f);

        ParentTo(Rect("StoreMerchantDialogue_Shadow", new Vector3(x + 0.09f, y - 0.09f, -1.42f), new Vector3(width + 0.2f, height + 0.18f, 1f), shadowMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_Frame", new Vector3(x, y, -1.5f), new Vector3(width, height, 1f), frameMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_Fill", new Vector3(x, y, -1.56f), new Vector3(width - 0.28f, height - 0.24f, 1f), fillMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_Inset", new Vector3(x, y - 0.02f, -1.61f), new Vector3(width - 0.55f, height - 0.44f, 1f), insetMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_TopEdge", new Vector3(x, y + height * 0.5f - 0.1f, -1.65f), new Vector3(width - 0.75f, 0.08f, 1f), edgeMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_BottomEdge", new Vector3(x, y - height * 0.5f + 0.1f, -1.65f), new Vector3(width - 0.75f, 0.08f, 1f), edgeMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_LeftEdge", new Vector3(x - width * 0.5f + 0.13f, y, -1.65f), new Vector3(0.08f, height - 0.28f, 1f), edgeMaterial), bubble.transform);
        ParentTo(Rect("StoreMerchantDialogue_RightEdge", new Vector3(x + width * 0.5f - 0.13f, y, -1.65f), new Vector3(0.08f, height - 0.28f, 1f), edgeMaterial), bubble.transform);

        float tailX = 0.15f;
        float tailY = y - height * 0.5f - 0.24f;
        ParentTo(Triangle("StoreMerchantDialogue_TailShadow", new Vector3(tailX + 0.07f, tailY - 0.05f, -1.44f), new Vector3(0.86f, 0.66f, 1f), shadowMaterial, 180f), bubble.transform);
        ParentTo(Triangle("StoreMerchantDialogue_TailFrame", new Vector3(tailX, tailY, -1.58f), new Vector3(0.8f, 0.6f, 1f), frameMaterial, 180f), bubble.transform);
        ParentTo(Triangle("StoreMerchantDialogue_TailEdge", new Vector3(tailX, tailY + 0.03f, -1.63f), new Vector3(0.62f, 0.46f, 1f), edgeMaterial, 180f), bubble.transform);
        ParentTo(Triangle("StoreMerchantDialogue_TailFill", new Vector3(tailX, tailY + 0.07f, -1.69f), new Vector3(0.42f, 0.3f, 1f), fillMaterial, 180f), bubble.transform);

        GameObject textRoot = new GameObject("StoreMerchantDialogue_TextFloat");
        textRoot.transform.SetParent(bubble.transform, false);
        textRoot.transform.localPosition = Vector3.zero;
        ParentTo(Text("StoreMerchantDialogue_TextShadow", "\u770b\u770b\u4e70\u4e9b\u4ec0\u4e48\u5427........", new Vector3(x + 0.035f, y - 0.055f, -1.72f), 0.23f, new Color(0.035f, 0.022f, 0.014f, 1f)), textRoot.transform);
        ParentTo(Text("StoreMerchantDialogue_Text", "\u770b\u770b\u4e70\u4e9b\u4ec0\u4e48\u5427........", new Vector3(x, y - 0.035f, -1.77f), 0.23f, new Color(1f, 0.86f, 0.55f, 1f)), textRoot.transform);

        StoreFloatingText floatingText = textRoot.AddComponent<StoreFloatingText>();
        SerializedObject serializedFloatingText = new SerializedObject(floatingText);
        serializedFloatingText.FindProperty("m_FloatAmplitude").vector2Value = new Vector2(0.024f, 0.03f);
        serializedFloatingText.FindProperty("m_FloatSpeed").floatValue = 2.4f;
        serializedFloatingText.FindProperty("m_JitterAmount").floatValue = 0.008f;
        serializedFloatingText.FindProperty("m_JitterSpeed").floatValue = 9.5f;
        serializedFloatingText.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ParentTo(GameObject child, Transform parent)
    {
        child.transform.SetParent(parent, true);
    }

    private static Vector2[] ConvertImagePixelsToWorldPoints(Vector2[] pixelPoints, int textureWidth, int textureHeight, float imageWidth, float imageHeight)
    {
        Vector2[] points = new Vector2[pixelPoints.Length];
        for (int i = 0; i < pixelPoints.Length; i++)
        {
            float x = (pixelPoints[i].x / Mathf.Max(1f, textureWidth) - 0.5f) * imageWidth;
            float y = (0.5f - pixelPoints[i].y / Mathf.Max(1f, textureHeight)) * imageHeight;
            points[i] = new Vector2(x, y);
        }

        return points;
    }

    private static Mesh CreatePolygonFanMesh(string name, Vector2[] points)
    {
        Vector3[] vertices = new Vector3[points.Length + 1];
        int[] triangles = new int[points.Length * 3];
        Vector2 center = Vector2.zero;

        for (int i = 0; i < points.Length; i++)
        {
            center += points[i];
            vertices[i + 1] = new Vector3(points[i].x, points[i].y, 0f);
        }

        center /= Mathf.Max(1, points.Length);
        vertices[0] = new Vector3(center.x, center.y, 0f);

        for (int i = 0; i < points.Length; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i == points.Length - 1 ? 1 : i + 2;
            triangles[triangleIndex + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildShelves(Material wood, Material darkWood, Material red, Material blue, Material green, Material glass, Material bone)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float xBase = side * 4.6f;
            Rect("Shelf_Post_A_" + side, new Vector3(xBase - side * 1.1f, 0.15f, -0.35f), new Vector3(0.16f, 2.95f, 1f), darkWood);
            Rect("Shelf_Post_B_" + side, new Vector3(xBase + side * 1.1f, 0.15f, -0.35f), new Vector3(0.16f, 2.95f, 1f), darkWood);
            for (int level = 0; level < 3; level++)
            {
                float y = -0.95f + level * 0.95f;
                Rect("Shelf_Board_" + side + "_" + level, new Vector3(xBase, y, -0.45f), new Vector3(2.45f, 0.18f, 1f), wood);
                for (int b = 0; b < 3; b++)
                {
                    float bx = xBase - 0.72f + b * 0.72f;
                    Material potion = (level + b + side) % 3 == 0 ? red : ((level + b) % 3 == 1 ? blue : green);
                    Rect("Bottle_Body_" + side + "_" + level + "_" + b, new Vector3(bx, y + 0.24f, -0.65f), new Vector3(0.22f, 0.42f, 1f), potion);
                    Rect("Bottle_Neck_" + side + "_" + level + "_" + b, new Vector3(bx, y + 0.49f, -0.66f), new Vector3(0.12f, 0.16f, 1f), glass);
                    Circle("Bottle_Stopper_" + side + "_" + level + "_" + b, new Vector3(bx, y + 0.6f, -0.68f), new Vector3(0.13f, 0.13f, 1f), bone);
                }
            }
        }
    }

    private static void BuildGoblin(Material skin, Material skinDark, Material nose, Material eye, Material cloth, Material bone, Material shadow)
    {
        Circle("Goblin_Shadow", new Vector3(0f, -1.35f, -1.05f), new Vector3(1.8f, 0.42f, 1f), shadow);
        Circle("Goblin_Body", new Vector3(0f, -0.86f, -1.15f), new Vector3(1.25f, 1.35f, 1f), skinDark);
        Circle("Goblin_Head", new Vector3(0f, 0.05f, -1.3f), new Vector3(1.18f, 1.02f, 1f), skin);
        Triangle("Goblin_LeftEar", new Vector3(-0.72f, 0.08f, -1.25f), new Vector3(0.72f, 0.42f, 1f), skin, 90f);
        Triangle("Goblin_RightEar", new Vector3(0.72f, 0.08f, -1.25f), new Vector3(0.72f, 0.42f, 1f), skin, -90f);
        Triangle("Goblin_Nose", new Vector3(0.02f, -0.05f, -1.45f), new Vector3(0.3f, 0.32f, 1f), nose, 180f);
        Circle("Goblin_LeftEye", new Vector3(-0.23f, 0.18f, -1.5f), new Vector3(0.12f, 0.16f, 1f), eye);
        Circle("Goblin_RightEye", new Vector3(0.25f, 0.18f, -1.5f), new Vector3(0.12f, 0.16f, 1f), eye);
        Rect("Goblin_Mouth", new Vector3(0.05f, -0.28f, -1.52f), new Vector3(0.48f, 0.07f, 1f), eye);
        Triangle("Goblin_Tooth_A", new Vector3(-0.1f, -0.34f, -1.55f), new Vector3(0.08f, 0.12f, 1f), bone, 180f);
        Triangle("Goblin_Tooth_B", new Vector3(0.22f, -0.34f, -1.55f), new Vector3(0.08f, 0.12f, 1f), bone, 180f);
        Rect("Goblin_Apron", new Vector3(0f, -0.78f, -1.5f), new Vector3(0.72f, 0.78f, 1f), cloth);
        Rect("Goblin_LeftArm", new Vector3(-0.74f, -0.67f, -1.35f), new Vector3(0.3f, 0.9f, 1f), skin);
        Rect("Goblin_RightArm", new Vector3(0.82f, -0.72f, -1.35f), new Vector3(0.3f, 0.88f, 1f), skin);
        Circle("Goblin_LeftHand", new Vector3(-0.86f, -1.17f, -1.48f), new Vector3(0.28f, 0.2f, 1f), skin);
        Circle("Goblin_RightHand", new Vector3(0.96f, -1.18f, -1.48f), new Vector3(0.28f, 0.2f, 1f), skin);
    }

    private static void BuildCounterDetails(Material gold, Material bone, Material ink)
    {
        for (int i = 0; i < 8; i++)
        {
            Circle("Coin_" + i, new Vector3(-2.25f + i * 0.16f, -1.36f + (i % 2) * 0.04f, -1.65f), new Vector3(0.12f, 0.12f, 1f), gold);
        }

        Rect("Ledger", new Vector3(2.35f, -1.34f, -1.58f), new Vector3(0.78f, 0.44f, 1f), bone);
        Rect("Ledger_Line_A", new Vector3(2.35f, -1.27f, -1.68f), new Vector3(0.58f, 0.025f, 1f), ink);
        Rect("Ledger_Line_B", new Vector3(2.35f, -1.38f, -1.68f), new Vector3(0.48f, 0.025f, 1f), ink);
        Rect("Knife", new Vector3(-1.92f, -1.32f, -1.6f), new Vector3(0.72f, 0.08f, 1f), bone);
        Triangle("Knife_Tip", new Vector3(-1.48f, -1.32f, -1.63f), new Vector3(0.22f, 0.16f, 1f), bone, -90f);
    }

    private static void BuildTorches(Material darkWood, Material flame, Material flameCore, Material shadow)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 6.2f;
            Rect("Torch_Handle_" + side, new Vector3(x, 1.25f, -0.85f), new Vector3(0.16f, 0.88f, 1f), darkWood);
            Triangle("Torch_Flame_Outer_" + side, new Vector3(x, 1.82f, -1.1f), new Vector3(0.62f, 0.96f, 1f), flame, 0f);
            Triangle("Torch_Flame_Inner_" + side, new Vector3(x, 1.78f, -1.2f), new Vector3(0.34f, 0.58f, 1f), flameCore, 0f);
            Circle("Torch_Glow_" + side, new Vector3(x, 1.66f, 0.1f), new Vector3(2.25f, 2.25f, 1f), shadow);

            GameObject lightObject = new GameObject("Torch_PointLight_" + side);
            lightObject.transform.SetParent(artRoot, false);
            lightObject.transform.localPosition = new Vector3(x, 1.65f, -3f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.22f, 1f);
            light.intensity = 1.2f;
            light.range = 5f;
        }
    }

    private static Material MaterialAsset(string name, Color color)
    {
        string path = MaterialsRoot + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        ConfigureMaterialTransparency(material, color.a);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material TexturedMaterialAsset(string name, Texture2D texture)
    {
        return TexturedMaterialAsset(name, texture, Vector2.one, Vector2.zero);
    }

    private static Material TexturedMaterialAsset(string name, Texture2D texture, Vector2 scale, Vector2 offset)
    {
        Material material = MaterialAsset(name, Color.white);
        material.mainTexture = texture;
        material.mainTextureScale = scale;
        material.mainTextureOffset = offset;

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTextureOffset("_BaseMap", offset);
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", scale);
            material.SetTextureOffset("_MainTex", offset);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureMaterialTransparency(Material material, float alpha)
    {
        bool transparent = alpha < 0.999f;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", transparent ? 0f : 1f);
        }

        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
        }
    }

    private static GameObject Rect(string name, Vector3 position, Vector3 scale, Material material)
    {
        return AddMesh(name, quadMesh, position, scale, 0f, material);
    }

    private static GameObject Circle(string name, Vector3 position, Vector3 scale, Material material)
    {
        return AddMesh(name, circleMesh, position, scale, 0f, material);
    }

    private static GameObject Triangle(string name, Vector3 position, Vector3 scale, Material material, float rotationZ)
    {
        return AddMesh(name, triangleMesh, position, scale, rotationZ, material);
    }

    private static GameObject AddMesh(string name, Mesh mesh, Vector3 position, Vector3 scale, float rotationZ, Material material)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(artRoot, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localScale = scale;
        gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return gameObject;
    }

    private static GameObject Text(string name, string text, Vector3 position, float characterSize, Color color)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(artRoot, false);
        gameObject.transform.localPosition = position;

        TextMesh textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = characterSize;
        textMesh.color = color;
        return gameObject;
    }

    private static void EnsureShopScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        AddBuildSceneIfMissing(scenes, ScenePath);
        AddBuildSceneIfMissing(scenes, ShelfScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildSceneIfMissing(List<EditorBuildSettingsScene> scenes, string scenePath)
    {
        AssetDatabase.ImportAsset(scenePath);

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == scenePath)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
    }

    private static void ConfigureCameraAndLighting()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = usingStoreIllustration ? new Vector3(0f, 0f, -10f) : new Vector3(0f, 0.12f, -10f);

        camera.transform.rotation = Quaternion.identity;
        camera.orthographic = true;
        camera.orthographicSize = usingStoreIllustration ? 5f : 4.9f;
        camera.backgroundColor = new Color(0.018f, 0.015f, 0.012f, 1f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        if (camera.GetComponent<AudioListener>() == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.15f, 0.12f, 1f);
    }
}
