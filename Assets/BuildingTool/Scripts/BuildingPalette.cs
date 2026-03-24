using UnityEngine;
using System.Collections.Generic;

namespace BuildingSystem
{
    [CreateAssetMenu(fileName = "NewBuildingPalette", menuName = "Building Tool/Building Palette")]
    public class BuildingPalette : ScriptableObject
    {
        [Tooltip("List of prefabs available for placement.")]
        public List<PlaceableObject> availableObjects = new List<PlaceableObject>();
    }
}
