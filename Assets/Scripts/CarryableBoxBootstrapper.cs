using UnityEngine;

public static class CarryableBoxBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneBoxesAreCarryable()
    {
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (!string.Equals(tr.name, "Box", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tr.GetComponent<CarryableBox>() == null)
            {
                tr.gameObject.AddComponent<CarryableBox>();
            }
        }
    }
}
