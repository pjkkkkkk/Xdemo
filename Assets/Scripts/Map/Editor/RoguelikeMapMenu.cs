#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoguelikeMapMenu
{
    private const string GeneratorName = "RoguelikeMapGenerator";
    private const string TabletopGeneratorName = "Parchment Roguelike Map";

    [MenuItem("Tools/Roguelike Map/Create Generator In Scene")]
    public static void CreateGeneratorInScene()
    {
        RoguelikeMapGenerator generator = Object.FindFirstObjectByType<RoguelikeMapGenerator>();
        if (generator == null)
        {
            GameObject generatorObject = new GameObject(GeneratorName);
            Undo.RegisterCreatedObjectUndo(generatorObject, "Create Roguelike Map Generator");
            generatorObject.transform.position = new Vector3(0f, -2.3f, 6f);
            generator = generatorObject.AddComponent<RoguelikeMapGenerator>();
        }

        generator.GenerateMap();
        Selection.activeGameObject = generator.gameObject;
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
    }

    [MenuItem("Tools/Roguelike Map/Create Parchment Map On Table")]
    public static void CreateParchmentMapOnTable()
    {
        RoguelikeMapGenerator generator = FindOrCreateGenerator(TabletopGeneratorName);
        Bounds tableBounds = FindTableBounds();
        Vector3 mapPosition = new Vector3(tableBounds.center.x, tableBounds.max.y + 0.018f, tableBounds.center.z);

        Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Create Parchment Roguelike Map On Table");
        generator.gameObject.name = TabletopGeneratorName;
        generator.SetupAsTabletopParchment(mapPosition, Quaternion.Euler(-90f, 0f, 0f));
        generator.GenerateMap();

        Selection.activeGameObject = generator.gameObject;
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
    }

    [MenuItem("Tools/Roguelike Map/Regenerate Selected Map")]
    public static void RegenerateSelectedMap()
    {
        RoguelikeMapGenerator generator = Selection.activeGameObject == null
            ? null
            : Selection.activeGameObject.GetComponent<RoguelikeMapGenerator>();

        if (generator == null)
        {
            Debug.LogWarning("Select a GameObject with RoguelikeMapGenerator first.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Regenerate Roguelike Map");
        generator.GenerateMap();
        EditorUtility.SetDirty(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
    }

    private static RoguelikeMapGenerator FindOrCreateGenerator(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        RoguelikeMapGenerator generator = existing == null ? null : existing.GetComponent<RoguelikeMapGenerator>();
        if (generator != null)
        {
            return generator;
        }

        GameObject generatorObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(generatorObject, "Create Roguelike Map Generator");
        return generatorObject.AddComponent<RoguelikeMapGenerator>();
    }

    private static Bounds FindTableBounds()
    {
        GameObject tableRoot = GameObject.Find("TableRoot");
        if (tableRoot != null && TryGetRendererBounds(tableRoot, out Bounds tableBounds))
        {
            return tableBounds;
        }

        if (TryGetNamedRendererBounds(new[] { "NorthDeck", "SouthDeck", "WestDeck", "EastDeck", "RecessBase" }, out Bounds deckBounds))
        {
            return deckBounds;
        }

        return new Bounds(new Vector3(0f, 0.86f, 0f), new Vector3(1.6f, 0.1f, 4.55f));
    }

    private static bool TryGetNamedRendererBounds(string[] names, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds();

        for (int i = 0; i < names.Length; i++)
        {
            GameObject target = GameObject.Find(names[i]);
            if (target == null)
            {
                continue;
            }

            Renderer renderer = target.GetComponent<Renderer>();
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

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        bounds = new Bounds();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }
}
#endif
