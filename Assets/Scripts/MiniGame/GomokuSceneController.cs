using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GomokuSceneController : MonoBehaviour
{
    private const int Empty = 0;
    private const int PlayerStone = 1;
    private const int AiStone = -1;
    private const int WinScore = 10000000;
    private const int LossScore = -WinScore;

    private static readonly Vector2Int[] LineDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1)
    };

    private struct ScoredMove
    {
        public Vector2Int move;
        public int score;

        public ScoredMove(Vector2Int move, int score)
        {
            this.move = move;
            this.score = score;
        }
    }

    private enum MatchOutcome
    {
        None,
        PlayerWin,
        PlayerLose,
        Draw
    }

    [Header("Board")]
    [SerializeField, Range(9, 19)] private int boardSize = 15;
    [SerializeField, Range(5, 7)] private int winLength = 5;

    [Header("Flow")]
    [SerializeField] private string fallbackReturnScene = "SampleScene";
    [SerializeField] private bool autoReturnOnPlayerWin = true;
    [SerializeField, Range(0.1f, 3f)] private float autoReturnDelay = 1.1f;

    [Header("AI")]
    [SerializeField, Range(1, 3)] private int aiSearchDepth = 2;
    [SerializeField, Range(6, 18)] private int aiCandidateLimit = 12;
    [SerializeField, Range(1, 2)] private int aiNeighborRadius = 2;
    [SerializeField, Range(0.8f, 1.5f)] private float aiDefenseBias = 1.15f;
    [SerializeField, Range(0f, 0.6f)] private float aiThinkDelay = 0.25f;

    [Header("UI")]
    [SerializeField, Range(24f, 88f)] private float topMargin = 48f;
    [SerializeField] private Color boardColorA = new Color(0.56f, 0.37f, 0.18f, 1f);
    [SerializeField] private Color boardColorB = new Color(0.64f, 0.44f, 0.23f, 1f);
    [SerializeField] private Color gridLineColor = new Color(0.22f, 0.14f, 0.06f, 0.95f);
    [SerializeField] private Color blackStoneColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color blackStoneEdgeColor = new Color(0.24f, 0.24f, 0.24f, 1f);
    [SerializeField] private Color whiteStoneColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color whiteStoneEdgeColor = new Color(0.58f, 0.52f, 0.42f, 1f);
    [SerializeField] private Color lastMoveHighlightColor = new Color(1f, 0.88f, 0.18f, 0.96f);
    [SerializeField, Range(0.6f, 0.92f)] private float stoneFillRatio = 0.78f;
    [SerializeField, Range(0.02f, 0.18f)] private float gridThicknessRatio = 0.06f;
    [SerializeField, Range(0.06f, 0.32f)] private float lastMoveRingThicknessRatio = 0.14f;
    [SerializeField] private bool showResultPopup = true;
    [SerializeField] private Color popupOverlayColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color popupPanelColor = new Color(0.19f, 0.12f, 0.06f, 0.95f);

    private readonly System.Random random = new System.Random();
    private int[,] board = new int[15, 15];
    private bool isPlayerTurn = true;
    private bool isGameOver;
    private bool aiMoveQueued;
    private bool returnQueued;
    private float aiMoveAt;
    private float returnAt;
    private string statusText = "";
    private Vector2Int lastMove = new Vector2Int(-1, -1);
    private MatchOutcome matchOutcome = MatchOutcome.None;
    private Texture2D blackStoneTexture;
    private Texture2D whiteStoneTexture;
    private Texture2D lastMoveRingTexture;

    private void Awake()
    {
        AllocateBoard();
        ResetGame();
    }

    private void Update()
    {
        if (aiMoveQueued && Time.unscaledTime >= aiMoveAt)
        {
            aiMoveQueued = false;
            PerformAiMove();
        }

        if (returnQueued && Time.unscaledTime >= returnAt)
        {
            returnQueued = false;
            ReturnToMap();
        }
    }

    private void OnGUI()
    {
        DrawStatusBar();
        DrawBoard();
        DrawBottomButtons();
        DrawResultPopup();
    }

    private void OnDestroy()
    {
        DestroyGeneratedTexture(ref blackStoneTexture);
        DestroyGeneratedTexture(ref whiteStoneTexture);
        DestroyGeneratedTexture(ref lastMoveRingTexture);
    }

    private void AllocateBoard()
    {
        boardSize = Mathf.Clamp(boardSize, 9, 19);
        winLength = Mathf.Clamp(winLength, 5, 7);
        board = new int[boardSize, boardSize];
    }

    private void ResetGame()
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                board[x, y] = Empty;
            }
        }

        isPlayerTurn = true;
        isGameOver = false;
        aiMoveQueued = false;
        returnQueued = false;
        lastMove = new Vector2Int(-1, -1);
        matchOutcome = MatchOutcome.None;
        statusText = "你执黑子先手，先连成五子获胜。";
    }

    private void DrawStatusBar()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };

        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(0f, 8f, Screen.width, 28f), "五子棋", titleStyle);
        GUI.Label(new Rect(16f, 36f, Screen.width - 32f, 44f), statusText, statusStyle);
    }

    private void DrawBoard()
    {
        float availableHeight = Mathf.Max(180f, Screen.height - topMargin - 180f);
        float boardPixelSize = Mathf.Min(Screen.width * 0.9f, availableHeight);
        float cellSize = Mathf.Max(18f, Mathf.Floor(boardPixelSize / boardSize));
        boardPixelSize = cellSize * boardSize;

        float originX = (Screen.width - boardPixelSize) * 0.5f;
        float originY = topMargin + 40f;

        EnsureVisualTextures();

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                Rect cellRect = new Rect(originX + (x * cellSize), originY + (y * cellSize), cellSize, cellSize);
                DrawSolidRect(cellRect, ((x + y) & 1) == 0 ? boardColorA : boardColorB);

                if (board[x, y] != Empty)
                {
                    continue;
                }

                bool canClick = !isGameOver && isPlayerTurn && !aiMoveQueued;
                GUI.enabled = canClick;
                if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                {
                    HandlePlayerMove(x, y);
                }
                GUI.enabled = true;
            }
        }

        DrawGrid(originX, originY, boardPixelSize, cellSize);

        float stoneSize = cellSize * Mathf.Clamp(stoneFillRatio, 0.6f, 0.92f);
        float stoneOffset = (cellSize - stoneSize) * 0.5f;
        float ringPadding = Mathf.Max(2f, cellSize * 0.08f);

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] == Empty)
                {
                    continue;
                }

                Rect stoneRect = new Rect(
                    originX + (x * cellSize) + stoneOffset,
                    originY + (y * cellSize) + stoneOffset,
                    stoneSize,
                    stoneSize);

                GUI.DrawTexture(
                    stoneRect,
                    board[x, y] == PlayerStone ? blackStoneTexture : whiteStoneTexture,
                    ScaleMode.StretchToFill,
                    true);

                if (lastMove.x == x && lastMove.y == y)
                {
                    GUI.DrawTexture(ExpandRect(stoneRect, ringPadding), lastMoveRingTexture, ScaleMode.StretchToFill, true);
                }
            }
        }
    }

    private void EnsureVisualTextures()
    {
        if (blackStoneTexture == null)
        {
            blackStoneTexture = CreateStoneTexture(128, blackStoneColor, blackStoneEdgeColor, 0.1f);
        }

        if (whiteStoneTexture == null)
        {
            whiteStoneTexture = CreateStoneTexture(128, whiteStoneColor, whiteStoneEdgeColor, 0.14f);
        }

        if (lastMoveRingTexture == null)
        {
            float ringThickness = Mathf.Clamp(lastMoveRingThicknessRatio, 0.06f, 0.32f);
            lastMoveRingTexture = CreateRingTexture(128, lastMoveHighlightColor, ringThickness);
        }
    }

    private Texture2D CreateStoneTexture(int size, Color fillColor, Color edgeColor, float edgeWidth)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (size - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float edgeT = Mathf.InverseLerp(1f - edgeWidth, 1f, distance);
                Color color = Color.Lerp(fillColor, edgeColor, edgeT);

                float alpha = 1f - Mathf.InverseLerp(0.96f, 1f, distance);
                color.a *= alpha;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private Texture2D CreateRingTexture(int size, Color ringColor, float ringThickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (size - 1) * 0.5f;
        float inner = Mathf.Clamp01(1f - ringThickness);
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (distance < inner || distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float innerFade = Mathf.InverseLerp(inner, inner + 0.05f, distance);
                float outerFade = 1f - Mathf.InverseLerp(0.95f, 1f, distance);
                Color color = ringColor;
                color.a *= Mathf.Clamp01(Mathf.Min(innerFade, outerFade));
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private void DrawGrid(float originX, float originY, float boardPixelSize, float cellSize)
    {
        float lineThickness = Mathf.Max(1f, cellSize * Mathf.Clamp(gridThicknessRatio, 0.02f, 0.18f));

        for (int i = 0; i <= boardSize; i++)
        {
            float x = originX + (i * cellSize) - (lineThickness * 0.5f);
            DrawSolidRect(new Rect(x, originY, lineThickness, boardPixelSize), gridLineColor);

            float y = originY + (i * cellSize) - (lineThickness * 0.5f);
            DrawSolidRect(new Rect(originX, y, boardPixelSize, lineThickness), gridLineColor);
        }
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color cached = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
        GUI.color = cached;
    }

    private static Rect ExpandRect(Rect rect, float amount)
    {
        return new Rect(rect.x - amount, rect.y - amount, rect.width + (amount * 2f), rect.height + (amount * 2f));
    }

    private static void DestroyGeneratedTexture(ref Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(texture);
        }
        else
        {
            Object.DestroyImmediate(texture);
        }

        texture = null;
    }

    private void DrawBottomButtons()
    {
        if (isGameOver && showResultPopup)
        {
            return;
        }

        float buttonWidth = Mathf.Min(220f, Screen.width * 0.4f);
        float y = Screen.height - 106f;

        Rect restartRect = new Rect((Screen.width * 0.5f) - buttonWidth - 12f, y, buttonWidth, 42f);
        Rect returnRect = new Rect((Screen.width * 0.5f) + 12f, y, buttonWidth, 42f);

        if (GUI.Button(restartRect, "重新开始"))
        {
            ResetGame();
        }

        if (GUI.Button(returnRect, "返回地图"))
        {
            ReturnToMap();
        }
    }

    private void DrawResultPopup()
    {
        if (!showResultPopup || !isGameOver || matchOutcome == MatchOutcome.None)
        {
            return;
        }

        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), popupOverlayColor);

        float panelWidth = Mathf.Min(520f, Screen.width - 48f);
        float panelHeight = 220f;
        Rect panelRect = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        DrawSolidRect(panelRect, popupPanelColor);
        GUI.Box(panelRect, GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };

        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.95f, 0.92f, 0.85f, 1f) }
        };

        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 18f, panelRect.width - 32f, 42f), GetOutcomeTitle(), titleStyle);
        GUI.Label(new Rect(panelRect.x + 20f, panelRect.y + 64f, panelRect.width - 40f, 56f), GetOutcomeMessage(), bodyStyle);

        if (matchOutcome == MatchOutcome.PlayerWin)
        {
            Rect nextRect = new Rect(panelRect.x + (panelRect.width - 180f) * 0.5f, panelRect.y + panelRect.height - 62f, 180f, 40f);
            if (GUI.Button(nextRect, "下一关"))
            {
                ReturnToMap();
            }
            return;
        }

        float buttonWidth = (panelRect.width - 52f) * 0.5f;
        Rect retryRect = new Rect(panelRect.x + 16f, panelRect.y + panelRect.height - 62f, buttonWidth, 40f);
        Rect backRect = new Rect(retryRect.xMax + 20f, panelRect.y + panelRect.height - 62f, buttonWidth, 40f);

        if (GUI.Button(retryRect, "再试一次"))
        {
            ResetGame();
        }

        if (GUI.Button(backRect, "回到地图"))
        {
            ReturnToMap();
        }
    }

    private string GetOutcomeTitle()
    {
        switch (matchOutcome)
        {
            case MatchOutcome.PlayerWin:
                return "挑战成功";
            case MatchOutcome.PlayerLose:
                return "挑战失败";
            case MatchOutcome.Draw:
                return "本局平局";
            default:
                return "";
        }
    }

    private string GetOutcomeMessage()
    {
        switch (matchOutcome)
        {
            case MatchOutcome.PlayerWin:
                return "你已赢下本局，点击下一关返回地图并推进到后续节点。";
            case MatchOutcome.PlayerLose:
                return "这局没过关。可以再试一次，或者先回到地图。";
            case MatchOutcome.Draw:
                return "本局平局，暂不会推进地图。你可以继续挑战。";
            default:
                return "";
        }
    }

    private void HandlePlayerMove(int x, int y)
    {
        if (isGameOver || !isPlayerTurn || board[x, y] != Empty)
        {
            return;
        }

        board[x, y] = PlayerStone;
        lastMove = new Vector2Int(x, y);

        if (HasWonFrom(x, y, PlayerStone))
        {
            isGameOver = true;
            matchOutcome = MatchOutcome.PlayerWin;
            statusText = "你获胜了，地图会解锁到下一节点。";
            MiniGameFlowState.ReportGomokuResult(true);

            if (autoReturnOnPlayerWin && !showResultPopup)
            {
                returnQueued = true;
                returnAt = Time.unscaledTime + autoReturnDelay;
            }

            return;
        }

        if (IsBoardFull())
        {
            isGameOver = true;
            matchOutcome = MatchOutcome.Draw;
            statusText = "平局，本次不推进地图。";
            MiniGameFlowState.ReportGomokuResult(false);
            return;
        }

        isPlayerTurn = false;
        statusText = "电脑正在深度思考...";
        aiMoveQueued = true;
        aiMoveAt = Time.unscaledTime + Mathf.Clamp(aiThinkDelay, 0f, 0.6f);
    }

    private void PerformAiMove()
    {
        if (isGameOver)
        {
            return;
        }

        Vector2Int move;
        if (!TryFindImmediateMove(AiStone, out move) &&
            !TryFindImmediateMove(PlayerStone, out move) &&
            !TryFindStrategicMove(out move) &&
            !TryFindNeighborMove(out move))
        {
            if (!TryFindAnyEmpty(out move))
            {
                isGameOver = true;
                matchOutcome = MatchOutcome.Draw;
                statusText = "平局，本次不推进地图。";
                MiniGameFlowState.ReportGomokuResult(false);
                return;
            }
        }

        board[move.x, move.y] = AiStone;
        lastMove = new Vector2Int(move.x, move.y);

        if (HasWonFrom(move.x, move.y, AiStone))
        {
            isGameOver = true;
            matchOutcome = MatchOutcome.PlayerLose;
            statusText = "电脑获胜了，再试一次。";
            MiniGameFlowState.ReportGomokuResult(false);
            return;
        }

        if (IsBoardFull())
        {
            isGameOver = true;
            matchOutcome = MatchOutcome.Draw;
            statusText = "平局，本次不推进地图。";
            MiniGameFlowState.ReportGomokuResult(false);
            return;
        }

        isPlayerTurn = true;
        statusText = "轮到你了。";
    }

    private bool TryFindStrategicMove(out Vector2Int move)
    {
        int depth = Mathf.Clamp(aiSearchDepth, 1, 3);
        int rootLimit = Mathf.Clamp(aiCandidateLimit, 6, 24);
        List<ScoredMove> rootCandidates = BuildCandidateMoves(AiStone, rootLimit);

        if (rootCandidates.Count == 0)
        {
            move = Vector2Int.zero;
            return false;
        }

        int bestScore = LossScore;
        move = rootCandidates[0].move;
        int alpha = LossScore;
        int beta = WinScore;

        for (int i = 0; i < rootCandidates.Count; i++)
        {
            Vector2Int candidate = rootCandidates[i].move;
            board[candidate.x, candidate.y] = AiStone;

            int score;
            if (HasWonFrom(candidate.x, candidate.y, AiStone))
            {
                score = WinScore - 1;
            }
            else
            {
                score = -Negamax(depth - 1, -beta, -alpha, PlayerStone, 1);
            }

            board[candidate.x, candidate.y] = Empty;

            if (score > bestScore || (score == bestScore && random.NextDouble() < 0.2))
            {
                bestScore = score;
                move = candidate;
            }

            if (score > alpha)
            {
                alpha = score;
            }
        }

        return true;
    }

    private int Negamax(int depth, int alpha, int beta, int sideToMove, int ply)
    {
        if (depth <= 0 || IsBoardFull())
        {
            return EvaluatePositionForSide(sideToMove);
        }

        int depthFromRoot = Mathf.Max(0, aiSearchDepth - depth);
        int candidateLimit = Mathf.Max(6, aiCandidateLimit - (depthFromRoot * 2));
        List<ScoredMove> candidates = BuildCandidateMoves(sideToMove, candidateLimit);

        if (candidates.Count == 0)
        {
            return EvaluatePositionForSide(sideToMove);
        }

        int best = LossScore;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2Int candidate = candidates[i].move;
            board[candidate.x, candidate.y] = sideToMove;

            int score;
            if (HasWonFrom(candidate.x, candidate.y, sideToMove))
            {
                score = WinScore - ply;
            }
            else
            {
                score = -Negamax(depth - 1, -beta, -alpha, -sideToMove, ply + 1);
            }

            board[candidate.x, candidate.y] = Empty;

            if (score > best)
            {
                best = score;
            }

            if (score > alpha)
            {
                alpha = score;
            }

            if (alpha >= beta)
            {
                break;
            }
        }

        return best;
    }

    private List<ScoredMove> BuildCandidateMoves(int side, int limit)
    {
        List<ScoredMove> moves = new List<ScoredMove>();

        if (!HasAnyStoneOnBoard())
        {
            int center = boardSize / 2;
            moves.Add(new ScoredMove(new Vector2Int(center, center), 1));
            return moves;
        }

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] != Empty)
                {
                    continue;
                }

                if (!HasNeighborInRadius(x, y, aiNeighborRadius))
                {
                    continue;
                }

                int score = EvaluateMoveScore(x, y, side);
                moves.Add(new ScoredMove(new Vector2Int(x, y), score));
            }
        }

        if (moves.Count == 0)
        {
            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    if (board[x, y] == Empty)
                    {
                        moves.Add(new ScoredMove(new Vector2Int(x, y), 0));
                    }
                }
            }
        }

        moves.Sort((a, b) => b.score.CompareTo(a.score));

        if (moves.Count > limit)
        {
            moves.RemoveRange(limit, moves.Count - limit);
        }

        return moves;
    }

    private int EvaluatePositionForSide(int sideToMove)
    {
        int aiScore = EvaluateSide(AiStone);
        int playerScore = EvaluateSide(PlayerStone);
        int total = aiScore - Mathf.RoundToInt(playerScore * aiDefenseBias);
        return sideToMove == AiStone ? total : -total;
    }

    private int EvaluateSide(int side)
    {
        int score = 0;

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] != side)
                {
                    continue;
                }

                for (int i = 0; i < LineDirections.Length; i++)
                {
                    int dx = LineDirections[i].x;
                    int dy = LineDirections[i].y;

                    int prevX = x - dx;
                    int prevY = y - dy;
                    if (IsInside(prevX, prevY) && board[prevX, prevY] == side)
                    {
                        continue;
                    }

                    int length = 1;
                    int cx = x + dx;
                    int cy = y + dy;
                    while (IsInside(cx, cy) && board[cx, cy] == side)
                    {
                        length++;
                        cx += dx;
                        cy += dy;
                    }

                    int openEnds = 0;
                    if (IsInside(prevX, prevY) && board[prevX, prevY] == Empty)
                    {
                        openEnds++;
                    }

                    if (IsInside(cx, cy) && board[cx, cy] == Empty)
                    {
                        openEnds++;
                    }

                    score += ScorePattern(length, openEnds);
                }
            }
        }

        return score;
    }

    private int EvaluateMoveScore(int x, int y, int side)
    {
        int center = boardSize / 2;
        int centerDistance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
        int score = (boardSize - centerDistance) * 4;

        board[x, y] = side;
        score += EvaluateStoneAt(x, y, side) * 2;
        if (HasWonFrom(x, y, side))
        {
            score += WinScore / 2;
        }
        board[x, y] = Empty;

        int opponent = -side;
        board[x, y] = opponent;
        score += Mathf.RoundToInt(EvaluateStoneAt(x, y, opponent) * aiDefenseBias);
        if (HasWonFrom(x, y, opponent))
        {
            score += WinScore / 3;
        }
        board[x, y] = Empty;

        if (HasNeighborInRadius(x, y, 1))
        {
            score += 25;
        }

        return score;
    }

    private int EvaluateStoneAt(int x, int y, int side)
    {
        int total = 0;

        for (int i = 0; i < LineDirections.Length; i++)
        {
            int dx = LineDirections[i].x;
            int dy = LineDirections[i].y;
            int forward = CountDirection(x, y, dx, dy, side);
            int backward = CountDirection(x, y, -dx, -dy, side);
            int length = 1 + forward + backward;

            int openEnds = 0;
            int forwardX = x + (dx * (forward + 1));
            int forwardY = y + (dy * (forward + 1));
            if (IsInside(forwardX, forwardY) && board[forwardX, forwardY] == Empty)
            {
                openEnds++;
            }

            int backwardX = x - (dx * (backward + 1));
            int backwardY = y - (dy * (backward + 1));
            if (IsInside(backwardX, backwardY) && board[backwardX, backwardY] == Empty)
            {
                openEnds++;
            }

            total += ScorePattern(length, openEnds);
        }

        return total;
    }

    private int ScorePattern(int length, int openEnds)
    {
        if (length >= winLength)
        {
            return WinScore;
        }

        if (openEnds <= 0)
        {
            return 0;
        }

        int remaining = winLength - length;
        if (remaining == 1)
        {
            return openEnds == 2 ? 220000 : 42000;
        }

        if (remaining == 2)
        {
            return openEnds == 2 ? 12000 : 2600;
        }

        if (remaining == 3)
        {
            return openEnds == 2 ? 1800 : 320;
        }

        if (remaining == 4)
        {
            return openEnds == 2 ? 280 : 60;
        }

        return openEnds == 2 ? 36 : 8;
    }

    private bool TryFindImmediateMove(int side, out Vector2Int move)
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] != Empty)
                {
                    continue;
                }

                board[x, y] = side;
                bool wins = HasWonFrom(x, y, side);
                board[x, y] = Empty;

                if (wins)
                {
                    move = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        move = Vector2Int.zero;
        return false;
    }

    private bool TryFindNeighborMove(out Vector2Int move)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] != Empty)
                {
                    continue;
                }

                if (HasNeighborInRadius(x, y, 1))
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0)
        {
            move = Vector2Int.zero;
            return false;
        }

        move = candidates[random.Next(0, candidates.Count)];
        return true;
    }

    private bool TryFindAnyEmpty(out Vector2Int move)
    {
        List<Vector2Int> allEmpty = new List<Vector2Int>();

        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] == Empty)
                {
                    allEmpty.Add(new Vector2Int(x, y));
                }
            }
        }

        if (allEmpty.Count == 0)
        {
            move = Vector2Int.zero;
            return false;
        }

        move = allEmpty[random.Next(0, allEmpty.Count)];
        return true;
    }

    private bool HasAnyStoneOnBoard()
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] != Empty)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasNeighborInRadius(int x, int y, int radius)
    {
        for (int oy = -radius; oy <= radius; oy++)
        {
            for (int ox = -radius; ox <= radius; ox++)
            {
                if (ox == 0 && oy == 0)
                {
                    continue;
                }

                int nx = x + ox;
                int ny = y + oy;

                if (!IsInside(nx, ny))
                {
                    continue;
                }

                if (board[nx, ny] != Empty)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < boardSize && y < boardSize;
    }

    private bool IsBoardFull()
    {
        for (int y = 0; y < boardSize; y++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                if (board[x, y] == Empty)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasWonFrom(int x, int y, int side)
    {
        return CountLine(x, y, 1, 0, side) >= winLength ||
               CountLine(x, y, 0, 1, side) >= winLength ||
               CountLine(x, y, 1, 1, side) >= winLength ||
               CountLine(x, y, 1, -1, side) >= winLength;
    }

    private int CountLine(int x, int y, int dx, int dy, int side)
    {
        int count = 1;
        count += CountDirection(x, y, dx, dy, side);
        count += CountDirection(x, y, -dx, -dy, side);
        return count;
    }

    private int CountDirection(int x, int y, int dx, int dy, int side)
    {
        int total = 0;
        int cx = x + dx;
        int cy = y + dy;

        while (cx >= 0 && cy >= 0 && cx < boardSize && cy < boardSize && board[cx, cy] == side)
        {
            total++;
            cx += dx;
            cy += dy;
        }

        return total;
    }

    private void ReturnToMap()
    {
        string sceneName = string.IsNullOrWhiteSpace(MiniGameFlowState.ReturnSceneName)
            ? fallbackReturnScene
            : MiniGameFlowState.ReturnSceneName;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("GomokuSceneController has no return scene configured.", this);
            return;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
