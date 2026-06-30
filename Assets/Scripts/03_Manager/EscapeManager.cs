using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    public class EscapeManager : MonoBehaviour
    {
        private readonly Dictionary<string, EscapeRouteSOBJ> definitions = new Dictionary<string, EscapeRouteSOBJ>();
        private GameSessionData sessionData;
        private GameLogManager logManager;

        public void Initialize(GameSessionData session, IEnumerable<CardSOBJ> cardPool, GameLogManager log)
        {
            sessionData = session;
            logManager = log;
            definitions.Clear();

            foreach (CardSOBJ card in cardPool)
            {
                if (card == null || card.CardType != CardType.EscapeRoute || card.EscapeRouteSOBJ == null)
                {
                    continue;
                }

                definitions[card.EscapeRouteSOBJ.EscapeRouteId] = card.EscapeRouteSOBJ;
            }
        }

        public EscapeRouteData CreateEscapeRouteFromCard(EscapeRouteSOBJ definition, string nodeId, string sourceCardInstanceId)
        {
            if (definition == null || sessionData == null)
            {
                return null;
            }

            string instanceId = $"ESC-{Guid.NewGuid():N}";
            EscapeRouteData route = new EscapeRouteData(
                instanceId,
                definition.EscapeRouteId,
                sourceCardInstanceId,
                nodeId,
                definition.DefenseRounds);

            sessionData.EscapeRoutes.Add(route);
            sessionData.FindNode(nodeId)?.AddEscapeRoute(instanceId);
            logManager?.AddLog(LogCategory.Escape, $"탈출 루트 '{definition.DisplayName}' 발견: {nodeId}");
            return route;
        }

        public EscapeRouteSOBJ GetDefinition(string escapeRouteSOBJId)
        {
            definitions.TryGetValue(escapeRouteSOBJId, out EscapeRouteSOBJ definition);
            return definition;
        }

        public bool AreMaterialsComplete(EscapeRouteData route)
        {
            EscapeRouteSOBJ definition = route != null ? GetDefinition(route.EscapeRouteSOBJId) : null;
            if (route == null || definition == null)
            {
                return false;
            }

            foreach (ItemRequirementSOBJEntry requirement in definition.RequiredMaterials)
            {
                if (requirement?.Item == null)
                {
                    continue;
                }

                if (route.GetInstalledAmount(requirement.Item.ItemId) < requirement.RequiredAmount)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryInstallMaterial(string playerId, string escapeRouteInstanceId, string itemId, int amount, float apCost)
        {
            PlayerData player = sessionData?.FindPlayer(playerId);
            EscapeRouteData route = sessionData?.FindEscapeRoute(escapeRouteInstanceId);
            EscapeRouteSOBJ definition = route != null ? GetDefinition(route.EscapeRouteSOBJId) : null;
            if (player == null || route == null || definition == null || amount <= 0)
            {
                return false;
            }

            if (player.CurrentNodeId != route.NodeId)
            {
                return false;
            }

            ItemRequirementSOBJEntry requirement = null;
            foreach (ItemRequirementSOBJEntry entry in definition.RequiredMaterials)
            {
                if (entry?.Item != null && entry.Item.ItemId == itemId)
                {
                    requirement = entry;
                    break;
                }
            }

            if (requirement == null)
            {
                return false;
            }

            int missing = requirement.RequiredAmount - route.GetInstalledAmount(itemId);
            int installAmount = Mathf.Min(amount, Mathf.Max(0, missing));
            if (installAmount <= 0 || player.GetItemAmount(itemId) < installAmount)
            {
                return false;
            }

            if (!player.TrySpendAP(apCost))
            {
                return false;
            }

            if (!player.RemoveItem(itemId, installAmount))
            {
                return false;
            }

            route.InstallMaterial(itemId, installAmount);
            logManager?.AddLog(LogCategory.Escape, $"{player.PlayerName}이(가) {definition.DisplayName}에 {itemId} x{installAmount} 설치.");

            if (AreMaterialsComplete(route))
            {
                route.Activate();
                logManager?.AddLog(LogCategory.Escape, $"{definition.DisplayName} 준비 완료. 방어 단계로 진입합니다.");
            }

            return true;
        }
    }
}
