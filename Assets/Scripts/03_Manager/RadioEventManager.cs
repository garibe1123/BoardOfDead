using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    /// <summary>
    /// 기존 GameSessionData 기반 무전 카드와 신규 BoardSpacePrefab 기반
    /// 단순 무전 배치를 모두 지원합니다.
    /// </summary>
    public class RadioEventManager : MonoBehaviour
    {
        [Header("Simple Board Mode")]
        [SerializeField, Range(1, 10)]
        private int radioCardCountPerRound = 3;

        [SerializeField, Min(1)]
        private int minimumDurationTurns = 1;

        [SerializeField, Min(1)]
        private int maximumDurationTurns = 3;

        [SerializeField]
        private GameObject simpleRadioCardMarkerPrefab;

        private readonly List<RadioCardSOBJ> radioCardPool =
            new List<RadioCardSOBJ>();

        private readonly Dictionary<string, RadioCardSOBJ> definitions =
            new Dictionary<string, RadioCardSOBJ>();

        private readonly Dictionary<string, RadioCardPrefab> runtimeMarkers =
            new Dictionary<string, RadioCardPrefab>();

        private GameSessionData sessionData;
        private GameRuleSOBJ ruleSOBJ;
        private GameBoardPrefab gameBoardPrefab;
        private RadioCardPrefab radioCardPrefab;
        private Transform radioCardRoot;
        private GameLogManager logManager;

        private GridManager gridManager;
        private System.Random random;
        private RadioCardSOBJ runtimeFallbackCard;
        private RadioRuntimeMode runtimeMode;

        /// <summary>
        /// 기존 TimelineManager/GameSessionData용 초기화.
        /// </summary>
        public void Initialize(
            GameSessionData session,
            GameRuleSOBJ rules,
            GameBoardSOBJ boardSOBJ,
            GameBoardPrefab boardPrefab,
            RadioCardPrefab markerPrefab,
            Transform markerRoot,
            GameLogManager log)
        {
            sessionData = session;
            ruleSOBJ = rules;
            gameBoardPrefab = boardPrefab;
            radioCardPrefab = markerPrefab;
            radioCardRoot = markerRoot;
            logManager = log;

            random =
                new System.Random(
                    session != null
                        ? session.RandomSeed + 203
                        : Environment.TickCount);

            runtimeMode = RadioRuntimeMode.SessionData;

            radioCardPool.Clear();
            definitions.Clear();
            ClearLegacyMarkers();

            if (boardSOBJ != null)
            {
                foreach (RadioCardSOBJ card
                         in boardSOBJ.RadioCardPool)
                {
                    RegisterDefinition(card);
                }
            }

            if (radioCardPool.Count == 0)
            {
                runtimeFallbackCard =
                    ScriptableObject.CreateInstance<RadioCardSOBJ>();

                runtimeFallbackCard.name =
                    "Runtime_DefaultRadioCard";

                runtimeFallbackCard.hideFlags =
                    HideFlags.DontSave;

                runtimeFallbackCard.ConfigureRuntime(
                    "DEFAULT_RADIO_CARD",
                    "기본 테스트 무전 신호");

                RegisterDefinition(
                    runtimeFallbackCard);
            }
        }

        /// <summary>
        /// 신규 자동 보드용 초기화.
        /// </summary>
        public void Initialize(
            GridManager runtimeGridManager,
            System.Random runtimeRandom)
        {
            gridManager = runtimeGridManager;
            random =
                runtimeRandom ??
                new System.Random(Environment.TickCount);

            runtimeMode =
                RadioRuntimeMode.SimpleBoardSpace;
        }

        public bool HasActiveRadioAtNode(string nodeId)
        {
            if (runtimeMode ==
                RadioRuntimeMode.SessionData)
            {
                NodeData node =
                    sessionData != null
                        ? sessionData.FindNode(nodeId)
                        : null;

                return
                    node != null &&
                    node.HasActiveRadioCard;
            }

            if (runtimeMode ==
                RadioRuntimeMode.SimpleBoardSpace &&
                gridManager != null)
            {
                BoardSpacePrefab space;

                return
                    gridManager.TryGetSpace(
                        nodeId,
                        out space) &&
                    space != null &&
                    space.HasRadioCard;
            }

            return false;
        }

        /// <summary>
        /// 기존 TimelineManager가 호출합니다.
        /// </summary>
        public void ProcessEndOfRound()
        {
            if (runtimeMode ==
                RadioRuntimeMode.SimpleBoardSpace)
            {
                ResolveRoundEnd();
                return;
            }

            ProcessLegacyEndOfRound();
        }

        /// <summary>
        /// 신규 단순 TurnManager가 호출합니다.
        /// </summary>
        public void ResolveRoundEnd()
        {
            if (runtimeMode ==
                RadioRuntimeMode.SessionData)
            {
                ProcessLegacyEndOfRound();
                return;
            }

            ResolveSimpleBoardEndOfRound();
        }

        private void ProcessLegacyEndOfRound()
        {
            if (sessionData == null ||
                ruleSOBJ == null)
            {
                return;
            }

            TickLegacyCards();
            PlaceLegacyRadioCards(
                ruleSOBJ.RadioCardCountPerRound);
        }

        private void TickLegacyCards()
        {
            List<RadioCardData> expired =
                new List<RadioCardData>();

            for (int index = 0;
                 index < sessionData.RadioCards.Count;
                 index++)
            {
                RadioCardData card =
                    sessionData.RadioCards[index];

                if (card == null)
                {
                    continue;
                }

                card.TickRound();

                if (card.IsExpired)
                {
                    expired.Add(card);
                }
            }

            for (int index = 0;
                 index < expired.Count;
                 index++)
            {
                RadioCardData card =
                    expired[index];

                NodeData node =
                    sessionData.FindNode(card.NodeId);

                if (node != null)
                {
                    node.RemoveRadioCard(
                        card.RadioCardInstanceId);
                }

                sessionData.RadioCards.Remove(card);

                RadioCardPrefab marker;

                if (runtimeMarkers.TryGetValue(
                    card.RadioCardInstanceId,
                    out marker))
                {
                    runtimeMarkers.Remove(
                        card.RadioCardInstanceId);

                    if (marker != null)
                    {
                        Destroy(marker.gameObject);
                    }
                }

                if (logManager != null)
                {
                    logManager.AddLog(
                        LogCategory.Radio,
                        "무전 신호 만료: " +
                        card.NodeId);
                }
            }
        }

        private void PlaceLegacyRadioCards(int count)
        {
            if (count <= 0 ||
                radioCardPool.Count == 0)
            {
                return;
            }

            List<NodeData> available =
                sessionData.Nodes.FindAll(
                    delegate(NodeData node)
                    {
                        return
                            node != null &&
                            node.IsBuilding &&
                            node.Enterable &&
                            !node.HasActiveRadioCard;
                    });

            if (available.Count < count)
            {
                List<NodeData> allBuildings =
                    sessionData.Nodes.FindAll(
                        delegate(NodeData node)
                        {
                            return
                                node != null &&
                                node.IsBuilding &&
                                node.Enterable;
                        });

                for (int index = 0;
                     index < allBuildings.Count;
                     index++)
                {
                    if (!available.Contains(
                        allBuildings[index]))
                    {
                        available.Add(
                            allBuildings[index]);
                    }
                }
            }

            Shuffle(available);

            int placementCount =
                Mathf.Min(
                    count,
                    available.Count);

            for (int index = 0;
                 index < placementCount;
                 index++)
            {
                NodeData targetNode =
                    available[index];

                RadioCardSOBJ definition =
                    radioCardPool[
                        random.Next(
                            0,
                            radioCardPool.Count)];

                int duration =
                    random.Next(
                        ruleSOBJ.RadioDurationMin,
                        ruleSOBJ.RadioDurationMax + 1);

                RadioCardData cardData =
                    new RadioCardData(
                        "RADIO-" +
                        Guid.NewGuid().ToString("N"),
                        definition.RadioCardId,
                        targetNode.NodeId,
                        duration);

                sessionData.RadioCards.Add(cardData);
                targetNode.AddRadioCard(
                    cardData.RadioCardInstanceId);

                CreateLegacyMarker(
                    cardData,
                    targetNode);

                if (logManager != null)
                {
                    logManager.AddLog(
                        LogCategory.Radio,
                        "라디오 카드 배치: " +
                        targetNode.NodeId +
                        ", 잔류 " +
                        duration +
                        "턴.");
                }
            }
        }

        private void CreateLegacyMarker(
            RadioCardData data,
            NodeData targetNode)
        {
            BoardSpacePrefab targetSpace =
                gameBoardPrefab != null
                    ? gameBoardPrefab.FindSpace(
                        targetNode.NodeId)
                    : null;

            Transform parent =
                targetSpace != null
                    ? targetSpace.RadioCardRoot
                    : radioCardRoot;

            RadioCardPrefab marker;

            if (radioCardPrefab != null)
            {
                marker =
                    Instantiate(
                        radioCardPrefab,
                        parent);
            }
            else
            {
                GameObject fallback =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                fallback.transform.SetParent(
                    parent,
                    false);

                fallback.transform.localScale =
                    new Vector3(
                        0.12f,
                        0.03f,
                        0.12f);

                Collider collider =
                    fallback.GetComponent<Collider>();

                if (collider != null)
                {
                    Destroy(collider);
                }

                marker =
                    fallback.AddComponent<RadioCardPrefab>();
            }

            int stackIndex =
                targetNode
                    .ActiveRadioCardInstanceIds
                    .Count -
                1;

            marker.Bind(
                data,
                Mathf.Max(0, stackIndex));

            runtimeMarkers[
                data.RadioCardInstanceId] =
                marker;
        }

        private void ResolveSimpleBoardEndOfRound()
        {
            if (gridManager == null)
            {
                return;
            }

            gridManager.TickRadioCards();

            List<BoardSpacePrefab> targets =
                gridManager.GetRandomDistinctBuildings(
                    random,
                    radioCardCountPerRound);

            int minimum =
                Mathf.Max(
                    1,
                    minimumDurationTurns);

            int maximum =
                Mathf.Max(
                    minimum,
                    maximumDurationTurns);

            for (int index = 0;
                 index < targets.Count;
                 index++)
            {
                int duration =
                    random.Next(
                        minimum,
                        maximum + 1);

                targets[index].PlaceRadioCard(
                    duration,
                    simpleRadioCardMarkerPrefab);

                Debug.Log(
                    "[RadioEventManager] 라디오 카드 배치: " +
                    targets[index].NodeId +
                    " / 지속 " +
                    duration +
                    "턴");
            }
        }

        private void RegisterDefinition(
            RadioCardSOBJ definition)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(
                    definition.RadioCardId))
            {
                return;
            }

            definitions[
                definition.RadioCardId] =
                definition;

            radioCardPool.Add(definition);
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int index = list.Count - 1;
                 index > 0;
                 index--)
            {
                int swapIndex =
                    random.Next(
                        0,
                        index + 1);

                T temporary =
                    list[index];

                list[index] =
                    list[swapIndex];

                list[swapIndex] =
                    temporary;
            }
        }

        private void ClearLegacyMarkers()
        {
            foreach (RadioCardPrefab marker
                     in runtimeMarkers.Values)
            {
                if (marker != null)
                {
                    Destroy(marker.gameObject);
                }
            }

            runtimeMarkers.Clear();
        }

        private enum RadioRuntimeMode
        {
            None,
            SessionData,
            SimpleBoardSpace
        }
    }
}
