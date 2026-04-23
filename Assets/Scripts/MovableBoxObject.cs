using UnityEngine;

/// <summary>
/// Marker component for boxes that should be movable/carryable by the player.
/// Add this to any GameObject you want treated as a physics box.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CarryableBox))]
public class MovableBoxObject : MonoBehaviour
{
}
