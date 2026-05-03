using UnityEngine;

public static class RoguelikeMapLaunchRequest
{
    private const string PrintRequestKey = "RoguelikeMap.PrintInkOnNextGameplayScene";

    public static void RequestInkPrintOnNextGameplayScene()
    {
        PlayerPrefs.SetInt(PrintRequestKey, 1);
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
}
