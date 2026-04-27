using UnityEngine;

public static class MiniGameFlowState
{
    public static string ReturnSceneName { get; private set; } = string.Empty;
    public static string RequestedNodeId { get; private set; } = string.Empty;

    private static bool hasPendingResult;
    private static bool lastResultWon;
    private static string lastResultNodeId = string.Empty;

    public static void PrepareGomokuRequest(string returnSceneName, string nodeId)
    {
        ReturnSceneName = string.IsNullOrWhiteSpace(returnSceneName) ? string.Empty : returnSceneName;
        RequestedNodeId = string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId;
        hasPendingResult = false;
        lastResultWon = false;
        lastResultNodeId = RequestedNodeId;
    }

    public static void ReportGomokuResult(bool won)
    {
        hasPendingResult = true;
        lastResultWon = won;
        lastResultNodeId = RequestedNodeId;
    }

    public static bool TryConsumeGomokuResult(out bool won, out string nodeId)
    {
        if (!hasPendingResult)
        {
            won = false;
            nodeId = string.Empty;
            return false;
        }

        won = lastResultWon;
        nodeId = lastResultNodeId;
        hasPendingResult = false;
        return true;
    }
}
