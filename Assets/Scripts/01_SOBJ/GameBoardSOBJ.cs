using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BoardOfDead
{
    [CreateAssetMenu(fileName = "GameBoard", menuName = "Board Of Dead/SOBJ/Game Board")]
    public class GameBoardSOBJ : ScriptableObject
    {
        [Header("Search Card Pool")]
        [FormerlySerializedAs("fieldCardPool")]
        [SerializeField] private List<CardSOBJ> searchCardPool = new List<CardSOBJ>();

        [Header("Radio Card Pool")]
        [SerializeField] private List<RadioCardSOBJ> radioCardPool = new List<RadioCardSOBJ>();

        public IReadOnlyList<CardSOBJ> SearchCardPool => searchCardPool;
        public IReadOnlyList<CardSOBJ> FieldCardPool => searchCardPool;
        public IReadOnlyList<RadioCardSOBJ> RadioCardPool => radioCardPool;
    }
}
