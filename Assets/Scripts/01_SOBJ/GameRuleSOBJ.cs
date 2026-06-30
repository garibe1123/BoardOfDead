using UnityEngine;
using UnityEngine.Serialization;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "GameRule", menuName = "Board Of Dead/SOBJ/Game Rule")]
    public class GameRuleSOBJ : ScriptableObject
    {
        [Header("Round")]
        [SerializeField, Min(1)] private int maxRounds = 20;

        [Header("Board Generation")]
        [SerializeField, Min(1)] private int districtCount = 5;
        [SerializeField, Min(2)] private int districtGapMin = 2;
        [SerializeField, Min(2)] private int districtGapMax = 3;
        [SerializeField, Min(0.1f)] private float boardSpaceWorldSize = 2f;

        [Header("Movement AP")]
        [FormerlySerializedAs("baseMoveAPCost")]
        [SerializeField, Min(0.1f)] private float roadMoveAPCost = 1f;
        [SerializeField, Min(0.1f)] private float buildingEnterAPCost = 2f;
        [SerializeField, Min(0.1f)] private float baseActionAPCost = 1f;

        [Header("Compatibility: Horde / Doom")]
        [SerializeField, Min(1)] private int hordeCardThreshold = 4;
        [SerializeField, Min(1)] private int nuclearTriggerHordeCount = 3;
        [SerializeField, Min(1)] private int nuclearCountdownRounds = 5;

        [Header("Search AP")]
        [Tooltip("일반 건물 탐색 비용입니다. 2보다 큰 값만 사용합니다.")]
        [SerializeField, Min(3f)] private float normalSearchAPCost = 3f;
        [SerializeField, Min(0.1f)] private float radioSearchAPCost = 1f;

        [Header("Radio Card")]
        [SerializeField, Min(0)] private int radioCardCountPerRound = 3;
        [SerializeField, Min(1)] private int radioDurationMin = 1;
        [SerializeField, Min(1)] private int radioDurationMax = 3;

        public int MaxRounds => Mathf.Max(1, maxRounds);
        public int DistrictCount => Mathf.Max(1, districtCount);
        public int DistrictGapMin => Mathf.Max(2, districtGapMin);
        public int DistrictGapMax => Mathf.Max(DistrictGapMin, districtGapMax);
        public float BoardSpaceWorldSize => Mathf.Max(0.1f, boardSpaceWorldSize);
        public float RoadMoveAPCost => Mathf.Max(0.1f, roadMoveAPCost);
        public float BuildingEnterAPCost => Mathf.Max(0.1f, buildingEnterAPCost);
        public float BaseMoveAPCost => RoadMoveAPCost;
        public float BaseActionAPCost => Mathf.Max(0.1f, baseActionAPCost);
        public int HordeCardThreshold => Mathf.Max(1, hordeCardThreshold);
        public int NuclearTriggerHordeCount => Mathf.Max(1, nuclearTriggerHordeCount);
        public int NuclearCountdownRounds => Mathf.Max(1, nuclearCountdownRounds);
        public float NormalSearchAPCost => Mathf.Max(3f, normalSearchAPCost);
        public float RadioSearchAPCost => Mathf.Max(0.1f, radioSearchAPCost);
        public int RadioCardCountPerRound => Mathf.Max(0, radioCardCountPerRound);
        public int RadioDurationMin => Mathf.Max(1, radioDurationMin);
        public int RadioDurationMax => Mathf.Max(RadioDurationMin, radioDurationMax);
    }
}
