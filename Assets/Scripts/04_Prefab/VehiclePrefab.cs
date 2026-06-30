using UnityEngine;

namespace BoardOfDead
{
    public class VehiclePrefab : MonoBehaviour
    {
        [SerializeField] private string vehicleInstanceId;

        public string VehicleInstanceId => vehicleInstanceId;

        public void Bind(VehicleData vehicleData)
        {
            vehicleInstanceId = vehicleData != null ? vehicleData.VehicleInstanceId : string.Empty;
            gameObject.name = string.IsNullOrEmpty(vehicleInstanceId)
                ? "VehiclePrefab_Unbound"
                : $"VehiclePrefab_{vehicleInstanceId}";
        }
    }
}
