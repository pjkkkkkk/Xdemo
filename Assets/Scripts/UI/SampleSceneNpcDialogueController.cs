using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Sample Scene NPC Dialogue Controller")]
[DisallowMultipleComponent]
public sealed class SampleSceneNpcDialogueController : MonoBehaviour
{
    [Serializable]
    private sealed class DialogueLine
    {
        [TextArea(2, 4)]
        [SerializeField] private string m_Text = "...";
        [SerializeField, Min(0.5f)] private float m_Duration = 3.5f;

        public string Text => string.IsNullOrWhiteSpace(m_Text) ? "..." : m_Text.Trim();
        public float Duration => Mathf.Max(0.5f, m_Duration);
    }

    [Header("Dialogue")]
    [SerializeField] private string m_SpeakerName = "NPC";
    [SerializeField, Min(0f)] private float m_InitialDelay = 0.8f;
    [SerializeField] private bool m_UseUnscaledTime = true;
    [SerializeField] private bool m_HideAfterPlayback;
    [SerializeField] private List<DialogueLine> m_DialogueLines = new List<DialogueLine>
    {
        new DialogueLine()
    };

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

    private void Start()
    {
        EnsureUi();
        BeginPlayback();
    }

    private void OnDisable()
    {
        if (m_PlaybackRoutine != null)
        {
            StopCoroutine(m_PlaybackRoutine);
            m_PlaybackRoutine = null;
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

        if (m_DialogueLines == null || m_DialogueLines.Count == 0)
        {
            m_PlaybackRoutine = null;
            yield break;
        }

        yield return WaitForDuration(m_InitialDelay);

        m_SpeakerText.text = string.IsNullOrWhiteSpace(m_SpeakerName) ? "NPC" : m_SpeakerName.Trim();
        m_CanvasGroup.alpha = 1f;

        for (int i = 0; i < m_DialogueLines.Count; i++)
        {
            DialogueLine currentLine = m_DialogueLines[i];
            if (currentLine == null)
            {
                continue;
            }

            m_BodyText.text = currentLine.Text;
            yield return WaitForDuration(currentLine.Duration);
        }

        if (m_HideAfterPlayback)
        {
            m_CanvasGroup.alpha = 0f;
        }

        m_PlaybackRoutine = null;
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

    private void EnsureUi()
    {
        if (m_CanvasGroup != null && m_SpeakerText != null && m_BodyText != null)
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
        text.supportRichText = false;

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
