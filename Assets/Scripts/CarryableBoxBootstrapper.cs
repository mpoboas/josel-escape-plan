using UnityEngine;

public static class CarryableBoxBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMarkedBoxesAreCarryable()
    {
        MovableBoxObject[] movableBoxes = Object.FindObjectsByType<MovableBoxObject>(FindObjectsSortMode.None);
        for (int i = 0; i < movableBoxes.Length; i++)
        {
            MovableBoxObject movable = movableBoxes[i];
            if (movable == null)
            {
                continue;
            }

            if (movable.GetComponent<CarryableBox>() == null)
            {
                movable.gameObject.AddComponent<CarryableBox>();
            }
        }
    }
}
