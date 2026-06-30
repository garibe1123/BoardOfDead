using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "Vehicle_", menuName = "Board Of Dead/SOBJ/Vehicle")]
    public class VehicleSOBJ : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string vehicleId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private VehicleType vehicleType = VehicleType.Sedan;

        [Header("Movement")]
        [Tooltip("0.5이면 이동 AP 비용이 절반이 됩니다.")]
        [SerializeField, Range(0.1f, 1f)] private float movementAPMultiplier = 0.5f;
        [SerializeField, Min(1)] private int seatCount = 4;

        [Header("Fuel")]
        [SerializeField] private ItemSOBJ fuelItem;
        [SerializeField, Min(1)] private int maxFuel = 10;
        [SerializeField, Min(1)] private int minimumFuelToUse = 1;
        [SerializeField, Min(0)] private int fuelConsumptionPerMove = 1;

        [Header("Repair")]
        [SerializeField] private List<ItemRequirementSOBJEntry> requiredParts = new List<ItemRequirementSOBJEntry>();

        public string VehicleId => vehicleId;
        public string DisplayName => displayName;
        public string Description => description;
        public VehicleType VehicleType => vehicleType;
        public float MovementAPMultiplier => Mathf.Clamp(movementAPMultiplier, 0.1f, 1f);
        public int SeatCount => Mathf.Max(1, seatCount);
        public ItemSOBJ FuelItem => fuelItem;
        public int MaxFuel => Mathf.Max(1, maxFuel);
        public int MinimumFuelToUse => Mathf.Clamp(minimumFuelToUse, 1, MaxFuel);
        public int FuelConsumptionPerMove => Mathf.Max(0, fuelConsumptionPerMove);
        public IReadOnlyList<ItemRequirementSOBJEntry> RequiredParts => requiredParts;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(vehicleId))
            {
                vehicleId = name;
            }
        }
#endif
    }
}
