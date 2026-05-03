using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

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
    }

    [Header("Dialogue")]
    [SerializeField] private string m_SpeakerName = "NPC";
    [SerializeField, Min(0f)] private float m_InitialDelay = 0.8f;
    [SerializeField] private bool m_UseUnscaledTime = true;
    [SerializeField] private bool m_HideAfterPlayback;
    [SerializeField] private bool m_LoadDialogueFromResources = true;
    [SerializeField] private string m_DialogueResourcePath = "intro";
    [SerializeField] private TextAsset m_DialogueTextAsset;
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
    [SerializeField, Range(0.15f, 1.4f)] private float m_FinalViewBehindDistance = 0.32f;
    [SerializeField, Range(0.08f, 1.2f)] private float m_FinalViewHeightAbove = 0.28f;
    [SerializeField, Range(0f, 1.6f)] private float m_FinalViewLookAhead = 0.48f;
    [SerializeField, Range(24f, 70f)] private float m_FinalViewFieldOfView = 44f;
    [SerializeField, Range(0.05f, 2f)] private float m_FinalViewMoveSeconds = 0.55f;

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

    private void Start()
    {
        EnsureUi();
        BeginPlayback();
    }

    private void Update()
    {
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

        if (m_PlaybackRoutine != null)
        {
            StopCoroutine(m_PlaybackRoutine);
        }

        m_PlaybackRoutine = StartCoroutine(PlayDialogueRoutine());
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

        yield return MoveCameraRoutine(m_OriginalCamera, m_OriginalCameraPose.position, m_OriginalCameraPose.rotation, m_OriginalCameraPose.fieldOfView, m_CloseUpMoveSeconds);
        m_HasOriginalCameraPose = false;
        m_OriginalCamera = null;
    }

    private IEnumerator MoveCameraToWizardViewRoutine()
    {
        Camera targetCamera = ResolveCloseUpCamera();
        Transform target = FindTransformByName(m_FinalViewTargetName);
        if (targetCamera == null || target == null)
        {
            yield break;
        }

        Vector3 center = target.position;
        float topY = center.y;
        Bounds bounds;
        if (TryGetRendererBounds(target, out bounds))
        {
            center = bounds.center;
            topY = bounds.max.y;
        }

        Vector3 tableForward = Vector3.forward;
        Vector3 lookTarget = center + (tableForward * m_FinalViewLookAhead);
        lookTarget.y = center.y;

        Vector3 targetPosition = center - (tableForward * m_FinalViewBehindDistance);
        targetPosition.y = topY + m_FinalViewHeightAbove;

        Quaternion targetRotation = Quaternion.LookRotation((lookTarget - targetPosition).normalized, Vector3.up);
        yield return MoveCameraRoutine(targetCamera, targetPosition, targetRotation, m_FinalViewFieldOfView, m_FinalViewMoveSeconds);
    }

    private IEnumerator MoveCameraRoutine(Camera targetCamera, Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration)
    {
        if (targetCamera == null)
        {
            yield break;
        }

        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        float startFieldOfView = targetCamera.fieldOfView;
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
            yield return null;
        }

        targetCamera.transform.position = targetPosition;
        targetCamera.transform.rotation = targetRotation;
        targetCamera.fieldOfView = targetFieldOfView;
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
            fieldOfView = targetCamera.fieldOfView
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
