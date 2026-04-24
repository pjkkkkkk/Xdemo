using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
using UnityEngine.PlayerLoop;

#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(UIDocument))]
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "SampleScene";
    [SerializeField] private StyleSheet menuStyleSheet;
    [SerializeField] private Texture2D backgroundImage;
    [SerializeField] private string startButtonName = "start-button";
    [SerializeField] private string continueButtonName = "continue-button";
    [SerializeField] private string quitButtonName = "quit-button";
    [SerializeField] private string backgroundLayerName = "background-layer";
    [SerializeField] private string fogWashName = "fog-wash";
    [SerializeField] private string emberDotName = "ember-dot";
    [SerializeField] private float fogPulseSpeed = 0.22f;
    [SerializeField] private float fogPulseAmplitude = 0.05f;
    [SerializeField] private float emberPulseSpeed = 0.7f;
    [SerializeField] private float emberMinOpacity = 0.45f;
    [SerializeField] private float emberMaxOpacity = 0.95f;

    private UIDocument documentRef;
    private Button startButton;
    private Button continueButton;
    private Button quitButton;
    private VisualElement backgroundLayer;
    private VisualElement fogWash;
    private VisualElement emberDot;
    private bool isLoading;

    private void Awake()
    {
        EnsureRuntimeInputSupport();
        documentRef = GetComponent<UIDocument>();
    }

    private void Start()
    {
        BindUi();
    }

    private void OnEnable()
    {
        BindUi();
    }

    private void OnDisable()
    {
        UnbindUi();
    }

    private void Update()
    {
        AnimateOverlay();
    }

    private void BindUi()
    {
        if (documentRef == null)
        {
            documentRef = GetComponent<UIDocument>();
        }

        var root = documentRef.rootVisualElement;
        if (root == null)
        {
            return;
        }

        if (menuStyleSheet != null && !root.styleSheets.Contains(menuStyleSheet))
        {
            root.styleSheets.Add(menuStyleSheet);
        }

        if (startButton == null)
        {
            startButton = root.Q<Button>(startButtonName);
            if (startButton != null)
            {
                startButton.clicked += HandleStartClicked;
            }
        }

        if (continueButton == null)
        {
            continueButton = root.Q<Button>(continueButtonName);
            if (continueButton != null)
            {
                continueButton.clicked += HandleContinueClicked;
            }
        }

        if (quitButton == null)
        {
            quitButton = root.Q<Button>(quitButtonName);
            if (quitButton != null)
            {
                quitButton.clicked += HandleQuitClicked;
            }
        }

        backgroundLayer ??= root.Q<VisualElement>(backgroundLayerName);
        fogWash ??= root.Q<VisualElement>(fogWashName);
        emberDot ??= root.Q<VisualElement>(emberDotName);

        if (backgroundLayer != null && backgroundImage != null)
        {
            backgroundLayer.style.backgroundImage = new StyleBackground(backgroundImage);
        }
    }

    private void UnbindUi()
    {
        if (startButton != null)
        {
            startButton.clicked -= HandleStartClicked;
            startButton = null;
        }

        if (continueButton != null)
        {
            continueButton.clicked -= HandleContinueClicked;
            continueButton = null;
        }

        if (quitButton != null)
        {
            quitButton.clicked -= HandleQuitClicked;
            quitButton = null;
        }
    }

    private void HandleStartClicked()
    {
        LoadGameplayScene();
    }

    private void HandleContinueClicked()
    {
        LoadGameplayScene();
    }

    private void HandleQuitClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadGameplayScene()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("MainMenuController is missing a gameplay scene name.");
            return;
        }

        isLoading = true;
        SetButtonsEnabled(false);
        Debug.Log($"[MainMenuController] Loading scene '{gameplaySceneName}'.", this);
        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        if (startButton != null)
        {
            startButton.SetEnabled(enabled);
        }

        if (continueButton != null)
        {
            continueButton.SetEnabled(enabled);
        }

        if (quitButton != null)
        {
            quitButton.SetEnabled(enabled);
        }
    }

    private void AnimateOverlay()
    {
        var time = Time.unscaledTime;

        if (fogWash != null)
        {
            fogWash.style.opacity = 0.82f + Mathf.Sin(time * fogPulseSpeed) * fogPulseAmplitude;
        }

        if (emberDot != null)
        {
            var emberPulse = 0.5f + 0.5f * Mathf.Sin(time * emberPulseSpeed);
            emberDot.style.opacity = Mathf.Lerp(emberMinOpacity, emberMaxOpacity, emberPulse);
        }
    }

    private void EnsureRuntimeInputSupport()
    {
        if (GetComponent<PanelRaycaster>() == null)
        {
            gameObject.AddComponent<PanelRaycaster>();
        }

        if (GetComponent<PanelEventHandler>() == null)
        {
            gameObject.AddComponent<PanelEventHandler>();
        }

        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#endif

        if (EventSystem.current != eventSystem)
        {
            EventSystem.current = eventSystem;
        }

        eventSystem.UpdateModules();
    }
}
