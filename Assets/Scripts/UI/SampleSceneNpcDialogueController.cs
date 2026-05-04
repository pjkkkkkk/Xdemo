using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

[AddComponentMenu("UI/Sample Scene NPC Dialogue Controller")]
[DisallowMultipleComponent]
public sealed class SampleSceneNpcDialogueController : MonoBehaviour
{
    private const string c_ShortEndingPhrase = "更短的结局";
    private const string c_StartCloseUpCue = "那就让我们开始吧";

    [Serializable]
    private sealed class DialogueLine
    {
        [TextArea(2, 4)]
        [SerializeField] private string m_Text = "...";
        [SerializeField, Min(0.5f)] private float m_Duration = 3.5f;

        public string Text => string.IsNullOrWhiteSpace(m_Text) ? "..." : m_Text.Trim();
        public float Duration => Mathf.Max(0.5f, m_Duration);
    }

    private sealed class RuntimeDialogueLine
    {
        public string speaker;
        public string text;
        public float duration;

        public RuntimeDialogueLine(string speaker, string text, float duration)
        {
            this.speaker = speaker;
            this.text = text;
            this.duration = duration;
        }
    }

    private struct CameraPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
        public bool orthographic;
        public float orthographicSize;
    }

    [Header("Dialogue")]
    [SerializeField] private string m_SpeakerName = "NPC";
    [SerializeField, Min(0f)] private float m_InitialDelay = 0.8f;
    [SerializeField] private bool m_UseUnscaledTime = true;
    [SerializeField] private bool m_HideAfterPlayback;
    [SerializeField] private bool m_LoadDialogueFromResources = true;
    [SerializeField] private string m_DialogueResourcePath = "intro";
    [SerializeField] private TextAsset m_DialogueTextAsset;
    [SerializeField] private bool m_RequireStartMenuRequestForIntro = true;
    [SerializeField] private string m_NarrationSpeakerName = "";
    [SerializeField, Min(0.5f)] private float m_MinTextLineDuration = 2.2f;
    [SerializeField, Min(0.5f)] private float m_MaxTextLineDuration = 7.5f;
    [SerializeField, Min(0.01f)] private float m_SecondsPerCharacter = 0.055f;
    [SerializeField] private List<DialogueLine> m_DialogueLines = new List<DialogueLine>
    {
        new DialogueLine()
    };

    [Header("Interaction")]
    [SerializeField] private bool m_AdvanceOnMouseClick = true;
    [SerializeField] private bool m_AdvanceOnTouch = true;

    [Header("Special Dialogue Effects")]
    [SerializeField] private Color m_ShortEndingColor = new Color(1f, 0.08f, 0.08f, 1f);
    [SerializeField, Range(0f, 12f)] private float m_ShortEndingJitterPixels = 4f;
    [SerializeField, Range(1f, 60f)] private float m_ShortEndingJitterFrequency = 28f;

    [Header("Camera Close-Up")]
    [SerializeField] private Camera m_CloseUpCamera;
    [SerializeField] private Transform m_CloseUpTarget;
    [SerializeField] private string m_CloseUpTargetName = "devil_boss";
    [SerializeField, Range(0.35f, 3f)] private float m_CloseUpDistance = 1.05f;
    [SerializeField, Range(18f, 60f)] private float m_CloseUpFieldOfView = 31f;
    [SerializeField, Range(0.05f, 2f)] private float m_CloseUpMoveSeconds = 0.45f;
    [SerializeField, Range(0f, 1f)] private float m_CloseUpHeadHeightPercent = 0.86f;

    [Header("Final Player View")]
    [SerializeField] private bool m_HideAfterFinalClick = true;
    [SerializeField] private bool m_MoveCameraToWizardAfterFinalClick = true;
    [SerializeField] private string m_FinalViewTargetName = "wizard";
    [SerializeField] private Vector3 m_WizardViewCameraOffset = new Vector3(-0.006f, 0.265f, -0.119f);
    [SerializeField] private Vector3 m_WizardViewRotationEuler = new Vector3(46.5f, 358.15f, 0.07f);
    [SerializeField, Range(24f, 70f)] private float m_WizardViewFieldOfView = 60f;
    [SerializeField, Range(0.05f, 2f)] private float m_FinalViewMoveSeconds = 0.55f;
    [SerializeField] private bool m_FollowWizardHorizontally = true;
    [SerializeField, Range(0f, 30f)] private float m_WizardViewFollowLerpSpeed = 18f;

    [Header("Wizard View Adjustment")]
    [SerializeField] private bool m_EnableWizardViewAdjustment = true;
    [SerializeField, Range(0f, 30f)] private float m_WizardViewMaxAdjustmentDegrees = 15f;
    [SerializeField, Range(15f, 180f)] private float m_WizardViewAdjustDegreesPerSecond = 72f;

    [Header("Map Overview")]
    [SerializeField] private bool m_ShowMapOverviewButton = true;
    [SerializeField] private string m_MapOverviewTargetName = "PhysicalRoguelikeMap";
    [SerializeField, Range(0.7f, 4f)] private float m_MapOverviewHeight = 2.25f;
    [SerializeField, Range(0.2f, 1f)] private float m_MapOverviewVisibleMapFraction = 0.333f;
    [SerializeField, Range(0.04f, 0.22f)] private float m_MapOverviewWizardBottomMargin = 0.12f;
    [SerializeField, Range(0.05f, 3f)] private float m_MapOverviewPanRange = 1.65f;
    [SerializeField, Range(0.0005f, 0.012f)] private float m_MapOverviewDragSensitivity = 0.003f;
    [SerializeField, Range(0.02f, 0.25f)] private float m_MapOverviewScrollSensitivity = 0.16f;
    [SerializeField, Range(0.05f, 2f)] private float m_MapOverviewMoveSeconds = 0.45f;
    [SerializeField] private Vector2 m_MapOverviewButtonPosition = new Vector2(-42f, 0f);
    [SerializeField, Range(56f, 120f)] private float m_MapOverviewButtonSize = 84f;

    [Header("Layout")]
    [SerializeField, Range(540f, 1280f)] private float m_BoxWidth = 920f;
    [SerializeField, Range(100f, 280f)] private float m_BoxHeight = 148f;
    [SerializeField, Range(16f, 180f)] private float m_BottomOffset = 44f;
    [SerializeField, Range(14, 40)] private int m_SpeakerFontSize = 18;
    [SerializeField, Range(18, 54)] private int m_BodyFontSize = 30;
    [SerializeField] private Color m_BackgroundColor = new Color(0.03f, 0.02f, 0.015f, 0.84f);
    [SerializeField] private Color m_AccentColor = new Color(0.91f, 0.64f, 0.24f, 1f);
    [SerializeField] private Color m_SpeakerColor = new Color(0.95f, 0.83f, 0.58f, 1f);
    [SerializeField] private Color m_TextColor = new Color(0.97f, 0.95f, 0.9f, 1f);

    private CanvasGroup m_CanvasGroup;
    private Coroutine m_PlaybackRoutine;
    private Text m_SpeakerText;
    private Text m_BodyText;
    private Text m_EmphasisText;
    private RectTransform m_EmphasisRect;
    private Vector2 m_EmphasisBasePosition;
    private bool m_IsEmphasisActive;
    private Camera m_OriginalCamera;
    private CameraPose m_OriginalCameraPose;
    private bool m_HasOriginalCameraPose;
    private CanvasGroup m_MapOverviewCanvasGroup;
    private Button m_MapOverviewButton;
    private Image m_MapOverviewButtonImage;
    private Image m_MapOverviewEyeImage;
    private Coroutine m_ViewRoutine;
    private bool m_IsMapOverviewActive;
    private float m_MapOverviewPanOffset;
    private bool m_HasPanPointerPosition;
    private Vector2 m_LastPanPointerPosition;
    private Vector2 m_WizardViewAngleOffset;
    private bool m_IsWizardViewActive;
    private bool m_HasWizardViewLockedCameraY;
    private float m_WizardViewLockedCameraY;

    private void Start()
    {
        EnsureUi();
        EnsureMapOverviewUi();
        SetMapOverviewControlsVisible(false);

        if (ShouldPlayIntroDialogue())
        {
            BeginPlayback();
            return;
        }

        StartCoroutine(EnterGameplayViewRoutine());
    }

    private void Update()
    {
        HandleWizardViewAdjustmentInput();
        HandleWizardViewHorizontalFollow();
        HandleMapOverviewPanInput();

        if (!m_IsEmphasisActive || m_EmphasisRect == null)
        {
            return;
        }

        float time = m_UseUnscaledTime ? Time.unscaledTime : Time.time;
        float frequency = Mathf.Max(1f, m_ShortEndingJitterFrequency);
        float amount = Mathf.Max(0f, m_ShortEndingJitterPixels);
        Vector2 offset = new Vector2(
            (Mathf.PerlinNoise(time * frequency, 0.17f) - 0.5f) * amount * 2f,
            (Mathf.PerlinNoise(0.71f, time * frequency) - 0.5f) * amount * 2f);

        m_EmphasisRect.anchoredPosition = m_EmphasisBasePosition + offset;
    }

    private void OnDisable()
    {
        if (m_PlaybackRoutine != null)
        {
            StopCoroutine(m_PlaybackRoutine);
            m_PlaybackRoutine = null;
        }

        SetShortEndingEmphasis(false);
        RestoreOriginalCameraPoseImmediate();

        if (m_ViewRoutine != null)
        {
            StopCoroutine(m_ViewRoutine);
            m_ViewRoutine = null;
        }
    }

    [ContextMenu("Replay Dialogue")]
    private void ReplayDialogue()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BeginPlayback();
    }

    public void BeginPlayback()
    {
        EnsureUi();
        SetMapOverviewControlsVisible(false);
        m_IsMapOverviewActive = false;

        if (m_PlaybackRoutine != null)
        {
            StopCoroutine(m_PlaybackRoutine);
        }

        m_PlaybackRoutine = StartCoroutine(PlayDialogueRoutine());
    }

    private bool ShouldPlayIntroDialogue()
    {
        if (!m_RequireStartMenuRequestForIntro)
        {
            return true;
        }

        return RoguelikeMapLaunchRequest.ConsumeIntroDialogueOnNextGameplayScene();
    }

    private IEnumerator EnterGameplayViewRoutine()
    {
        yield return null;

        if (m_MoveCameraToWizardAfterFinalClick)
        {
            yield return MoveCameraToWizardViewRoutine();
        }

        SetMapOverviewControlsVisible(true);
    }

    private IEnumerator PlayDialogueRoutine()
    {
        if (m_CanvasGroup == null || m_SpeakerText == null || m_BodyText == null)
        {
            yield break;
        }

        m_CanvasGroup.alpha = 0f;
        m_CanvasGroup.interactable = false;
        m_CanvasGroup.blocksRaycasts = false;
        List<RuntimeDialogueLine> playbackLines = BuildPlaybackLines();

        if (playbackLines.Count == 0)
        {
            yield return EnterGameplayViewRoutine();
            m_PlaybackRoutine = null;
            yield break;
        }

        yield return WaitForDuration(m_InitialDelay);

        m_CanvasGroup.alpha = 1f;
        m_CanvasGroup.interactable = true;
        m_CanvasGroup.blocksRaycasts = true;

        for (int i = 0; i < playbackLines.Count; i++)
        {
            RuntimeDialogueLine currentLine = playbackLines[i];
            if (currentLine == null || string.IsNullOrWhiteSpace(currentLine.text))
            {
                continue;
            }

            m_SpeakerText.text = currentLine.speaker;
            m_BodyText.text = ApplySpecialDialogueMarkup(currentLine.text);
            ConfigureShortEndingEmphasis(currentLine.text);

            bool shouldUseCloseUp = ContainsOrdinal(currentLine.text, c_StartCloseUpCue);
            if (shouldUseCloseUp)
            {
                yield return MoveCameraToCloseUpRoutine();
            }

            yield return WaitForAdvanceInput();

            SetShortEndingEmphasis(false);
            if (shouldUseCloseUp)
            {
                yield return RestoreOriginalCameraPoseRoutine();
            }
        }

        if (m_HideAfterPlayback || m_HideAfterFinalClick)
        {
            m_CanvasGroup.alpha = 0f;
        }

        m_CanvasGroup.interactable = false;
        m_CanvasGroup.blocksRaycasts = false;

        if (m_MoveCameraToWizardAfterFinalClick)
        {
            yield return MoveCameraToWizardViewRoutine();
        }

        SetMapOverviewControlsVisible(true);
        m_PlaybackRoutine = null;
    }

    private List<RuntimeDialogueLine> BuildPlaybackLines()
    {
        TextAsset textAsset = ResolveDialogueTextAsset();
        if (textAsset != null && !string.IsNullOrWhiteSpace(textAsset.text))
        {
            List<RuntimeDialogueLine> parsedLines = ParseDialogueText(textAsset.text);
            if (parsedLines.Count > 0)
            {
                return parsedLines;
            }
        }

        List<RuntimeDialogueLine> fallbackLines = new List<RuntimeDialogueLine>();
        string speakerName = ResolveSpeakerName(m_SpeakerName);
        if (m_DialogueLines == null)
        {
            return fallbackLines;
        }

        for (int i = 0; i < m_DialogueLines.Count; i++)
        {
            DialogueLine line = m_DialogueLines[i];
            if (line == null)
            {
                continue;
            }

            fallbackLines.Add(new RuntimeDialogueLine(speakerName, line.Text, line.Duration));
        }

        return fallbackLines;
    }

    private TextAsset ResolveDialogueTextAsset()
    {
        if (!m_LoadDialogueFromResources)
        {
            return null;
        }

        if (m_DialogueTextAsset != null)
        {
            return m_DialogueTextAsset;
        }

        if (string.IsNullOrWhiteSpace(m_DialogueResourcePath))
        {
            return null;
        }

        return Resources.Load<TextAsset>(m_DialogueResourcePath.Trim());
    }

    private List<RuntimeDialogueLine> ParseDialogueText(string rawText)
    {
        List<RuntimeDialogueLine> result = new List<RuntimeDialogueLine>();
        List<string> blocks = SplitDialogueBlocks(rawText);

        for (int i = 0; i < blocks.Count; i++)
        {
            RuntimeDialogueLine line = ParseDialogueBlock(blocks[i]);
            if (line != null)
            {
                result.Add(line);
            }
        }

        return result;
    }

    private RuntimeDialogueLine ParseDialogueBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return null;
        }

        string[] rawLines = block.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (rawLines.Length == 0)
        {
            return null;
        }

        int bodyStartIndex = 0;
        string speaker = ResolveSpeakerName(m_NarrationSpeakerName);
        string firstLine = rawLines[0].Trim();

        if (TryExtractSpeaker(firstLine, out string parsedSpeaker, out string inlineBody))
        {
            speaker = ResolveSpeakerName(parsedSpeaker);
            bodyStartIndex = 1;
        }

        StringBuilder bodyBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(inlineBody))
        {
            bodyBuilder.AppendLine(inlineBody.Trim());
        }

        for (int i = bodyStartIndex; i < rawLines.Length; i++)
        {
            string text = rawLines[i].Trim();
            if (text.Length == 0)
            {
                continue;
            }

            bodyBuilder.AppendLine(text);
        }

        string body = bodyBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        float duration = Mathf.Clamp(body.Replace("*", string.Empty).Length * m_SecondsPerCharacter, m_MinTextLineDuration, m_MaxTextLineDuration);
        return new RuntimeDialogueLine(speaker, ConvertSimpleMarkdownToRichText(body), duration);
    }

    private string ApplySpecialDialogueMarkup(string text)
    {
        if (string.IsNullOrEmpty(text) || !ContainsOrdinal(text, c_ShortEndingPhrase))
        {
            return text;
        }

        string color = ColorUtility.ToHtmlStringRGB(m_ShortEndingColor);
        return text.Replace(c_ShortEndingPhrase, $"<color=#{color}><b>{c_ShortEndingPhrase}</b></color>");
    }

    private static List<string> SplitDialogueBlocks(string rawText)
    {
        List<string> blocks = new List<string>();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return blocks;
        }

        string normalizedText = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalizedText.Split('\n');
        StringBuilder blockBuilder = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                FlushDialogueBlock(blockBuilder, blocks);
                continue;
            }

            blockBuilder.AppendLine(lines[i].Trim());
        }

        FlushDialogueBlock(blockBuilder, blocks);
        return blocks;
    }

    private static void FlushDialogueBlock(StringBuilder blockBuilder, List<string> blocks)
    {
        if (blockBuilder.Length == 0)
        {
            return;
        }

        string block = blockBuilder.ToString().Trim();
        if (block.Length > 0)
        {
            blocks.Add(block);
        }

        blockBuilder.Length = 0;
    }

    private static bool TryExtractSpeaker(string line, out string speaker, out string inlineBody)
    {
        speaker = null;
        inlineBody = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        int chineseColonIndex = line.IndexOf('：');
        int colonIndex = line.IndexOf(':');
        int separatorIndex = chineseColonIndex >= 0 ? chineseColonIndex : colonIndex;
        if (separatorIndex <= 0)
        {
            return false;
        }

        speaker = line.Substring(0, separatorIndex).Trim();
        inlineBody = line.Substring(separatorIndex + 1).Trim();
        return speaker.Length > 0;
    }

    private static string ResolveSpeakerName(string speakerName)
    {
        return string.IsNullOrWhiteSpace(speakerName) ? string.Empty : speakerName.Trim();
    }

    private static string ConvertSimpleMarkdownToRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(text.Length);
        bool bold = false;
        bool italic = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                builder.Append(bold ? "</b>" : "<b>");
                bold = !bold;
                i++;
                continue;
            }

            if (text[i] == '*')
            {
                builder.Append(italic ? "</i>" : "<i>");
                italic = !italic;
                continue;
            }

            builder.Append(text[i]);
        }

        if (italic)
        {
            builder.Append("</i>");
        }

        if (bold)
        {
            builder.Append("</b>");
        }

        return builder.ToString();
    }

    private IEnumerator WaitForDuration(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        if (m_UseUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield break;
        }

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator WaitForAdvanceInput()
    {
        yield return null;

        while (!AdvanceInputPressed())
        {
            yield return null;
        }
    }

    private bool AdvanceInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (m_AdvanceOnMouseClick && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (m_AdvanceOnTouch && UnityEngine.InputSystem.Touchscreen.current != null && UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (m_AdvanceOnMouseClick && Input.GetMouseButtonDown(0))
            {
                return true;
            }

            if (m_AdvanceOnTouch && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#endif

        return false;
    }

    private void ConfigureShortEndingEmphasis(string text)
    {
        if (m_EmphasisText == null || m_EmphasisRect == null || string.IsNullOrEmpty(text) || !ContainsOrdinal(text, c_ShortEndingPhrase))
        {
            SetShortEndingEmphasis(false);
            return;
        }

        string plainText = StripRichTextTags(text);
        if (!TryFindPhraseLine(plainText, c_ShortEndingPhrase, out int lineIndex, out int lineCount, out string linePrefix))
        {
            SetShortEndingEmphasis(false);
            return;
        }

        float prefixWidth = CalculateTextWidth(m_BodyText, linePrefix);
        float phraseWidth = Mathf.Max(1f, CalculateTextWidth(m_BodyText, c_ShortEndingPhrase));
        float lineHeight = Mathf.Max(1f, m_BodyText.fontSize * m_BodyText.lineSpacing);
        float yOffset = ((lineCount - 1) * lineHeight * 0.5f) - (lineIndex * lineHeight);

        m_EmphasisText.text = c_ShortEndingPhrase;
        m_EmphasisText.color = m_ShortEndingColor;
        m_EmphasisRect.sizeDelta = new Vector2(phraseWidth + m_ShortEndingJitterPixels * 4f, lineHeight * 1.5f);
        m_EmphasisBasePosition = new Vector2(prefixWidth, yOffset);
        m_EmphasisRect.anchoredPosition = m_EmphasisBasePosition;
        SetShortEndingEmphasis(true);
    }

    private void SetShortEndingEmphasis(bool active)
    {
        m_IsEmphasisActive = active;

        if (m_EmphasisText == null || m_EmphasisRect == null)
        {
            return;
        }

        m_EmphasisText.enabled = active;
        if (!active)
        {
            m_EmphasisText.text = string.Empty;
            m_EmphasisRect.anchoredPosition = m_EmphasisBasePosition;
        }
    }

    private IEnumerator MoveCameraToCloseUpRoutine()
    {
        Camera closeUpCamera = ResolveCloseUpCamera();
        if (closeUpCamera == null || !TryResolveCloseUpTargetPoint(out Vector3 targetPoint))
        {
            yield break;
        }

        CaptureOriginalCameraPose(closeUpCamera);

        Vector3 lookDirection = targetPoint - closeUpCamera.transform.position;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookDirection = closeUpCamera.transform.forward;
        }

        lookDirection.Normalize();
        Vector3 targetPosition = targetPoint - lookDirection * m_CloseUpDistance;
        Quaternion targetRotation = Quaternion.LookRotation((targetPoint - targetPosition).normalized, Vector3.up);

        yield return MoveCameraRoutine(closeUpCamera, targetPosition, targetRotation, m_CloseUpFieldOfView, m_CloseUpMoveSeconds);
    }

    private IEnumerator RestoreOriginalCameraPoseRoutine()
    {
        if (!m_HasOriginalCameraPose || m_OriginalCamera == null)
        {
            yield break;
        }

        yield return MoveCameraToPoseRoutine(m_OriginalCamera, m_OriginalCameraPose, m_CloseUpMoveSeconds);
        m_HasOriginalCameraPose = false;
        m_OriginalCamera = null;
    }

    private IEnumerator MoveCameraToWizardViewRoutine()
    {
        Camera targetCamera = ResolveCloseUpCamera();
        if (targetCamera == null || !TryBuildWizardViewPose(targetCamera, out CameraPose targetPose))
        {
            yield break;
        }

        m_IsMapOverviewActive = false;
        m_MapOverviewPanOffset = 0f;
        UpdateMapOverviewButtonVisualState(false);
        CaptureWizardViewLockedCameraHeight(targetPose);
        yield return MoveCameraToPoseRoutine(targetCamera, targetPose, m_FinalViewMoveSeconds);
        m_IsWizardViewActive = true;
    }

    private bool TryBuildWizardViewPose(Camera targetCamera, out CameraPose pose)
    {
        pose = default;

        if (!TryGetWizardViewTargetCenter(out Vector3 center))
        {
            return false;
        }

        pose.position = center + m_WizardViewCameraOffset;
        if (m_HasWizardViewLockedCameraY)
        {
            pose.position.y = m_WizardViewLockedCameraY;
        }

        pose.rotation = Quaternion.Euler(m_WizardViewRotationEuler + new Vector3(m_WizardViewAngleOffset.y, m_WizardViewAngleOffset.x, 0f));
        pose.fieldOfView = m_WizardViewFieldOfView;
        pose.orthographic = false;
        pose.orthographicSize = targetCamera != null ? targetCamera.orthographicSize : 5f;
        return true;
    }

    private bool TryGetWizardViewTargetCenter(out Vector3 center)
    {
        center = Vector3.zero;

        Transform target = FindTransformByName(m_FinalViewTargetName);
        if (target == null)
        {
            return false;
        }

        center = target.position;
        Bounds bounds;
        if (TryGetRendererBounds(target, out bounds))
        {
            center = bounds.center;
        }

        return true;
    }

    private void CaptureWizardViewLockedCameraHeight(CameraPose pose)
    {
        m_WizardViewLockedCameraY = pose.position.y;
        m_HasWizardViewLockedCameraY = true;
    }

    private IEnumerator MoveCameraToPoseRoutine(Camera targetCamera, CameraPose targetPose, float duration)
    {
        if (targetCamera == null)
        {
            yield break;
        }

        targetCamera.orthographic = targetPose.orthographic;
        yield return MoveCameraRoutine(
            targetCamera,
            targetPose.position,
            targetPose.rotation,
            targetPose.fieldOfView,
            targetPose.orthographicSize,
            duration);
    }

    private IEnumerator MoveCameraRoutine(Camera targetCamera, Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration)
    {
        float targetOrthographicSize = targetCamera != null ? targetCamera.orthographicSize : 5f;
        yield return MoveCameraRoutine(targetCamera, targetPosition, targetRotation, targetFieldOfView, targetOrthographicSize, duration);
    }

    private IEnumerator MoveCameraRoutine(Camera targetCamera, Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float targetOrthographicSize, float duration)
    {
        if (targetCamera == null)
        {
            yield break;
        }

        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        float startFieldOfView = targetCamera.fieldOfView;
        float startOrthographicSize = targetCamera.orthographicSize;
        float seconds = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            t = t * t * (3f - 2f * t);

            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, t);
            targetCamera.orthographicSize = Mathf.Lerp(startOrthographicSize, targetOrthographicSize, t);
            yield return null;
        }

        targetCamera.transform.position = targetPosition;
        targetCamera.transform.rotation = targetRotation;
        targetCamera.fieldOfView = targetFieldOfView;
        targetCamera.orthographicSize = targetOrthographicSize;
    }

    private void ToggleMapOverview()
    {
        if (!m_ShowMapOverviewButton)
        {
            return;
        }

        if (m_ViewRoutine != null)
        {
            StopCoroutine(m_ViewRoutine);
            m_ViewRoutine = null;
        }

        m_ViewRoutine = StartCoroutine(m_IsMapOverviewActive ? ReturnToWizardViewRoutine() : MoveToMapOverviewRoutine());
    }

    private IEnumerator MoveToMapOverviewRoutine()
    {
        Camera targetCamera = ResolveCloseUpCamera();
        if (targetCamera == null || !TryBuildMapOverviewPose(targetCamera, out CameraPose targetPose))
        {
            m_ViewRoutine = null;
            yield break;
        }

        m_IsMapOverviewActive = true;
        m_IsWizardViewActive = false;
        UpdateMapOverviewButtonVisualState(true);
        yield return MoveCameraToPoseRoutine(targetCamera, targetPose, m_MapOverviewMoveSeconds);
        m_ViewRoutine = null;
    }

    private IEnumerator ReturnToWizardViewRoutine()
    {
        m_IsMapOverviewActive = false;
        m_MapOverviewPanOffset = 0f;
        UpdateMapOverviewButtonVisualState(false);
        yield return MoveCameraToWizardViewRoutine();
        m_ViewRoutine = null;
    }

    private bool TryBuildMapOverviewPose(Camera targetCamera, out CameraPose pose)
    {
        pose = default;
        if (!TryResolveMapBounds(out Bounds bounds))
        {
            return false;
        }

        float orthographicSize = CalculateMapOverviewOrthographicSize(bounds);
        float baseCenterZ = CalculateMapOverviewBaseCenterZ(bounds, orthographicSize);
        m_MapOverviewPanOffset = ClampMapOverviewPanOffset(m_MapOverviewPanOffset, bounds, orthographicSize, baseCenterZ);

        pose.position = new Vector3(bounds.center.x, bounds.max.y + m_MapOverviewHeight, baseCenterZ + m_MapOverviewPanOffset);
        pose.rotation = Quaternion.Euler(90f, 0f, 0f);
        pose.fieldOfView = targetCamera != null ? targetCamera.fieldOfView : m_WizardViewFieldOfView;
        pose.orthographic = true;
        pose.orthographicSize = orthographicSize;
        return true;
    }

    private float CalculateMapOverviewOrthographicSize(Bounds bounds)
    {
        float visibleFraction = Mathf.Clamp(m_MapOverviewVisibleMapFraction, 0.2f, 1f);
        return Mathf.Max(0.18f, bounds.extents.z * visibleFraction);
    }

    private float CalculateMapOverviewBaseCenterZ(Bounds bounds, float orthographicSize)
    {
        Vector3 anchor = bounds.center;
        Transform wizard = FindTransformByName(m_FinalViewTargetName);
        if (wizard != null)
        {
            anchor = wizard.position;
            Bounds wizardBounds;
            if (TryGetRendererBounds(wizard, out wizardBounds))
            {
                anchor = wizardBounds.center;
            }
        }

        float bottomMargin = Mathf.Clamp(m_MapOverviewWizardBottomMargin, 0.04f, 0.22f);
        return anchor.z + orthographicSize * (1f - (bottomMargin * 2f));
    }

    private float ClampMapOverviewPanOffset(float requestedOffset, Bounds bounds, float orthographicSize, float baseCenterZ)
    {
        float range = Mathf.Max(0.01f, m_MapOverviewPanRange);
        float clampedOffset = Mathf.Clamp(requestedOffset, -range, range);

        float minCenterZ = bounds.min.z + orthographicSize;
        float maxCenterZ = bounds.max.z - orthographicSize;
        if (minCenterZ > maxCenterZ)
        {
            return 0f;
        }

        float centerZ = Mathf.Clamp(baseCenterZ + clampedOffset, minCenterZ, maxCenterZ);
        return centerZ - baseCenterZ;
    }

    private bool TryResolveMapBounds(out Bounds bounds)
    {
        Transform mapTarget = FindTransformByName(m_MapOverviewTargetName);
        if (mapTarget != null && TryGetRendererBounds(mapTarget, out bounds))
        {
            return true;
        }

        RoguelikeMapGenerator[] generators = FindObjectsByType<RoguelikeMapGenerator>(FindObjectsInactive.Include);
        for (int i = 0; i < generators.Length; i++)
        {
            if (generators[i] != null && TryGetRendererBounds(generators[i].transform, out bounds))
            {
                return true;
            }
        }

        bounds = default;
        return false;
    }

    private void HandleWizardViewAdjustmentInput()
    {
        if (!m_EnableWizardViewAdjustment || m_IsMapOverviewActive || m_PlaybackRoutine != null || m_ViewRoutine != null)
        {
            return;
        }

        Vector2 input = ReadWizardViewAdjustmentInput();
        if (input.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        float deltaTime = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float step = Mathf.Max(1f, m_WizardViewAdjustDegreesPerSecond) * deltaTime;
        float limit = Mathf.Max(0f, m_WizardViewMaxAdjustmentDegrees);
        Vector2 nextOffset = m_WizardViewAngleOffset;
        nextOffset.x = Mathf.Clamp(nextOffset.x + input.x * step, -limit, limit);
        nextOffset.y = Mathf.Clamp(nextOffset.y - input.y * step, -limit, limit);

        if ((nextOffset - m_WizardViewAngleOffset).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        m_WizardViewAngleOffset = nextOffset;
        ApplyWizardViewCameraPoseImmediate();
    }

    private void ApplyWizardViewCameraPoseImmediate()
    {
        Camera targetCamera = ResolveCloseUpCamera();
        if (targetCamera == null || !TryBuildWizardViewPose(targetCamera, out CameraPose pose))
        {
            return;
        }

        targetCamera.orthographic = pose.orthographic;
        targetCamera.transform.position = pose.position;
        targetCamera.transform.rotation = pose.rotation;
        targetCamera.fieldOfView = pose.fieldOfView;
        targetCamera.orthographicSize = pose.orthographicSize;
    }

    private void HandleWizardViewHorizontalFollow()
    {
        if (!m_FollowWizardHorizontally || !m_IsWizardViewActive || m_IsMapOverviewActive || m_PlaybackRoutine != null || m_ViewRoutine != null)
        {
            return;
        }

        Camera targetCamera = ResolveCloseUpCamera();
        if (targetCamera == null || !TryBuildWizardViewPose(targetCamera, out CameraPose pose))
        {
            return;
        }

        Vector3 currentPosition = targetCamera.transform.position;
        Vector3 targetPosition = new Vector3(pose.position.x, currentPosition.y, pose.position.z);
        if (m_HasWizardViewLockedCameraY)
        {
            targetPosition.y = m_WizardViewLockedCameraY;
        }

        float deltaTime = m_UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float followSpeed = Mathf.Max(0f, m_WizardViewFollowLerpSpeed);
        targetCamera.transform.position = followSpeed <= 0f
            ? targetPosition
            : Vector3.Lerp(currentPosition, targetPosition, Mathf.Clamp01(deltaTime * followSpeed));
    }

    private static Vector2 ReadWizardViewAdjustmentInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            {
                input.x -= 1f;
            }

            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            {
                input.x += 1f;
            }

            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
            {
                input.y += 1f;
            }

            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
            {
                input.y -= 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            {
                input.x -= 1f;
            }

            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            {
                input.x += 1f;
            }

            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
            {
                input.y += 1f;
            }

            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
            {
                input.y -= 1f;
            }
        }
        catch (InvalidOperationException)
        {
            return input;
        }
#endif

        return input;
    }

    private void HandleMapOverviewPanInput()
    {
        if (!m_IsMapOverviewActive || m_ViewRoutine != null)
        {
            m_HasPanPointerPosition = false;
            return;
        }

        float scrollDelta = ReadOverviewScrollDelta();
        if (Mathf.Abs(scrollDelta) > 0.001f)
        {
            AdjustMapOverviewPan(scrollDelta * m_MapOverviewScrollSensitivity);
        }

        if (!TryReadPanPointer(out Vector2 pointerPosition, out bool isPressed) || IsPointerOverUi())
        {
            m_HasPanPointerPosition = false;
            return;
        }

        if (!isPressed)
        {
            m_HasPanPointerPosition = false;
            return;
        }

        if (m_HasPanPointerPosition)
        {
            float deltaY = pointerPosition.y - m_LastPanPointerPosition.y;
            AdjustMapOverviewPan(deltaY * m_MapOverviewDragSensitivity);
        }

        m_LastPanPointerPosition = pointerPosition;
        m_HasPanPointerPosition = true;
    }

    private void AdjustMapOverviewPan(float delta)
    {
        float newOffset = m_MapOverviewPanOffset + delta;
        if (TryResolveMapBounds(out Bounds bounds))
        {
            float orthographicSize = CalculateMapOverviewOrthographicSize(bounds);
            float baseCenterZ = CalculateMapOverviewBaseCenterZ(bounds, orthographicSize);
            newOffset = ClampMapOverviewPanOffset(newOffset, bounds, orthographicSize, baseCenterZ);
        }

        if (Mathf.Approximately(newOffset, m_MapOverviewPanOffset))
        {
            return;
        }

        m_MapOverviewPanOffset = newOffset;
        ApplyMapOverviewCameraPoseImmediate();
    }

    private void ApplyMapOverviewCameraPoseImmediate()
    {
        Camera targetCamera = ResolveCloseUpCamera();
        if (targetCamera == null || !TryBuildMapOverviewPose(targetCamera, out CameraPose pose))
        {
            return;
        }

        targetCamera.orthographic = pose.orthographic;
        targetCamera.transform.position = pose.position;
        targetCamera.transform.rotation = pose.rotation;
        targetCamera.fieldOfView = pose.fieldOfView;
        targetCamera.orthographicSize = pose.orthographicSize;
    }

    private static float ReadOverviewScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.scroll.ReadValue().y / 120f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return Input.mouseScrollDelta.y;
        }
        catch (InvalidOperationException)
        {
            return 0f;
        }
