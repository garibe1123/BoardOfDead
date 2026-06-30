using UnityEngine;

namespace BoardOfDead
{
    public class EscapeRoutePrefab : MonoBehaviour
    {
        [SerializeField] private string escapeRouteInstanceId;

        public string EscapeRouteInstanceId => escapeRouteInstanceId;

        public void Bind(EscapeRouteData routeData)
        {
            escapeRouteInstanceId = routeData != null ? routeData.EscapeRouteInstanceId : string.Empty;
            gameObject.name = string.IsNullOrEmpty(escapeRouteInstanceId)
                ? "EscapeRoutePrefab_Unbound"
                : $"EscapeRoutePrefab_{escapeRouteInstanceId}";
        }
    }
}
