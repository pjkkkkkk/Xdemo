using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class OutOfMatchDndSceneBuilder : MonoBehaviour
{
    private const string c_GeneratedRootName = "GeneratedScene";
    private const string c_DefaultUrpUnlitShader = "Universal Render Pipeline/Unlit";
    private const string c_DefaultUrpLitShader = "Universal Render Pipeline/Lit";
    private const string c_StandardShader = "Standard";
    private const string c_GeneratedAssetsFolder = "Assets/Generated";
    private const string c_GeneratedSceneFolder = "Assets/Generated/OutOfMatchScene";
    private const string c_GeneratedMaterialsFolder = "Assets/Generated/OutOfMatchScene/Materials";

    [Header("Build Controls")]
    [SerializeField] private bool m_RebuildOnAwake = true;
    [SerializeField] private bool m_ClearExistingChildrenOnBuild = true;

    [Header("Imported Dealer")]
    [SerializeField] private string m_DemonObjectName = "devil_boss";
    [SerializeField] private string m_DemonSourceMaterialPath = "Assets/TripoModels/fantasy_necromancer_3d_model/Materials/fantasy+necromancer+3d+model_basecolor.mat";
    [SerializeField] private string[] m_ExtraModelNamesToHide = { "npc_1", "fallen wool", "dragon" };
    [SerializeField, Range(0.85f, 1.8f)] private float m_DemonScale = 1.28f;
    [SerializeField] private bool m_PreserveImportedDemonRotation = true;
    [SerializeField, Range(-180f, 180f)] private float m_DemonYawOffset = 180f;
    [SerializeField, Range(0.28f, 0.9f)] private float m_DemonSeatHeight = 0.46f;
    [SerializeField, Range(0.45f, 1.35f)] private float m_DemonBackOffset = 0.76f;

    [Header("Room Layout")]
    [SerializeField, Range(4.8f, 8f)] private float m_RoomWidth = 5.8f;
    [SerializeField, Range(6.2f, 11f)] private float m_RoomDepth = 8.4f;
    [SerializeField, Range(2.5f, 4.2f)] private float m_RoomHeight = 3f;

    [Header("Table Layout")]
    [SerializeField, Range(3.6f, 5.4f)] private float m_TableLength = 4.55f;
    [SerializeField, Range(1.2f, 2.4f)] private float m_TableWidth = 1.58f;
    [SerializeField, Range(0.74f, 1f)] private float m_TableHeight = 0.86f;
    [SerializeField, Range(0.12f, 0.32f)] private float m_RecessDepth = 0.17f;
    [SerializeField, Range(2f, 4.2f)] private float m_RecessLength = 3.16f;
    [SerializeField, Range(0.56f, 1.62f)] private float m_RecessWidth = 0.8f;
    [SerializeField, Range(0.14f, 0.34f)] private float m_TableTopThickness = 0.09f;
    [SerializeField, Range(0.16f, 0.42f)] private float m_TableApronHeight = 0.26f;

    [Header("Tabletop Map")]
    [SerializeField] private bool m_CreateTabletopMap = true;
    [SerializeField] private Texture2D m_TabletopMapIconAtlas;
    [SerializeField] private string m_TabletopMapIconAtlasResourcePath = "Map/NodeIconAtlas";
    [SerializeField, Range(0.02f, 0.16f)] private float m_TabletopMapMargin = 0.05f;
    [SerializeField, Range(0.005f, 0.06f)] private float m_TabletopMapSurfaceLift = 0.028f;
    [SerializeField] private string[] m_QuestionNodeScenePool = new string[0];
    [SerializeField] private string[] m_ShopNodeScenePool = { "GoblinShop2DScene" };
    [SerializeField] private string[] m_CampfireNodeScenePool = new string[0];
    [SerializeField] private string[] m_EliteNodeScenePool = new string[0];
    [SerializeField] private string[] m_BossNodeScenePool = new string[0];

    [Header("Player Miniature")]
    [SerializeField] private string m_WizardObjectName = "wizard";
    [SerializeField] private string m_WizardSourceMaterialPath = "Assets/TripoModels/fantasy_wizard_3d_model/Materials/fantasywizard3dmodel_basecolor.mat";
    [SerializeField, Range(0.05f, 0.45f)] private float m_WizardMiniatureScale = 0.1467f;
    [SerializeField, Range(0f, 0.08f)] private float m_WizardMapSurfaceOffset = 0f;
    [SerializeField] private bool m_UseStableWizardLighting = true;
    [SerializeField] private bool m_FaceWizardTowardDemon = true;
    [SerializeField, Range(-180f, 180f)] private float m_WizardFacingYawOffset = -90f;
    [SerializeField] private bool m_PreserveImportedWizardRotation = true;
    [SerializeField] private Vector3 m_WizardRotation = new Vector3(270f, 0f, 0f);

    [Header("View Layout")]
    [SerializeField, Range(0f, 0.24f)] private float m_PlayerSeatDistance = 0.005f;
    [SerializeField, Range(0.95f, 1.35f)] private float m_PlayerEyeHeight = 1.22f;
    [SerializeField, Range(30f, 60f)] private float m_PlayerCameraFieldOfView = 52f;
    [SerializeField, Range(-10f, 8f)] private float m_CameraPitchOffset = 0f;

    [Header("Lighting")]
    [SerializeField, Range(0f, 0.08f)] private float m_DirectionalIntensity = 0.024f;
    [SerializeField, Range(0.4f, 3.5f)] private float m_CandleLightIntensity = 2.15f;
    [SerializeField, Range(0f, 2.5f)] private float m_TableFillIntensity = 1.35f;
    [SerializeField, Range(0f, 2.5f)] private float m_DealerKeyIntensity = 0.58f;
    [SerializeField, Range(0f, 1.5f)] private float m_DemonRimIntensity = 0.22f;
    [SerializeField] private Color m_AmbientColor = new Color(0.016f, 0.012f, 0.009f, 1f);

    [Header("Palette")]
    [SerializeField] private Color m_WoodDarkColor = new Color(0.09f, 0.055f, 0.028f, 1f);
    [SerializeField] private Color m_WoodMidColor = new Color(0.16f, 0.1f, 0.055f, 1f);
    [SerializeField] private Color m_WoodLightColor = new Color(0.22f, 0.15f, 0.08f, 1f);
    [SerializeField] private Color m_RecessColor = new Color(0.05f, 0.03f, 0.02f, 1f);
    [SerializeField] private Color m_LeatherColor = new Color(0.12f, 0.05f, 0.04f, 1f);
    [SerializeField] private Color m_BoneColor = new Color(0.53f, 0.48f, 0.39f, 1f);
    [SerializeField] private Color m_ParchmentColor = new Color(0.61f, 0.55f, 0.43f, 1f);
    [SerializeField] private Color m_MetalColor = new Color(0.13f, 0.11f, 0.1f, 1f);
    [SerializeField] private Color m_CandleColor = new Color(1f, 0.78f, 0.46f, 1f);
    [SerializeField] private Color m_EmberColor = new Color(0.45f, 0.12f, 0.08f, 1f);
    [SerializeField] private Color m_ShadowColor = new Color(0.025f, 0.018f, 0.014f, 1f);

    private readonly List<Material> m_RuntimeMaterials = new List<Material>(64);
    private Transform m_GeneratedRoot;
    private Camera m_CachedMainCamera;
    private RoguelikeMapGenerator m_TabletopMapGenerator;

    private void Awake()
    {
        if (!m_RebuildOnAwake)
        {
            return;
        }

        BuildScene();
    }

    private void Start()
    {
        if (!m_RebuildOnAwake)
        {
            return;
        }

        if (m_GeneratedRoot == null || m_CachedMainCamera == null)
        {
            BuildScene();
        }
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterials();
    }

    [ContextMenu("Build Scene")]
    public void BuildScene()
    {
        ValidateSettings();
        CacheCameraReference();
        ReleaseRuntimeMaterials();
        PrepareGeneratedRoot();
        ConfigureRenderSettings();

        BuildRoom();
        BuildTable();
        BuildDealerSeat();
        BuildDungeonMasterScreen();
        BuildAtmosphericProps();
        BuildLighting();
        PositionImportedDemon();
        PositionWizardMiniature();
        ConfigurePlayerView();
        SaveGeneratedAssets();

        Debug.Log("[OutOfMatchDndSceneBuilder] Scene build completed.", this);
    }

    private void ValidateSettings()
    {
        if (m_CreateTabletopMap)
        {
            m_TableWidth = Mathf.Max(m_TableWidth, 2.18f);
            m_RecessWidth = Mathf.Max(m_RecessWidth, 1.42f);
        }

        m_RecessLength = Mathf.Min(m_RecessLength, m_TableLength - 0.8f);
        m_RecessWidth = Mathf.Min(m_RecessWidth, m_TableWidth - 0.38f);
        m_RecessDepth = Mathf.Min(m_RecessDepth, m_TableHeight * 0.4f);
        m_TableTopThickness = Mathf.Min(m_TableTopThickness, m_RecessDepth - 0.04f);
        m_TableApronHeight = Mathf.Min(m_TableApronHeight, m_TableHeight - 0.12f);
    }

    private void CacheCameraReference()
    {
        if (m_CachedMainCamera != null)
        {
            return;
        }

        Camera[] temporaryCameras = FindObjectsByType<Camera>();
        for (int i = 0; i < temporaryCameras.Length; i++)
        {
            if (temporaryCameras[i] != null && temporaryCameras[i].CompareTag("MainCamera"))
            {
                m_CachedMainCamera = temporaryCameras[i];
                break;
            }
        }
    }

    private void PrepareGeneratedRoot()
    {
        Transform temporaryExistingRoot = transform.Find(c_GeneratedRootName);
        if (temporaryExistingRoot != null && m_ClearExistingChildrenOnBuild)
        {
            if (Application.isPlaying)
            {
                DisableGeneratedRootCameras(temporaryExistingRoot);
                temporaryExistingRoot.name = $"{c_GeneratedRootName}_Stale";
                if (m_CachedMainCamera != null && m_CachedMainCamera.transform.IsChildOf(temporaryExistingRoot))
                {
                    m_CachedMainCamera = null;
                }

                DestroyTransform(temporaryExistingRoot);

                GameObject temporaryGeneratedRootObject = new GameObject(c_GeneratedRootName);
                temporaryGeneratedRootObject.transform.SetParent(transform, false);
                m_GeneratedRoot = temporaryGeneratedRootObject.transform;
                return;
            }

            DestroyTransform(temporaryExistingRoot);
        }

        temporaryExistingRoot = transform.Find(c_GeneratedRootName);
        if (temporaryExistingRoot == null)
        {
            GameObject temporaryGeneratedRootObject = new GameObject(c_GeneratedRootName);
            temporaryGeneratedRootObject.transform.SetParent(transform, false);
            m_GeneratedRoot = temporaryGeneratedRootObject.transform;
            return;
        }

        m_GeneratedRoot = temporaryExistingRoot;
    }

    private static void DisableGeneratedRootCameras(Transform _root)
    {
        if (_root == null)
        {
            return;
        }

        Camera[] temporaryCameras = _root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < temporaryCameras.Length; i++)
        {
            if (temporaryCameras[i] == null)
            {
                continue;
            }

            temporaryCameras[i].enabled = false;

            AudioListener temporaryAudioListener = temporaryCameras[i].GetComponent<AudioListener>();
            if (temporaryAudioListener != null)
            {
                temporaryAudioListener.enabled = false;
            }
        }
    }

    private void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = m_AmbientColor;
        RenderSettings.reflectionIntensity = 0.2f;
        RenderSettings.fog = false;
        RenderSettings.subtractiveShadowColor = new Color(0.03f, 0.02f, 0.015f, 1f);
    }

    private void BuildRoom()
    {
        Transform temporaryRoomRoot = CreateEmpty("RoomRoot", Vector3.zero, m_GeneratedRoot);
        Material temporaryShadowMaterial = CreateLitMaterial("RoomShadowMat", m_ShadowColor, 0.9f, 0f);
        Material temporaryWallDarkMaterial = CreateLitMaterial("WallDarkMat", m_WoodDarkColor, 0.62f, 0f);
        Material temporaryWallMidMaterial = CreateLitMaterial("WallMidMat", m_WoodMidColor, 0.5f, 0f);
        Material temporaryWallLightMaterial = CreateLitMaterial("WallLightMat", m_WoodLightColor, 0.44f, 0f);

        float halfWidth = m_RoomWidth * 0.5f;
        float halfDepth = m_RoomDepth * 0.5f;

        CreatePrimitive("BackWallCore", PrimitiveType.Cube, new Vector3(0f, m_RoomHeight * 0.5f, halfDepth - 0.08f), new Vector3(m_RoomWidth, m_RoomHeight, 0.16f), Quaternion.identity, temporaryShadowMaterial, temporaryRoomRoot);
        CreatePrimitive("FrontWallCore", PrimitiveType.Cube, new Vector3(0f, m_RoomHeight * 0.5f, -halfDepth + 0.08f), new Vector3(m_RoomWidth, m_RoomHeight, 0.16f), Quaternion.identity, temporaryShadowMaterial, temporaryRoomRoot);
        CreatePrimitive("LeftWallCore", PrimitiveType.Cube, new Vector3(-halfWidth + 0.08f, m_RoomHeight * 0.5f, 0f), new Vector3(0.16f, m_RoomHeight, m_RoomDepth), Quaternion.identity, temporaryShadowMaterial, temporaryRoomRoot);
        CreatePrimitive("RightWallCore", PrimitiveType.Cube, new Vector3(halfWidth - 0.08f, m_RoomHeight * 0.5f, 0f), new Vector3(0.16f, m_RoomHeight, m_RoomDepth), Quaternion.identity, temporaryShadowMaterial, temporaryRoomRoot);
        CreatePrimitive("CeilingCore", PrimitiveType.Cube, new Vector3(0f, m_RoomHeight - 0.06f, 0f), new Vector3(m_RoomWidth, 0.12f, m_RoomDepth), Quaternion.identity, temporaryShadowMaterial, temporaryRoomRoot);

        BuildFloorPlanks(temporaryRoomRoot, temporaryWallDarkMaterial, temporaryWallMidMaterial, temporaryWallLightMaterial);
        BuildWallBoards(temporaryRoomRoot, temporaryWallDarkMaterial, temporaryWallMidMaterial, temporaryWallLightMaterial);
        BuildCeilingBeams(temporaryRoomRoot, temporaryWallDarkMaterial);
    }

    private void BuildFloorPlanks(Transform _parent, Material _darkMaterial, Material _midMaterial, Material _lightMaterial)
    {
        Transform temporaryFloorRoot = CreateEmpty("FloorPlanks", Vector3.zero, _parent);
        int plankCount = 8;
        float plankWidth = m_RoomWidth / plankCount;

        for (int i = 0; i < plankCount; i++)
        {
            float x = (-m_RoomWidth * 0.5f) + (plankWidth * 0.5f) + (plankWidth * i);
            float y = -0.03f + ((i % 2 == 0) ? 0f : 0.004f);
            Material temporaryMaterial = (i % 3 == 0) ? _lightMaterial : (i % 2 == 0 ? _midMaterial : _darkMaterial);
            CreatePrimitive(
                $"FloorPlank_{i}",
                PrimitiveType.Cube,
                new Vector3(x, y, 0f),
                new Vector3(plankWidth - 0.015f, 0.06f, m_RoomDepth - 0.22f),
                Quaternion.identity,
                temporaryMaterial,
                temporaryFloorRoot);
        }
    }

    private void BuildWallBoards(Transform _parent, Material _darkMaterial, Material _midMaterial, Material _lightMaterial)
    {
        Transform temporaryBoardsRoot = CreateEmpty("WallBoards", Vector3.zero, _parent);
        float halfWidth = m_RoomWidth * 0.5f;
        float halfDepth = m_RoomDepth * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            float x = (-m_RoomWidth * 0.5f) + 0.32f + (i * ((m_RoomWidth - 0.64f) / 9f));
            float height = m_RoomHeight - 0.26f + ((i % 2 == 0) ? 0.04f : -0.02f);
            Material temporaryMaterial = (i % 3 == 0) ? _lightMaterial : (i % 2 == 0 ? _midMaterial : _darkMaterial);

            CreatePrimitive(
                $"BackBoard_{i}",
                PrimitiveType.Cube,
                new Vector3(x, height * 0.5f, halfDepth - 0.045f),
                new Vector3(0.32f, height, 0.05f),
                Quaternion.identity,
                temporaryMaterial,
                temporaryBoardsRoot);

            CreatePrimitive(
                $"FrontBoard_{i}",
                PrimitiveType.Cube,
                new Vector3(x, height * 0.5f, -halfDepth + 0.045f),
                new Vector3(0.32f, height, 0.05f),
                Quaternion.identity,
                temporaryMaterial,
                temporaryBoardsRoot);
        }

        for (int i = 0; i < 12; i++)
        {
            float z = (-m_RoomDepth * 0.5f) + 0.42f + (i * ((m_RoomDepth - 0.84f) / 11f));
            float height = m_RoomHeight - 0.22f + ((i % 2 == 0) ? 0.03f : -0.03f);
            Material temporaryMaterial = (i % 3 == 0) ? _lightMaterial : (i % 2 == 0 ? _midMaterial : _darkMaterial);

            CreatePrimitive(
                $"LeftBoard_{i}",
                PrimitiveType.Cube,
                new Vector3(-halfWidth + 0.045f, height * 0.5f, z),
                new Vector3(0.05f, height, 0.34f),
                Quaternion.identity,
                temporaryMaterial,
                temporaryBoardsRoot);

            CreatePrimitive(
                $"RightBoard_{i}",
                PrimitiveType.Cube,
                new Vector3(halfWidth - 0.045f, height * 0.5f, z),
                new Vector3(0.05f, height, 0.34f),
                Quaternion.identity,
                temporaryMaterial,
                temporaryBoardsRoot);
        }

        CreatePrimitive("CornerPostBackLeft", PrimitiveType.Cube, new Vector3(-halfWidth + 0.12f, m_RoomHeight * 0.5f, halfDepth - 0.12f), new Vector3(0.18f, m_RoomHeight, 0.18f), Quaternion.identity, _darkMaterial, temporaryBoardsRoot);
        CreatePrimitive("CornerPostBackRight", PrimitiveType.Cube, new Vector3(halfWidth - 0.12f, m_RoomHeight * 0.5f, halfDepth - 0.12f), new Vector3(0.18f, m_RoomHeight, 0.18f), Quaternion.identity, _darkMaterial, temporaryBoardsRoot);
        CreatePrimitive("CornerPostFrontLeft", PrimitiveType.Cube, new Vector3(-halfWidth + 0.12f, m_RoomHeight * 0.5f, -halfDepth + 0.12f), new Vector3(0.18f, m_RoomHeight, 0.18f), Quaternion.identity, _darkMaterial, temporaryBoardsRoot);
        CreatePrimitive("CornerPostFrontRight", PrimitiveType.Cube, new Vector3(halfWidth - 0.12f, m_RoomHeight * 0.5f, -halfDepth + 0.12f), new Vector3(0.18f, m_RoomHeight, 0.5f * 0.36f), Quaternion.identity, _darkMaterial, temporaryBoardsRoot);
    }

    private void BuildCeilingBeams(Transform _parent, Material _material)
    {
        Transform temporaryBeamRoot = CreateEmpty("CeilingBeams", Vector3.zero, _parent);
        for (int i = 0; i < 4; i++)
        {
            float z = (-m_RoomDepth * 0.5f) + 1.15f + (i * 2f);
            CreatePrimitive(
                $"CeilingBeam_{i}",
                PrimitiveType.Cube,
                new Vector3(0f, m_RoomHeight - 0.18f, z),
                new Vector3(m_RoomWidth - 0.3f, 0.12f, 0.18f),
                Quaternion.identity,
                _material,
                temporaryBeamRoot);
        }
    }

    private void BuildTable()
    {
        Transform temporaryTableRoot = CreateEmpty("TableRoot", Vector3.zero, m_GeneratedRoot);
        Material temporaryWoodMaterial = CreateLitMaterial("TableWoodMat", m_WoodMidColor, 0.42f, 0f);
        Material temporaryWoodDarkMaterial = CreateLitMaterial("TableWoodDarkMat", m_WoodDarkColor, 0.58f, 0f);
        Material temporaryLeatherMaterial = CreateLitMaterial("TableLeatherMat", m_RecessColor, 0.18f, 0f);

        float halfLength = m_TableLength * 0.5f;
        float halfWidth = m_TableWidth * 0.5f;
        float recessHalfLength = m_RecessLength * 0.5f;
        float recessHalfWidth = m_RecessWidth * 0.5f;
        float endSectionLength = (m_TableLength - m_RecessLength) * 0.5f;
        float sideSectionWidth = (m_TableWidth - m_RecessWidth) * 0.5f;
        float topY = m_TableHeight - (m_TableTopThickness * 0.5f);
        float recessBaseY = m_TableHeight - m_RecessDepth;
        float innerWallHeight = m_RecessDepth - 0.025f;
        float apronCenterY = m_TableHeight - (m_TableApronHeight * 0.5f);
        float legHeight = m_TableHeight - 0.04f;
        float legOffsetX = halfWidth - 0.14f;
        float legOffsetZ = halfLength - 0.18f;

        CreatePrimitive("NorthDeck", PrimitiveType.Cube, new Vector3(0f, topY, recessHalfLength + (endSectionLength * 0.5f)), new Vector3(m_TableWidth, m_TableTopThickness, endSectionLength), Quaternion.identity, temporaryWoodMaterial, temporaryTableRoot);
        CreatePrimitive("SouthDeck", PrimitiveType.Cube, new Vector3(0f, topY, -recessHalfLength - (endSectionLength * 0.5f)), new Vector3(m_TableWidth, m_TableTopThickness, endSectionLength), Quaternion.identity, temporaryWoodMaterial, temporaryTableRoot);
        CreatePrimitive("WestDeck", PrimitiveType.Cube, new Vector3(-recessHalfWidth - (sideSectionWidth * 0.5f), topY, 0f), new Vector3(sideSectionWidth, m_TableTopThickness, m_RecessLength), Quaternion.identity, temporaryWoodMaterial, temporaryTableRoot);
        CreatePrimitive("EastDeck", PrimitiveType.Cube, new Vector3(recessHalfWidth + (sideSectionWidth * 0.5f), topY, 0f), new Vector3(sideSectionWidth, m_TableTopThickness, m_RecessLength), Quaternion.identity, temporaryWoodMaterial, temporaryTableRoot);

        CreatePrimitive("RecessBase", PrimitiveType.Cube, new Vector3(0f, recessBaseY, 0f), new Vector3(m_RecessWidth, 0.03f, m_RecessLength), Quaternion.identity, temporaryLeatherMaterial, temporaryTableRoot);
        CreatePrimitive("RecessWallNorth", PrimitiveType.Cube, new Vector3(0f, recessBaseY + (innerWallHeight * 0.5f), recessHalfLength + 0.03f), new Vector3(m_RecessWidth, innerWallHeight, 0.06f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("RecessWallSouth", PrimitiveType.Cube, new Vector3(0f, recessBaseY + (innerWallHeight * 0.5f), -recessHalfLength - 0.03f), new Vector3(m_RecessWidth, innerWallHeight, 0.06f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("RecessWallWest", PrimitiveType.Cube, new Vector3(-recessHalfWidth - 0.03f, recessBaseY + (innerWallHeight * 0.5f), 0f), new Vector3(0.06f, innerWallHeight, m_RecessLength + 0.12f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("RecessWallEast", PrimitiveType.Cube, new Vector3(recessHalfWidth + 0.03f, recessBaseY + (innerWallHeight * 0.5f), 0f), new Vector3(0.06f, innerWallHeight, m_RecessLength + 0.12f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        BuildTabletopMap(temporaryTableRoot, recessBaseY);

        CreatePrimitive("OuterApronNorth", PrimitiveType.Cube, new Vector3(0f, apronCenterY, halfLength - 0.04f), new Vector3(m_TableWidth - 0.08f, m_TableApronHeight, 0.08f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("OuterApronSouth", PrimitiveType.Cube, new Vector3(0f, apronCenterY, -halfLength + 0.04f), new Vector3(m_TableWidth - 0.08f, m_TableApronHeight, 0.08f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("OuterApronWest", PrimitiveType.Cube, new Vector3(-halfWidth + 0.04f, apronCenterY, 0f), new Vector3(0.08f, m_TableApronHeight, m_TableLength - 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("OuterApronEast", PrimitiveType.Cube, new Vector3(halfWidth - 0.04f, apronCenterY, 0f), new Vector3(0.08f, m_TableApronHeight, m_TableLength - 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);

        CreatePrimitive("LegNorthWest", PrimitiveType.Cube, new Vector3(-legOffsetX, legHeight * 0.5f, legOffsetZ), new Vector3(0.16f, legHeight, 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("LegNorthEast", PrimitiveType.Cube, new Vector3(legOffsetX, legHeight * 0.5f, legOffsetZ), new Vector3(0.16f, legHeight, 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("LegSouthWest", PrimitiveType.Cube, new Vector3(-legOffsetX, legHeight * 0.5f, -legOffsetZ), new Vector3(0.16f, legHeight, 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("LegSouthEast", PrimitiveType.Cube, new Vector3(legOffsetX, legHeight * 0.5f, -legOffsetZ), new Vector3(0.16f, legHeight, 0.16f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);

        CreatePrimitive("LongStretcherWest", PrimitiveType.Cube, new Vector3(-legOffsetX, 0.22f, 0f), new Vector3(0.06f, 0.08f, m_TableLength - 0.55f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("LongStretcherEast", PrimitiveType.Cube, new Vector3(legOffsetX, 0.22f, 0f), new Vector3(0.06f, 0.08f, m_TableLength - 0.55f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("CrossStretcherNorth", PrimitiveType.Cube, new Vector3(0f, 0.23f, legOffsetZ), new Vector3(m_TableWidth - 0.44f, 0.08f, 0.06f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
        CreatePrimitive("CrossStretcherSouth", PrimitiveType.Cube, new Vector3(0f, 0.23f, -legOffsetZ), new Vector3(m_TableWidth - 0.44f, 0.08f, 0.06f), Quaternion.identity, temporaryWoodDarkMaterial, temporaryTableRoot);
    }

    private void BuildTabletopMap(Transform _tableRoot, float _recessBaseY)
    {
        m_TabletopMapGenerator = null;

        if (!m_CreateTabletopMap)
        {
            return;
        }

        float temporaryMapWidth = Mathf.Max(0.18f, m_RecessWidth - (m_TabletopMapMargin * 2f));
        float temporaryMapLength = Mathf.Max(0.32f, m_RecessLength - (m_TabletopMapMargin * 2f));
        GameObject temporaryMapObject = new GameObject("PhysicalRoguelikeMap");
        temporaryMapObject.transform.SetParent(_tableRoot, false);

        RoguelikeMapGenerator temporaryGenerator = temporaryMapObject.AddComponent<RoguelikeMapGenerator>();
        temporaryGenerator.SetupAsLocalTabletopParchment(
            new Vector3(0f, _recessBaseY + m_TabletopMapSurfaceLift, 0f),
            Quaternion.Euler(-90f, 180f, 0f),
            new Vector2(temporaryMapWidth, temporaryMapLength),
            true);
        temporaryGenerator.SetNodeIconAtlas(ResolveTabletopMapIconAtlas());
        temporaryGenerator.SetNodeScenePools(
            m_QuestionNodeScenePool,
            m_ShopNodeScenePool,
            m_CampfireNodeScenePool,
            m_EliteNodeScenePool,
            m_BossNodeScenePool);
        temporaryGenerator.SetStartBuildEnabled(false);

        RoguelikeMapLaunchRequest.ConsumeInkPrintOnNextGameplayScene();
        temporaryGenerator.GenerateMap();
        m_TabletopMapGenerator = temporaryGenerator;
    }

    private void BuildDealerSeat()
    {
        Transform temporaryChairRoot = CreateEmpty("DealerChair", new Vector3(0f, 0f, (m_TableLength * 0.5f) + 1.08f), m_GeneratedRoot);
        Material temporaryWoodMaterial = CreateLitMaterial("ChairWoodMat", m_WoodDarkColor, 0.48f, 0f);
        Material temporaryLeatherMaterial = CreateLitMaterial("ChairLeatherMat", m_LeatherColor, 0.28f, 0f);

        CreatePrimitive("ChairSeat", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 0.08f, 0.68f), Quaternion.identity, temporaryLeatherMaterial, temporaryChairRoot);
        CreatePrimitive("ChairBack", PrimitiveType.Cube, new Vector3(0f, 1.02f, 0.28f), new Vector3(0.78f, 1.08f, 0.08f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
        CreatePrimitive("ChairBackTop", PrimitiveType.Cube, new Vector3(0f, 1.56f, 0.28f), new Vector3(0.92f, 0.08f, 0.1f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
        CreatePrimitive("ChairLegFrontLeft", PrimitiveType.Cube, new Vector3(-0.28f, 0.25f, -0.24f), new Vector3(0.09f, 0.5f, 0.09f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
        CreatePrimitive("ChairLegFrontRight", PrimitiveType.Cube, new Vector3(0.28f, 0.25f, -0.24f), new Vector3(0.09f, 0.5f, 0.09f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
        CreatePrimitive("ChairLegBackLeft", PrimitiveType.Cube, new Vector3(-0.28f, 0.7f, 0.26f), new Vector3(0.09f, 1.4f, 0.09f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
        CreatePrimitive("ChairLegBackRight", PrimitiveType.Cube, new Vector3(0.28f, 0.7f, 0.26f), new Vector3(0.09f, 1.4f, 0.09f), Quaternion.identity, temporaryWoodMaterial, temporaryChairRoot);
    }

    private void BuildDungeonMasterScreen()
    {
        Transform temporaryScreenRoot = CreateEmpty("DungeonMasterScreen", new Vector3(0f, m_TableHeight + 0.025f, (m_TableLength * 0.5f) - 0.42f), m_GeneratedRoot);
        Material temporaryPanelMaterial = CreateLitMaterial("ScreenPanelMat", new Color(0.18f, 0.11f, 0.07f, 1f), 0.28f, 0f);
        Material temporaryTrimMaterial = CreateLitMaterial("ScreenTrimMat", m_BoneColor * 0.65f, 0.26f, 0f);
        Material temporaryInsetMaterial = CreateLitMaterial("ScreenInsetMat", new Color(0.12f, 0.05f, 0.04f, 1f), 0.18f, 0f);
        Material temporaryWaxMaterial = CreateLitMaterial("WaxDripMat", new Color(0.62f, 0.51f, 0.4f, 1f), 0.5f, 0f);

        CreatePrimitive("CenterPanel", PrimitiveType.Cube, new Vector3(0f, 0.19f, 0f), new Vector3(0.9f, 0.38f, 0.03f), Quaternion.identity, temporaryPanelMaterial, temporaryScreenRoot);
        CreatePrimitive("LeftPanel", PrimitiveType.Cube, new Vector3(-0.54f, 0.18f, -0.08f), new Vector3(0.42f, 0.34f, 0.03f), Quaternion.Euler(0f, 24f, 0f), temporaryPanelMaterial, temporaryScreenRoot);
        CreatePrimitive("RightPanel", PrimitiveType.Cube, new Vector3(0.54f, 0.18f, -0.08f), new Vector3(0.42f, 0.34f, 0.03f), Quaternion.Euler(0f, -24f, 0f), temporaryPanelMaterial, temporaryScreenRoot);

        CreatePrimitive("CenterInset", PrimitiveType.Cube, new Vector3(0f, 0.19f, -0.01f), new Vector3(0.72f, 0.26f, 0.012f), Quaternion.identity, temporaryInsetMaterial, temporaryScreenRoot);
        CreatePrimitive("LeftInset", PrimitiveType.Cube, new Vector3(-0.54f, 0.18f, -0.092f), new Vector3(0.28f, 0.22f, 0.012f), Quaternion.Euler(0f, 24f, 0f), temporaryInsetMaterial, temporaryScreenRoot);
        CreatePrimitive("RightInset", PrimitiveType.Cube, new Vector3(0.54f, 0.18f, -0.092f), new Vector3(0.28f, 0.22f, 0.012f), Quaternion.Euler(0f, -24f, 0f), temporaryInsetMaterial, temporaryScreenRoot);

        CreatePrimitive("TopTrim", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(0.98f, 0.04f, 0.05f), Quaternion.identity, temporaryTrimMaterial, temporaryScreenRoot);
        CreatePrimitive("BottomTrim", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.98f, 0.04f, 0.05f), Quaternion.identity, temporaryTrimMaterial, temporaryScreenRoot);
        CreatePrimitive("OccultSigilCore", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.022f), new Vector3(0.11f, 0.11f, 0.015f), Quaternion.Euler(0f, 0f, 45f), temporaryTrimMaterial, temporaryScreenRoot);
        CreatePrimitive("OccultSigilBar", PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.024f), new Vector3(0.02f, 0.16f, 0.014f), Quaternion.identity, temporaryTrimMaterial, temporaryScreenRoot);

        CreatePrimitive("WaxDripLeft", PrimitiveType.Cylinder, new Vector3(-0.16f, 0.37f, -0.015f), new Vector3(0.012f, 0.06f, 0.012f), Quaternion.identity, temporaryWaxMaterial, temporaryScreenRoot);
        CreatePrimitive("WaxDripCenter", PrimitiveType.Cylinder, new Vector3(0.03f, 0.35f, -0.015f), new Vector3(0.01f, 0.04f, 0.01f), Quaternion.identity, temporaryWaxMaterial, temporaryScreenRoot);
        CreatePrimitive("WaxDripRight", PrimitiveType.Cylinder, new Vector3(0.18f, 0.365f, -0.015f), new Vector3(0.011f, 0.05f, 0.011f), Quaternion.identity, temporaryWaxMaterial, temporaryScreenRoot);
    }

    private void BuildAtmosphericProps()
    {
        Transform temporaryPropsRoot = CreateEmpty("AtmosphericProps", Vector3.zero, m_GeneratedRoot);
        Material temporaryBookMaterial = CreateLitMaterial("BookMat", m_LeatherColor, 0.25f, 0f);
        Material temporaryPaperMaterial = CreateLitMaterial("PaperMat", m_ParchmentColor, 0.14f, 0f);
        Material temporaryMetalMaterial = CreateLitMaterial("MetalMat", m_MetalColor, 0.22f, 0f);
        Material temporaryWaxMaterial = CreateLitMaterial("CandleWaxMat", new Color(0.76f, 0.71f, 0.63f, 1f), 0.5f, 0f);
        Material temporaryGlassMaterial = CreateLitMaterial("GlassLikeMat", new Color(0.1f, 0.09f, 0.08f, 1f), 0.72f, 0f);
        float halfLength = m_TableLength * 0.5f;
        float halfWidth = m_TableWidth * 0.5f;

        CreatePrimitive("PlayerBook", PrimitiveType.Cube, new Vector3(0.48f, m_TableHeight + 0.03f, -halfLength + 0.32f), new Vector3(0.24f, 0.05f, 0.33f), Quaternion.Euler(0f, -8f, 0f), temporaryBookMaterial, temporaryPropsRoot);
        CreatePrimitive("PlayerBookStrap", PrimitiveType.Cube, new Vector3(0.48f, m_TableHeight + 0.058f, -halfLength + 0.32f), new Vector3(0.04f, 0.012f, 0.34f), Quaternion.Euler(0f, -8f, 0f), temporaryMetalMaterial, temporaryPropsRoot);

        CreatePrimitive("InkBottle", PrimitiveType.Cylinder, new Vector3(0.74f, m_TableHeight + 0.055f, -halfLength + 0.26f), new Vector3(0.045f, 0.055f, 0.045f), Quaternion.identity, temporaryGlassMaterial, temporaryPropsRoot);
        CreatePrimitive("InkBottleStopper", PrimitiveType.Sphere, new Vector3(0.74f, m_TableHeight + 0.12f, -halfLength + 0.26f), new Vector3(0.03f, 0.028f, 0.03f), Quaternion.identity, temporaryBookMaterial, temporaryPropsRoot);

        CreatePrimitive("DieA", PrimitiveType.Cube, new Vector3(-0.5f, m_TableHeight + 0.02f, -halfLength + 0.28f), Vector3.one * 0.04f, Quaternion.Euler(12f, 26f, 18f), temporaryPaperMaterial, temporaryPropsRoot);
        CreatePrimitive("DieB", PrimitiveType.Cube, new Vector3(-0.43f, m_TableHeight + 0.019f, -halfLength + 0.34f), Vector3.one * 0.035f, Quaternion.Euler(22f, -14f, 9f), temporaryPaperMaterial, temporaryPropsRoot);
        CreatePrimitive("DieC", PrimitiveType.Cube, new Vector3(-0.57f, m_TableHeight + 0.018f, -halfLength + 0.37f), Vector3.one * 0.03f, Quaternion.Euler(10f, -36f, -14f), temporaryPaperMaterial, temporaryPropsRoot);

        CreatePrimitive("Scroll", PrimitiveType.Cylinder, new Vector3(-halfWidth + 0.14f, m_TableHeight + 0.03f, halfLength - 0.64f), new Vector3(0.05f, 0.16f, 0.05f), Quaternion.Euler(0f, 0f, 90f), temporaryPaperMaterial, temporaryPropsRoot);
        CreatePrimitive("ScrollTie", PrimitiveType.Cube, new Vector3(-halfWidth + 0.14f, m_TableHeight + 0.045f, halfLength - 0.64f), new Vector3(0.03f, 0.01f, 0.18f), Quaternion.Euler(18f, 0f, 90f), temporaryBookMaterial, temporaryPropsRoot);

        CreatePrimitive("HourglassTop", PrimitiveType.Cylinder, new Vector3(halfWidth - 0.16f, m_TableHeight + 0.17f, halfLength - 0.7f), new Vector3(0.06f, 0.02f, 0.06f), Quaternion.identity, temporaryMetalMaterial, temporaryPropsRoot);
        CreatePrimitive("HourglassBottom", PrimitiveType.Cylinder, new Vector3(halfWidth - 0.16f, m_TableHeight + 0.05f, halfLength - 0.7f), new Vector3(0.06f, 0.02f, 0.06f), Quaternion.identity, temporaryMetalMaterial, temporaryPropsRoot);
        CreatePrimitive("HourglassLeft", PrimitiveType.Cylinder, new Vector3((halfWidth - 0.16f) - 0.04f, m_TableHeight + 0.11f, halfLength - 0.7f), new Vector3(0.01f, 0.06f, 0.01f), Quaternion.identity, temporaryMetalMaterial, temporaryPropsRoot);
        CreatePrimitive("HourglassRight", PrimitiveType.Cylinder, new Vector3((halfWidth - 0.16f) + 0.04f, m_TableHeight + 0.11f, halfLength - 0.7f), new Vector3(0.01f, 0.06f, 0.01f), Quaternion.identity, temporaryMetalMaterial, temporaryPropsRoot);
        CreatePrimitive("HourglassGlassUpper", PrimitiveType.Sphere, new Vector3(halfWidth - 0.16f, m_TableHeight + 0.135f, halfLength - 0.7f), new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity, temporaryGlassMaterial, temporaryPropsRoot);
        CreatePrimitive("HourglassGlassLower", PrimitiveType.Sphere, new Vector3(halfWidth - 0.16f, m_TableHeight + 0.085f, halfLength - 0.7f), new Vector3(0.05f, 0.05f, 0.05f), Quaternion.identity, temporaryGlassMaterial, temporaryPropsRoot);

        BuildTableCandle("ScreenCandleLeft", new Vector3(-halfWidth + 0.16f, m_TableHeight + 0.01f, halfLength - 0.42f), temporaryWaxMaterial, temporaryMetalMaterial, temporaryPropsRoot);
        BuildTableCandle("ScreenCandleRight", new Vector3(halfWidth - 0.16f, m_TableHeight + 0.01f, halfLength - 0.42f), temporaryWaxMaterial, temporaryMetalMaterial, temporaryPropsRoot);
        BuildWallSconce("LeftWallSconce", new Vector3(-(m_RoomWidth * 0.5f) + 0.2f, 1.52f, (m_TableLength * 0.5f) + 0.58f), 90f, temporaryWaxMaterial, temporaryMetalMaterial, temporaryPropsRoot);
        BuildWallSconce("RightWallSconce", new Vector3((m_RoomWidth * 0.5f) - 0.2f, 1.52f, (m_TableLength * 0.5f) + 0.58f), -90f, temporaryWaxMaterial, temporaryMetalMaterial, temporaryPropsRoot);
    }

    private void BuildLighting()
    {
        Transform temporaryLightingRoot = CreateEmpty("Lighting", Vector3.zero, m_GeneratedRoot);

        CreateDirectionalLight(
            "DungeonDirectionalLight",
            new Vector3(0f, 2.6f, -1.2f),
            new Vector3(40f, 0f, 0f),
            new Color(0.14f, 0.1f, 0.08f, 1f),
            m_DirectionalIntensity,
            temporaryLightingRoot);

        float halfLength = m_TableLength * 0.5f;
        float halfWidth = m_TableWidth * 0.5f;

        CreatePointLight("ScreenCandleLeftLight", new Vector3(-halfWidth + 0.16f, m_TableHeight + 0.36f, halfLength - 0.42f), m_CandleColor, m_CandleLightIntensity, 2.5f, false, temporaryLightingRoot);
        CreatePointLight("ScreenCandleRightLight", new Vector3(halfWidth - 0.16f, m_TableHeight + 0.36f, halfLength - 0.42f), m_CandleColor, m_CandleLightIntensity, 2.5f, false, temporaryLightingRoot);
        CreatePointLight("LeftWallSconceLight", new Vector3(-(m_RoomWidth * 0.5f) + 0.36f, 1.56f, halfLength + 0.55f), m_CandleColor * 0.92f, m_CandleLightIntensity * 0.88f, 3f, false, temporaryLightingRoot);
        CreatePointLight("RightWallSconceLight", new Vector3((m_RoomWidth * 0.5f) - 0.36f, 1.56f, halfLength + 0.55f), m_CandleColor * 0.92f, m_CandleLightIntensity * 0.88f, 3f, false, temporaryLightingRoot);
        CreateSpotLight("TableFillLight", new Vector3(0f, 1.5f, -0.98f), new Vector3(18f, 0f, 0f), m_CandleColor * 0.62f, m_TableFillIntensity, 6.8f, 54f, temporaryLightingRoot);
        CreateSpotLight("DealerKeyLight", new Vector3(0f, 1.78f, 0.9f), new Vector3(22f, 0f, 0f), m_CandleColor * 0.45f, m_DealerKeyIntensity, 4.4f, 30f, temporaryLightingRoot);
        CreatePointLight("DemonRimLight", new Vector3(0f, m_TableHeight + 0.92f, halfLength + 1.25f), m_EmberColor, m_DemonRimIntensity, 1.8f, false, temporaryLightingRoot);
    }

    private void PositionImportedDemon()
    {
        HideNamedSceneObjects(m_ExtraModelNamesToHide);

        Transform temporaryDealer = FindSceneTransformByName(m_DemonObjectName);
        if (temporaryDealer == null)
        {
            BuildFallbackDealer();
            return;
        }

        temporaryDealer.gameObject.SetActive(true);
        temporaryDealer.SetParent(null, true);
        temporaryDealer.position = new Vector3(0f, m_TableHeight + m_DemonSeatHeight, (m_TableLength * 0.5f) + m_DemonBackOffset);
        if (!m_PreserveImportedDemonRotation)
        {
            temporaryDealer.rotation = Quaternion.Euler(0f, m_DemonYawOffset, 0f);
        }
        temporaryDealer.localScale = Vector3.one * m_DemonScale;

        Renderer[] temporaryRenderers = temporaryDealer.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < temporaryRenderers.Length; i++)
        {
            TintImportedRenderer(temporaryRenderers[i], new Color(0.7f, 0.6f, 0.54f, 1f));
        }
    }

    private void PositionWizardMiniature()
    {
        Transform temporaryWizard = FindSceneTransformByName(m_WizardObjectName);
        if (temporaryWizard == null || m_TabletopMapGenerator == null)
        {
            return;
        }

        Vector3 temporaryStartNodePosition;
        if (!m_TabletopMapGenerator.TryGetStartNodeWorldPosition(out temporaryStartNodePosition))
        {
            return;
        }

        temporaryWizard.gameObject.SetActive(true);
        temporaryWizard.SetParent(null, true);
        temporaryWizard.localScale = Vector3.one * m_WizardMiniatureScale;

        if (m_FaceWizardTowardDemon)
        {
            FaceWizardTowardDemon(temporaryWizard, temporaryStartNodePosition);
        }
        else if (!m_PreserveImportedWizardRotation)
        {
            temporaryWizard.rotation = Quaternion.Euler(m_WizardRotation);
        }

        Bounds temporaryBounds;
        if (TryGetRendererBounds(temporaryWizard, out temporaryBounds))
        {
            Vector3 temporaryTargetBottomCenter = new Vector3(
                temporaryStartNodePosition.x,
                temporaryStartNodePosition.y + m_WizardMapSurfaceOffset,
                temporaryStartNodePosition.z);
            Vector3 temporaryCurrentBottomCenter = new Vector3(
                temporaryBounds.center.x,
                temporaryBounds.min.y,
                temporaryBounds.center.z);

            temporaryWizard.position += temporaryTargetBottomCenter - temporaryCurrentBottomCenter;
        }
        else
        {
            temporaryWizard.position = temporaryStartNodePosition + (Vector3.up * m_WizardMapSurfaceOffset);
        }

        Renderer[] temporaryRenderers = temporaryWizard.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < temporaryRenderers.Length; i++)
        {
            if (temporaryRenderers[i] == null)
            {
                continue;
            }

            temporaryRenderers[i].shadowCastingMode = m_UseStableWizardLighting ? ShadowCastingMode.Off : ShadowCastingMode.On;
            temporaryRenderers[i].receiveShadows = !m_UseStableWizardLighting;
            ConfigureWizardRendererMaterial(temporaryRenderers[i]);
        }

        m_TabletopMapGenerator.SetPlayerPiece(temporaryWizard);
    }

    private void FaceWizardTowardDemon(Transform _wizard, Vector3 _wizardPosition)
    {
        Transform temporaryDemon = FindSceneTransformByName(m_DemonObjectName);
        if (_wizard == null || temporaryDemon == null)
        {
            return;
        }

        Vector3 temporaryDirection = temporaryDemon.position - _wizardPosition;
        temporaryDirection.y = 0f;
        if (temporaryDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 adjustedDirection = Quaternion.AngleAxis(m_WizardFacingYawOffset, Vector3.up) * temporaryDirection.normalized;
        _wizard.rotation = Quaternion.LookRotation(Vector3.up, adjustedDirection.normalized);
    }

    private void ConfigureWizardRendererMaterial(Renderer _renderer)
    {
        if (_renderer == null)
        {
            return;
        }

        Material temporarySourceMaterial = ResolveWizardSourceMaterial(_renderer.sharedMaterial);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Material temporaryPersistentMaterial = CreateOrUpdateEditorWizardMaterial("WizardMiniatureMat", temporarySourceMaterial);
            if (temporaryPersistentMaterial != null)
            {
                _renderer.sharedMaterial = temporaryPersistentMaterial;
            }

            return;
        }
#endif

        Material temporaryRuntimeMaterial = CreateRuntimeWizardMaterial(temporarySourceMaterial);
        if (temporaryRuntimeMaterial != null)
        {
            m_RuntimeMaterials.Add(temporaryRuntimeMaterial);
            _renderer.sharedMaterial = temporaryRuntimeMaterial;
        }
    }

    private Material CreateRuntimeWizardMaterial(Material _sourceMaterial)
    {
        Shader temporaryShader = ResolveWizardShader();
        if (temporaryShader == null)
        {
            return _sourceMaterial;
        }

        Material temporaryMaterial = new Material(temporaryShader)
        {
            name = "WizardMiniatureRuntimeMat",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };
        ApplyWizardMaterialProperties(temporaryMaterial, _sourceMaterial);
        return temporaryMaterial;
    }

    private Material ResolveWizardSourceMaterial(Material _fallbackMaterial)
    {
        if (_fallbackMaterial != null)
        {
            return _fallbackMaterial;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(m_WizardSourceMaterialPath))
        {
            Material temporarySourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(m_WizardSourceMaterialPath);
            if (temporarySourceMaterial != null)
            {
                return temporarySourceMaterial;
            }
        }
#endif

        return null;
    }

    private void ApplyWizardMaterialProperties(Material _targetMaterial, Material _sourceMaterial)
    {
        if (_targetMaterial == null)
        {
            return;
        }

        ApplyMaterialProperties(_targetMaterial, Color.white, 0.38f, 0f);

        Texture temporaryBaseTexture = GetFirstMaterialTexture(_sourceMaterial, "_BaseMap", "_MainTex");
        if (temporaryBaseTexture != null)
        {
            _targetMaterial.mainTexture = temporaryBaseTexture;
            SetMaterialTextureIfPresent(_targetMaterial, "_BaseMap", temporaryBaseTexture);
            SetMaterialTextureIfPresent(_targetMaterial, "_MainTex", temporaryBaseTexture);
        }

        Texture temporaryNormalTexture = GetFirstMaterialTexture(_sourceMaterial, "_BumpMap");
        if (temporaryNormalTexture != null)
        {
            SetMaterialTextureIfPresent(_targetMaterial, "_BumpMap", temporaryNormalTexture);
            _targetMaterial.EnableKeyword("_NORMALMAP");
        }

        Texture temporaryMetallicTexture = GetFirstMaterialTexture(_sourceMaterial, "_MetallicGlossMap");
        if (temporaryMetallicTexture != null)
        {
            SetMaterialTextureIfPresent(_targetMaterial, "_MetallicGlossMap", temporaryMetallicTexture);
            _targetMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        SetMaterialFloatIfPresent(_targetMaterial, "_Metallic", 0.18f);
        SetMaterialFloatIfPresent(_targetMaterial, "_Smoothness", 0.42f);
        SetMaterialFloatIfPresent(_targetMaterial, "_Glossiness", 0.42f);
    }

    private Shader ResolveWizardShader()
    {
        if (m_UseStableWizardLighting)
        {
            Shader temporaryUnlitShader = Shader.Find(c_DefaultUrpUnlitShader);
            if (temporaryUnlitShader != null)
            {
                return temporaryUnlitShader;
            }
        }

        return ResolveLitShader();
    }

    private static Texture GetFirstMaterialTexture(Material _material, params string[] _propertyNames)
    {
        if (_material == null || _propertyNames == null)
        {
            return null;
        }

        for (int i = 0; i < _propertyNames.Length; i++)
        {
            string temporaryPropertyName = _propertyNames[i];
            if (!string.IsNullOrWhiteSpace(temporaryPropertyName) && _material.HasProperty(temporaryPropertyName))
            {
                Texture temporaryTexture = _material.GetTexture(temporaryPropertyName);
                if (temporaryTexture != null)
                {
                    return temporaryTexture;
                }
            }
        }

        return null;
    }

    private static void SetMaterialTextureIfPresent(Material _material, string _propertyName, Texture _texture)
    {
        if (_material != null && _texture != null && _material.HasProperty(_propertyName))
        {
            _material.SetTexture(_propertyName, _texture);
        }
    }

    private static void SetMaterialFloatIfPresent(Material _material, string _propertyName, float _value)
    {
        if (_material != null && _material.HasProperty(_propertyName))
        {
            _material.SetFloat(_propertyName, _value);
        }
    }

    private static bool TryGetRendererBounds(Transform _root, out Bounds _bounds)
    {
        _bounds = default;
        if (_root == null)
        {
            return false;
        }

        Renderer[] temporaryRenderers = _root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < temporaryRenderers.Length; i++)
        {
            if (temporaryRenderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                _bounds = temporaryRenderers[i].bounds;
                hasBounds = true;
                continue;
            }

            _bounds.Encapsulate(temporaryRenderers[i].bounds);
        }

        return hasBounds;
    }

    private Texture2D ResolveTabletopMapIconAtlas()
    {
        if (m_TabletopMapIconAtlas != null)
        {
            return m_TabletopMapIconAtlas;
        }

        if (string.IsNullOrWhiteSpace(m_TabletopMapIconAtlasResourcePath))
        {
            return null;
        }

        return Resources.Load<Texture2D>(m_TabletopMapIconAtlasResourcePath);
    }

    private void BuildFallbackDealer()
    {
        Transform temporaryFallbackRoot = CreateEmpty("DealerFallback", new Vector3(0f, m_TableHeight + m_DemonSeatHeight, (m_TableLength * 0.5f) + m_DemonBackOffset), m_GeneratedRoot);
        Material temporaryBoneMaterial = CreateLitMaterial("FallbackBoneMat", m_BoneColor, 0.24f, 0f);
        Material temporaryShadowMaterial = CreateLitMaterial("FallbackVoidMat", new Color(0.03f, 0.02f, 0.018f, 1f), 0.9f, 0f);

        CreatePrimitive("FallbackSkull", PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0f), new Vector3(0.42f, 0.54f, 0.28f), Quaternion.identity, temporaryBoneMaterial, temporaryFallbackRoot);
        CreatePrimitive("FallbackHornLeft", PrimitiveType.Cylinder, new Vector3(-0.23f, 0.58f, -0.04f), new Vector3(0.04f, 0.22f, 0.04f), Quaternion.Euler(34f, 0f, 42f), temporaryBoneMaterial, temporaryFallbackRoot);
        CreatePrimitive("FallbackHornRight", PrimitiveType.Cylinder, new Vector3(0.23f, 0.58f, -0.04f), new Vector3(0.04f, 0.22f, 0.04f), Quaternion.Euler(34f, 0f, -42f), temporaryBoneMaterial, temporaryFallbackRoot);
        CreatePrimitive("FallbackEyes", PrimitiveType.Cube, new Vector3(0f, 0.36f, -0.13f), new Vector3(0.2f, 0.08f, 0.03f), Quaternion.identity, temporaryShadowMaterial, temporaryFallbackRoot);
        CreatePrimitive("FallbackRobe", PrimitiveType.Cube, new Vector3(0f, 0f, 0.14f), new Vector3(1.1f, 1.1f, 0.24f), Quaternion.identity, temporaryShadowMaterial, temporaryFallbackRoot);
    }

    private void ConfigurePlayerView()
    {
        if (m_CachedMainCamera == null)
        {
            GameObject temporaryCameraGameObject = new GameObject("Main Camera");
            temporaryCameraGameObject.tag = "MainCamera";
            m_CachedMainCamera = temporaryCameraGameObject.AddComponent<Camera>();
            temporaryCameraGameObject.AddComponent<AudioListener>();
        }

        float playerSeatZ = -(m_TableLength * 0.5f) - m_PlayerSeatDistance;
        Vector3 temporaryEyePosition = new Vector3(0f, m_PlayerEyeHeight, playerSeatZ);
        Vector3 temporaryLookTarget = new Vector3(0f, m_TableHeight + 0.12f, (m_TableLength * 0.5f) - 0.17f);
        Quaternion temporaryLookRotation = Quaternion.LookRotation((temporaryLookTarget - temporaryEyePosition).normalized, Vector3.up);
        Quaternion temporaryPitchOffset = Quaternion.Euler(m_CameraPitchOffset, 0f, 0f);

        m_CachedMainCamera.transform.SetParent(m_GeneratedRoot, false);
        m_CachedMainCamera.transform.position = temporaryEyePosition;
        m_CachedMainCamera.transform.rotation = temporaryLookRotation * temporaryPitchOffset;
        m_CachedMainCamera.fieldOfView = m_PlayerCameraFieldOfView;
        m_CachedMainCamera.nearClipPlane = 0.03f;
        m_CachedMainCamera.farClipPlane = 30f;
        m_CachedMainCamera.allowHDR = true;
        m_CachedMainCamera.clearFlags = CameraClearFlags.SolidColor;
        m_CachedMainCamera.backgroundColor = Color.black;
    }

    private void BuildTableCandle(string _name, Vector3 _position, Material _waxMaterial, Material _holderMaterial, Transform _parent)
    {
        Transform temporaryCandleRoot = CreateEmpty(_name, _position, _parent);
        Material temporaryFlameMaterial = CreateLitMaterial($"{_name}_FlameMat", m_CandleColor, 0.08f, 1.1f);

        CreatePrimitive("Holder", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.08f, 0.025f, 0.08f), Quaternion.identity, _holderMaterial, temporaryCandleRoot);
        CreatePrimitive("Wax", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.1f, 0.04f), Quaternion.identity, _waxMaterial, temporaryCandleRoot);
        CreatePrimitive("Flame", PrimitiveType.Sphere, new Vector3(0f, 0.28f, 0f), new Vector3(0.04f, 0.07f, 0.04f), Quaternion.identity, temporaryFlameMaterial, temporaryCandleRoot);
    }

    private void BuildWallSconce(string _name, Vector3 _position, float _yaw, Material _waxMaterial, Material _metalMaterial, Transform _parent)
    {
        Transform temporarySconceRoot = CreateEmpty(_name, _position, _parent);
        temporarySconceRoot.rotation = Quaternion.Euler(0f, _yaw, 0f);
        Material temporaryFlameMaterial = CreateLitMaterial($"{_name}_FlameMat", m_CandleColor, 0.08f, 1.1f);

        CreatePrimitive("Bracket", PrimitiveType.Cube, Vector3.zero, new Vector3(0.06f, 0.06f, 0.14f), Quaternion.identity, _metalMaterial, temporarySconceRoot);
        CreatePrimitive("Holder", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0.12f), new Vector3(0.06f, 0.015f, 0.06f), Quaternion.identity, _metalMaterial, temporarySconceRoot);
        CreatePrimitive("Wax", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0.12f), new Vector3(0.036f, 0.09f, 0.036f), Quaternion.identity, _waxMaterial, temporarySconceRoot);
        CreatePrimitive("Flame", PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0.12f), new Vector3(0.035f, 0.06f, 0.035f), Quaternion.identity, temporaryFlameMaterial, temporarySconceRoot);
    }

    private void CreateDirectionalLight(string _name, Vector3 _position, Vector3 _rotation, Color _color, float _intensity, Transform _parent)
    {
        GameObject temporaryLightObject = new GameObject(_name);
        temporaryLightObject.transform.SetParent(_parent, false);
        temporaryLightObject.transform.position = _position;
        temporaryLightObject.transform.rotation = Quaternion.Euler(_rotation);

        Light temporaryLight = temporaryLightObject.AddComponent<Light>();
        temporaryLight.type = LightType.Directional;
        temporaryLight.color = _color;
        temporaryLight.intensity = _intensity;
        temporaryLight.shadows = LightShadows.Soft;
    }

    private void CreatePointLight(string _name, Vector3 _position, Color _color, float _intensity, float _range, bool _castShadows, Transform _parent)
    {
        GameObject temporaryLightObject = new GameObject(_name);
        temporaryLightObject.transform.SetParent(_parent, false);
        temporaryLightObject.transform.position = _position;

        Light temporaryLight = temporaryLightObject.AddComponent<Light>();
        temporaryLight.type = LightType.Point;
        temporaryLight.color = _color;
        temporaryLight.intensity = _intensity;
        temporaryLight.range = _range;
        temporaryLight.shadows = _castShadows ? LightShadows.Soft : LightShadows.None;
    }

    private void CreateSpotLight(string _name, Vector3 _position, Vector3 _rotation, Color _color, float _intensity, float _range, float _spotAngle, Transform _parent)
    {
        GameObject temporaryLightObject = new GameObject(_name);
        temporaryLightObject.transform.SetParent(_parent, false);
        temporaryLightObject.transform.position = _position;
        temporaryLightObject.transform.rotation = Quaternion.Euler(_rotation);

        Light temporaryLight = temporaryLightObject.AddComponent<Light>();
        temporaryLight.type = LightType.Spot;
        temporaryLight.color = _color;
        temporaryLight.intensity = _intensity;
        temporaryLight.range = _range;
        temporaryLight.spotAngle = _spotAngle;
        temporaryLight.shadows = LightShadows.None;
    }

    private void HideNamedSceneObjects(string[] _names)
    {
        if (_names == null)
        {
            return;
        }

        for (int i = 0; i < _names.Length; i++)
        {
            Transform temporaryTransform = FindSceneTransformByName(_names[i]);
            if (temporaryTransform != null)
            {
                temporaryTransform.gameObject.SetActive(false);
            }
        }
    }

    private Transform FindSceneTransformByName(string _name)
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            return null;
        }

        Transform[] temporaryTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < temporaryTransforms.Length; i++)
        {
            if (temporaryTransforms[i] == null || temporaryTransforms[i].name != _name)
            {
                continue;
            }

            if (temporaryTransforms[i].hideFlags != HideFlags.None)
            {
                continue;
            }

            return temporaryTransforms[i];
        }

        return null;
    }

    private void TintImportedRenderer(Renderer _renderer, Color _baseColor)
    {
        if (_renderer == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Material temporaryPersistentTint = CreateOrUpdateEditorTintMaterial("DealerTintMat", _renderer.sharedMaterial, _baseColor, 0.32f, 0f);
            _renderer.sharedMaterial = temporaryPersistentTint;
            _renderer.shadowCastingMode = ShadowCastingMode.On;
            _renderer.receiveShadows = true;
            return;
        }
#endif

        if (_renderer.sharedMaterial == null)
        {
            return;
        }

        Material temporaryMaterial = new Material(_renderer.sharedMaterial)
        {
            name = $"{_renderer.sharedMaterial.name}_DungeonTint",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        if (temporaryMaterial.HasProperty("_BaseColor"))
        {
            temporaryMaterial.SetColor("_BaseColor", _baseColor);
        }

        if (temporaryMaterial.HasProperty("_Color"))
        {
            temporaryMaterial.SetColor("_Color", _baseColor);
        }

        if (temporaryMaterial.HasProperty("_Smoothness"))
        {
            temporaryMaterial.SetFloat("_Smoothness", 0.32f);
        }

        m_RuntimeMaterials.Add(temporaryMaterial);
        _renderer.sharedMaterial = temporaryMaterial;
        _renderer.shadowCastingMode = ShadowCastingMode.On;
        _renderer.receiveShadows = true;
    }

    private Transform CreateEmpty(string _name, Vector3 _localPosition, Transform _parent)
    {
        GameObject temporaryGameObject = new GameObject(_name);
        temporaryGameObject.transform.SetParent(_parent, false);
        temporaryGameObject.transform.localPosition = _localPosition;
        return temporaryGameObject.transform;
    }

    private GameObject CreatePrimitive(string _name, PrimitiveType _primitiveType, Vector3 _localPosition, Vector3 _localScale, Quaternion _localRotation, Material _material, Transform _parent)
    {
        GameObject temporaryPrimitive = GameObject.CreatePrimitive(_primitiveType);
        temporaryPrimitive.name = _name;
        temporaryPrimitive.transform.SetParent(_parent, false);
        temporaryPrimitive.transform.localPosition = _localPosition;
        temporaryPrimitive.transform.localRotation = _localRotation;
        temporaryPrimitive.transform.localScale = _localScale;

        if (temporaryPrimitive.TryGetComponent(out Renderer temporaryRenderer))
        {
            temporaryRenderer.sharedMaterial = _material;
            temporaryRenderer.shadowCastingMode = ShadowCastingMode.On;
            temporaryRenderer.receiveShadows = true;
        }

        return temporaryPrimitive;
    }

    private Material CreateLitMaterial(string _name, Color _color, float _smoothness, float _emission)
    {
        Shader temporaryShader = ResolveLitShader();
        if (temporaryShader == null)
        {
            Debug.LogError("[OutOfMatchDndSceneBuilder] Could not find a compatible shader.", this);
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return CreateOrUpdateEditorMaterialAsset(_name, temporaryShader, _color, _smoothness, _emission);
        }
#endif

        Material temporaryMaterial = new Material(temporaryShader)
        {
            name = _name,
            color = _color,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
        };

        ApplyMaterialProperties(temporaryMaterial, _color, _smoothness, _emission);

        m_RuntimeMaterials.Add(temporaryMaterial);
        return temporaryMaterial;
    }

    private Shader ResolveLitShader()
    {
        Shader temporaryShader = Shader.Find(c_DefaultUrpLitShader);
        if (temporaryShader == null)
        {
            temporaryShader = Shader.Find(c_StandardShader);
        }

        return temporaryShader;
    }

    private void ApplyMaterialProperties(Material _material, Color _color, float _smoothness, float _emission)
    {
        if (_material == null)
        {
            return;
        }

        _material.color = _color;
        _material.hideFlags = HideFlags.None;

        if (_material.HasProperty("_BaseColor"))
        {
            _material.SetColor("_BaseColor", _color);
        }

        if (_material.HasProperty("_Color"))
        {
            _material.SetColor("_Color", _color);
        }

        if (_material.HasProperty("_Smoothness"))
        {
            _material.SetFloat("_Smoothness", _smoothness);
        }

        if (_emission > 0f)
        {
            _material.EnableKeyword("_EMISSION");
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.SetColor("_EmissionColor", _color * _emission);
            }
        }
        else
        {
            _material.DisableKeyword("_EMISSION");
            if (_material.HasProperty("_EmissionColor"))
            {
                _material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void SaveGeneratedAssets()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif
    }

#if UNITY_EDITOR
    private Material CreateOrUpdateEditorMaterialAsset(string _name, Shader _shader, Color _color, float _smoothness, float _emission)
    {
        EnsureEditorFolder(c_GeneratedAssetsFolder);
        EnsureEditorFolder(c_GeneratedSceneFolder);
        EnsureEditorFolder(c_GeneratedMaterialsFolder);

        string temporaryPath = $"{c_GeneratedMaterialsFolder}/{_name}.mat";
        Material temporaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(temporaryPath);

        if (temporaryMaterial == null)
        {
            temporaryMaterial = new Material(_shader)
            {
                name = _name
            };
            AssetDatabase.CreateAsset(temporaryMaterial, temporaryPath);
        }

        if (temporaryMaterial.shader != _shader)
        {
            temporaryMaterial.shader = _shader;
        }

        ApplyMaterialProperties(temporaryMaterial, _color, _smoothness, _emission);
        EditorUtility.SetDirty(temporaryMaterial);
        return temporaryMaterial;
    }

    private Material CreateOrUpdateEditorTintMaterial(string _name, Material _sourceMaterial, Color _color, float _smoothness, float _emission)
    {
        EnsureEditorFolder(c_GeneratedAssetsFolder);
        EnsureEditorFolder(c_GeneratedSceneFolder);
        EnsureEditorFolder(c_GeneratedMaterialsFolder);

        if (_sourceMaterial == null && !string.IsNullOrWhiteSpace(m_DemonSourceMaterialPath))
        {
            _sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(m_DemonSourceMaterialPath);
        }

        Shader temporaryShader = _sourceMaterial != null ? _sourceMaterial.shader : ResolveLitShader();
        if (temporaryShader == null)
        {
            Debug.LogError("[OutOfMatchDndSceneBuilder] Could not create demon tint material because no compatible shader was found.", this);
            return _sourceMaterial;
        }

        string temporaryPath = $"{c_GeneratedMaterialsFolder}/{_name}.mat";
        Material temporaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(temporaryPath);

        if (temporaryMaterial == null)
        {
            temporaryMaterial = _sourceMaterial != null ? new Material(_sourceMaterial) : new Material(temporaryShader);
            temporaryMaterial.name = _name;
            AssetDatabase.CreateAsset(temporaryMaterial, temporaryPath);
        }
        else if (_sourceMaterial != null)
        {
            temporaryMaterial.CopyPropertiesFromMaterial(_sourceMaterial);
            temporaryMaterial.shader = _sourceMaterial.shader;
        }
        else if (temporaryMaterial.shader != temporaryShader)
        {
            temporaryMaterial.shader = temporaryShader;
        }

        ApplyMaterialProperties(temporaryMaterial, _color, _smoothness, _emission);
        EditorUtility.SetDirty(temporaryMaterial);
        return temporaryMaterial;
    }

    private Material CreateOrUpdateEditorWizardMaterial(string _name, Material _sourceMaterial)
    {
        EnsureEditorFolder(c_GeneratedAssetsFolder);
        EnsureEditorFolder(c_GeneratedSceneFolder);
        EnsureEditorFolder(c_GeneratedMaterialsFolder);

        Shader temporaryShader = ResolveWizardShader();
        if (temporaryShader == null)
        {
            Debug.LogError("[OutOfMatchDndSceneBuilder] Could not create wizard miniature material because no compatible shader was found.", this);
            return _sourceMaterial;
        }

        string temporaryPath = $"{c_GeneratedMaterialsFolder}/{_name}.mat";
        Material temporaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(temporaryPath);

        if (temporaryMaterial == null)
        {
            temporaryMaterial = new Material(temporaryShader)
            {
                name = _name
            };
            AssetDatabase.CreateAsset(temporaryMaterial, temporaryPath);
        }
        else if (temporaryMaterial.shader != temporaryShader)
        {
            temporaryMaterial.shader = temporaryShader;
        }

        ApplyWizardMaterialProperties(temporaryMaterial, _sourceMaterial);
        EditorUtility.SetDirty(temporaryMaterial);
        return temporaryMaterial;
    }

    private static void EnsureEditorFolder(string _folderPath)
    {
        if (AssetDatabase.IsValidFolder(_folderPath))
        {
            return;
        }

        string[] temporarySegments = _folderPath.Split('/');
        string temporaryCurrent = temporarySegments[0];

        for (int i = 1; i < temporarySegments.Length; i++)
        {
            string temporaryNext = $"{temporaryCurrent}/{temporarySegments[i]}";
            if (!AssetDatabase.IsValidFolder(temporaryNext))
            {
                AssetDatabase.CreateFolder(temporaryCurrent, temporarySegments[i]);
            }

            temporaryCurrent = temporaryNext;
        }
    }
#endif

    private void ReleaseRuntimeMaterials()
    {
        for (int i = 0; i < m_RuntimeMaterials.Count; i++)
        {
            if (m_RuntimeMaterials[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(m_RuntimeMaterials[i]);
            }
            else
            {
                DestroyImmediate(m_RuntimeMaterials[i]);
            }
        }

        m_RuntimeMaterials.Clear();
    }

    private void DestroyTransform(Transform _target)
    {
        if (_target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_target.gameObject);
        }
        else
        {
            DestroyImmediate(_target.gameObject);
        }
    }
}