#else
        return 0f;
#endif
    }

    private static bool TryReadPanPointer(out Vector2 position, out bool isPressed)
    {
        position = Vector2.zero;
        isPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            isPressed = true;
            return true;
        }

        if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            isPressed = Mouse.current.leftButton.isPressed;
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                position = touch.position;
                isPressed = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Began;
                return true;
            }

            position = Input.mousePosition;
            isPressed = Input.GetMouseButton(0);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            if (EventSystem.current.IsPointerOverGameObject(touchId))
            {
                return true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#endif

        return EventSystem.current.IsPointerOverGameObject();
    }

    private Camera ResolveCloseUpCamera()
    {
        if (m_CloseUpCamera != null)
        {
            return m_CloseUpCamera;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void CaptureOriginalCameraPose(Camera targetCamera)
    {
        if (m_HasOriginalCameraPose || targetCamera == null)
        {
            return;
        }

        m_OriginalCamera = targetCamera;
        m_OriginalCameraPose = new CameraPose
        {
            position = targetCamera.transform.position,
            rotation = targetCamera.transform.rotation,
            fieldOfView = targetCamera.fieldOfView,
            orthographic = targetCamera.orthographic,
            orthographicSize = targetCamera.orthographicSize
        };
        m_HasOriginalCameraPose = true;
    }

    private void RestoreOriginalCameraPoseImmediate()
    {
        if (!m_HasOriginalCameraPose || m_OriginalCamera == null)
        {
            return;
        }

        m_OriginalCamera.transform.position = m_OriginalCameraPose.position;
        m_OriginalCamera.transform.rotation = m_OriginalCameraPose.rotation;
        m_OriginalCamera.fieldOfView = m_OriginalCameraPose.fieldOfView;
        m_OriginalCamera.orthographic = m_OriginalCameraPose.orthographic;
        m_OriginalCamera.orthographicSize = m_OriginalCameraPose.orthographicSize;
        m_HasOriginalCameraPose = false;
        m_OriginalCamera = null;
    }

    private bool TryResolveCloseUpTargetPoint(out Vector3 targetPoint)
    {
        Transform target = ResolveCloseUpTarget();
        if (target == null)
        {
            targetPoint = Vector3.zero;
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            targetPoint = target.position + Vector3.up * 0.7f;
            return true;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        float headHeight = Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(m_CloseUpHeadHeightPercent));
        targetPoint = new Vector3(bounds.center.x, headHeight, bounds.center.z);
        return true;
    }

    private Transform ResolveCloseUpTarget()
    {
        if (m_CloseUpTarget != null)
        {
            return m_CloseUpTarget;
        }

        if (string.IsNullOrWhiteSpace(m_CloseUpTargetName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == m_CloseUpTargetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderers[i].bounds);
        }

        return hasBounds;
    }

    private static bool TryFindPhraseLine(string text, string phrase, out int lineIndex, out int lineCount, out string linePrefix)
    {
        lineIndex = 0;
        lineCount = 0;
        linePrefix = string.Empty;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(phrase))
        {
            return false;
        }

        string normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalizedText.Split('\n');
        lineCount = Mathf.Max(1, lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            int phraseIndex = lines[i].IndexOf(phrase, StringComparison.Ordinal);
            if (phraseIndex < 0)
            {
                continue;
            }

            lineIndex = i;
            linePrefix = lines[i].Substring(0, phraseIndex);
            return true;
        }

        return false;
    }

    private static float CalculateTextWidth(Text text, string value)
    {
        if (text == null || string.IsNullOrEmpty(value))
        {
            return 0f;
        }

        TextGenerationSettings settings = text.GetGenerationSettings(new Vector2(10000f, text.rectTransform.rect.height));
        settings.horizontalOverflow = HorizontalWrapMode.Overflow;
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        settings.richText = false;

        return text.cachedTextGeneratorForLayout.GetPreferredWidth(value, settings) / text.pixelsPerUnit;
    }

    private static string StripRichTextTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(text.Length);
        bool insideTag = false;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>' && insideTag)
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool ContainsOrdinal(string source, string value)
    {
        return !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(value) && source.IndexOf(value, StringComparison.Ordinal) >= 0;
    }

    private void EnsureMapOverviewUi()
    {
        if (!m_ShowMapOverviewButton || m_MapOverviewCanvasGroup != null)
        {
            return;
        }

        EnsureRuntimeInputSupport();

        GameObject canvasObject = new GameObject("SampleSceneViewControlsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 230;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        m_MapOverviewCanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        m_MapOverviewCanvasGroup.alpha = 0f;
        m_MapOverviewCanvasGroup.interactable = false;
        m_MapOverviewCanvasGroup.blocksRaycasts = false;

        GameObject buttonObject = new GameObject("MapOverviewToggleButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Shadow));
        buttonObject.transform.SetParent(canvasObject.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = m_MapOverviewButtonPosition;
        buttonRect.sizeDelta = Vector2.one * m_MapOverviewButtonSize;

        m_MapOverviewButtonImage = buttonObject.GetComponent<Image>();
        m_MapOverviewButtonImage.sprite = CreateHexSprite(128, new Color(0.06f, 0.045f, 0.035f, 0.9f), new Color(0.91f, 0.64f, 0.24f, 1f), 7f);
        m_MapOverviewButtonImage.type = Image.Type.Simple;
        m_MapOverviewButtonImage.alphaHitTestMinimumThreshold = 0.08f;

        Shadow shadow = buttonObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -4f);

        m_MapOverviewButton = buttonObject.GetComponent<Button>();
        m_MapOverviewButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = m_MapOverviewButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.78f, 1f);
        colors.pressedColor = new Color(0.82f, 0.64f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        colors.colorMultiplier = 1f;
        m_MapOverviewButton.colors = colors;
        m_MapOverviewButton.onClick.AddListener(ToggleMapOverview);

        GameObject eyeObject = new GameObject("EyeIcon", typeof(RectTransform), typeof(Image));
        eyeObject.transform.SetParent(buttonObject.transform, false);

        RectTransform eyeRect = eyeObject.GetComponent<RectTransform>();
        eyeRect.anchorMin = new Vector2(0.5f, 0.5f);
        eyeRect.anchorMax = new Vector2(0.5f, 0.5f);
        eyeRect.pivot = new Vector2(0.5f, 0.5f);
        eyeRect.anchoredPosition = Vector2.zero;
        eyeRect.sizeDelta = Vector2.one * (m_MapOverviewButtonSize * 0.48f);

        m_MapOverviewEyeImage = eyeObject.GetComponent<Image>();
        m_MapOverviewEyeImage.sprite = CreateEyeSprite(96, new Color(0.98f, 0.9f, 0.72f, 1f));
        m_MapOverviewEyeImage.raycastTarget = false;
    }

    private void SetMapOverviewControlsVisible(bool visible)
    {
        if (!m_ShowMapOverviewButton)
        {
            return;
        }

        EnsureMapOverviewUi();
        if (m_MapOverviewCanvasGroup == null)
        {
            return;
        }

        m_MapOverviewCanvasGroup.alpha = visible ? 1f : 0f;
        m_MapOverviewCanvasGroup.interactable = visible;
        m_MapOverviewCanvasGroup.blocksRaycasts = visible;
    }

    private void UpdateMapOverviewButtonVisualState(bool active)
    {
        if (m_MapOverviewButtonImage != null)
        {
            m_MapOverviewButtonImage.color = active ? new Color(1f, 0.86f, 0.55f, 1f) : Color.white;
        }

        if (m_MapOverviewEyeImage != null)
        {
            m_MapOverviewEyeImage.color = active ? new Color(0.12f, 0.075f, 0.035f, 1f) : Color.white;
        }
    }

    private static void EnsureRuntimeInputSupport()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif

        if (EventSystem.current != eventSystem)
        {
            EventSystem.current = eventSystem;
        }

        eventSystem.UpdateModules();
    }

    private static Sprite CreateHexSprite(int size, Color fillColor, Color borderColor, float borderPixels)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Hex Button Sprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.47f;
        float innerRadius = Mathf.Max(1f, outerRadius - borderPixels);
        Vector2[] outer = BuildHexagon(center, outerRadius);
        Vector2[] inner = BuildHexagon(center, innerRadius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                bool insideOuter = IsPointInsideConvexPolygon(point, outer);
                if (!insideOuter)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool insideInner = IsPointInsideConvexPolygon(point, inner);
                texture.SetPixel(x, y, insideInner ? fillColor : borderColor);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateEyeSprite(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Eye Icon Sprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size * 2f) - 1f;
                float ny = ((y + 0.5f) / size * 2f) - 1f;
                float eyeLimit = (1f - (nx * nx)) * 0.46f;
                bool insideEye = Mathf.Abs(nx) <= 0.96f && Mathf.Abs(ny) <= eyeLimit;
                float pupilDistance = Mathf.Sqrt((nx * nx) + (ny * ny));
                bool insidePupil = pupilDistance <= 0.18f;
                bool insideIris = pupilDistance <= 0.32f;

                if (!insideEye)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                if (insidePupil)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color pixelColor = color;
                if (insideIris)
                {
                    pixelColor.a *= 0.78f;
                }

                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Vector2[] BuildHexagon(Vector2 center, float radius)
    {
        Vector2[] points = new Vector2[6];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (30f + (60f * i));
            points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return points;
    }

    private static bool IsPointInsideConvexPolygon(Vector2 point, Vector2[] polygon)
    {
        bool hasPositive = false;
        bool hasNegative = false;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            float cross = ((b.x - a.x) * (point.y - a.y)) - ((b.y - a.y) * (point.x - a.x));
            hasPositive |= cross > 0f;
            hasNegative |= cross < 0f;
            if (hasPositive && hasNegative)
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureUi()
    {
        if (m_CanvasGroup != null && m_SpeakerText != null && m_BodyText != null && m_EmphasisText != null)
        {
            return;
        }

        Font dialogueFont = ResolveDialogueFont();

        GameObject canvasObject = new GameObject("NpcDialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        m_CanvasGroup = canvasObject.GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha = 0f;
        m_CanvasGroup.interactable = false;
        m_CanvasGroup.blocksRaycasts = false;

        GameObject panelObject = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image), typeof(Shadow));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, m_BottomOffset);
        panelRect.sizeDelta = new Vector2(m_BoxWidth, m_BoxHeight);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = m_BackgroundColor;

        Shadow panelShadow = panelObject.GetComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        panelShadow.effectDistance = new Vector2(0f, -8f);

        GameObject accentObject = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(panelObject.transform, false);

        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.offsetMin = new Vector2(0f, -6f);
        accentRect.offsetMax = new Vector2(0f, 0f);

        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = m_AccentColor;

        m_SpeakerText = CreateText(
            "Speaker",
            panelObject.transform,
            dialogueFont,
            m_SpeakerFontSize,
            m_SpeakerColor,
            FontStyle.Bold,
            TextAnchor.UpperLeft);

        RectTransform speakerRect = m_SpeakerText.rectTransform;
        speakerRect.anchorMin = new Vector2(0.5f, 1f);
        speakerRect.anchorMax = new Vector2(0.5f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.anchoredPosition = new Vector2(0f, -18f);
        speakerRect.sizeDelta = new Vector2(m_BoxWidth - 52f, 24f);

        m_BodyText = CreateText(
            "Body",
            panelObject.transform,
            dialogueFont,
            m_BodyFontSize,
            m_TextColor,
            FontStyle.Normal,
            TextAnchor.MiddleLeft);

        RectTransform bodyRect = m_BodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -52f);
        bodyRect.sizeDelta = new Vector2(m_BoxWidth - 52f, m_BoxHeight - 72f);

        m_EmphasisText = CreateText(
            "ShortEndingEmphasis",
            m_BodyText.transform,
            dialogueFont,
            m_BodyFontSize,
            m_ShortEndingColor,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);

        m_EmphasisRect = m_EmphasisText.rectTransform;
        m_EmphasisRect.anchorMin = new Vector2(0f, 0.5f);
        m_EmphasisRect.anchorMax = new Vector2(0f, 0.5f);
        m_EmphasisRect.pivot = new Vector2(0f, 0.5f);
        m_EmphasisRect.anchoredPosition = Vector2.zero;
        m_EmphasisRect.sizeDelta = new Vector2(220f, m_BodyFontSize * 1.5f);
        m_EmphasisText.horizontalOverflow = HorizontalWrapMode.Overflow;
        m_EmphasisText.verticalOverflow = VerticalWrapMode.Overflow;
        m_EmphasisText.raycastTarget = false;
        m_EmphasisText.enabled = false;

        m_SpeakerText.text = string.IsNullOrWhiteSpace(m_SpeakerName) ? "NPC" : m_SpeakerName.Trim();
        m_BodyText.text = string.Empty;
    }

    private static Text CreateText(string objectName, Transform parent, Font font, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.1f;
        text.supportRichText = true;
        text.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);

        return text;
    }

    private static Font ResolveDialogueFont()
    {
        string[] preferredFonts =
        {
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Hiragino Sans GB",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "Arial Unicode MS"
        };

        try
        {
            Font dynamicFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 28);
            if (dynamicFont != null)
            {
                return dynamicFont;
            }
        }
        catch (Exception)
        {
            // Fall back to Unity's built-in runtime font.
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
