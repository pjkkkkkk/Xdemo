using UnityEngine;

public static class RoguelikeMapLaunchRequest
{
    private const string PrintRequestKey = "RoguelikeMap.PrintInkOnNextGameplayScene";
    private const string IntroDialogueRequestKey = "RoguelikeMap.PlayIntroDialogueOnNextGameplayScene";
    private const string MapRunSeedKey = "RoguelikeMap.RunSeed";
    private const string CurrentNodeIdKey = "RoguelikeMap.CurrentNodeId";
    private const string VisitedNodeIdsKey = "RoguelikeMap.VisitedNodeIds";
    private const char NodeHistorySeparator = '|';

    public static void RequestInkPrintOnNextGameplayScene()
    {
        PlayerPrefs.SetInt(PrintRequestKey, 1);
        PlayerPrefs.Save();
    }

    public static void RequestIntroDialogueOnNextGameplayScene()
    {
        PlayerPrefs.SetInt(IntroDialogueRequestKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeInkPrintOnNextGameplayScene()
    {
        if (PlayerPrefs.GetInt(PrintRequestKey, 0) != 1)
        {
            return false;
        }

        PlayerPrefs.DeleteKey(PrintRequestKey);
        PlayerPrefs.Save();
        return true;
    }

    public static bool ConsumeIntroDialogueOnNextGameplayScene()
    {
        if (PlayerPrefs.GetInt(IntroDialogueRequestKey, 0) != 1)
        {
            return false;
        }

        PlayerPrefs.DeleteKey(IntroDialogueRequestKey);
        PlayerPrefs.Save();
        return true;
    }

    public static void ClearMapRunState()
    {
        PlayerPrefs.DeleteKey(MapRunSeedKey);
        ClearMapNodeHistory(false);
        PlayerPrefs.Save();
    }

    public static void ClearMapNodeHistory()
    {
        ClearMapNodeHistory(true);
    }

    private static void ClearMapNodeHistory(bool save)
    {
        PlayerPrefs.DeleteKey(CurrentNodeIdKey);
        PlayerPrefs.DeleteKey(VisitedNodeIdsKey);
        if (save)
        {
            PlayerPrefs.Save();
        }
    }

    public static int EnsureMapRunSeed(bool useRandomSeed, int fallbackSeed)
    {
        if (TryGetMapRunSeed(out int existingSeed))
        {
            return existingSeed;
        }

        int selectedSeed = useRandomSeed ? UnityEngine.Random.Range(1, int.MaxValue) : fallbackSeed;
        PlayerPrefs.SetInt(MapRunSeedKey, selectedSeed);
        PlayerPrefs.Save();
        return selectedSeed;
    }

    public static bool TryGetMapRunSeed(out int seed)
    {
        seed = PlayerPrefs.GetInt(MapRunSeedKey, 0);
        return PlayerPrefs.HasKey(MapRunSeedKey);
    }

    public static string GetCurrentMapNodeId()
    {
        return PlayerPrefs.GetString(CurrentNodeIdKey, string.Empty);
    }

    public static string[] GetVisitedMapNodeIds()
    {
        string history = PlayerPrefs.GetString(VisitedNodeIdsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(history))
        {
            return new string[0];
        }

        return history.Split(NodeHistorySeparator);
    }

    public static void RecordVisitedMapNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        string normalizedNodeId = nodeId.Trim();
        string history = PlayerPrefs.GetString(VisitedNodeIdsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(history))
        {
            history = normalizedNodeId;
        }
        else
        {
            string[] visitedNodeIds = history.Split(NodeHistorySeparator);
            bool alreadyRecorded = false;
            for (int i = 0; i < visitedNodeIds.Length; i++)
            {
                if (string.Equals(visitedNodeIds[i], normalizedNodeId, System.StringComparison.Ordinal))
                {
                    alreadyRecorded = true;
                    break;
                }
            }

            if (!alreadyRecorded)
            {
                history += NodeHistorySeparator + normalizedNodeId;
            }
        }

        PlayerPrefs.SetString(CurrentNodeIdKey, normalizedNodeId);
        PlayerPrefs.SetString(VisitedNodeIdsKey, history);
        PlayerPrefs.Save();
    }
}
