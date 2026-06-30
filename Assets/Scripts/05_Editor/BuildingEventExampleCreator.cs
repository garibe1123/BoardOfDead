#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoardOfDead.Editor
{
    public static class BuildingEventExampleCreator
    {
        private const string Root = "Assets/BoardOfDead/EventExamples";

        [MenuItem("Tools/Board Of Dead/Create Building Event Examples")]
        public static void CreateExamples()
        {
            EnsureFolder("Assets", "BoardOfDead");
            EnsureFolder("Assets/BoardOfDead", "EventExamples");

            BuildingEventArchetypeSOBJ locked =
                CreateAsset<BuildingEventArchetypeSOBJ>(
                    Root + "/EVT_LockedSpace.asset");
            ConfigureLockedSpace(locked);

            BuildingEventArchetypeSOBJ fallback =
                CreateAsset<BuildingEventArchetypeSOBJ>(
                    Root + "/EVT_SafeSearch.asset");
            ConfigureFallback(fallback);

            BuildingEventVariantSOBJ pharmacy =
                CreateVariant(
                    Root + "/VAR_LockedSpace_Pharmacy.asset",
                    "VAR_LOCKED_SPACE_PHARMACY",
                    locked,
                    "Pharmacy",
                    "잠긴 의약품 보관실",
                    "매장 뒤편의 철제 보관실은 아직 잠겨 있다. 안쪽에는 쓸 만한 의약품이 남아 있을 가능성이 높다.",
                    "MEDICINE");

            BuildingEventVariantSOBJ police =
                CreateVariant(
                    Root + "/VAR_LockedSpace_Police.asset",
                    "VAR_LOCKED_SPACE_POLICE",
                    locked,
                    "PoliceStation",
                    "봉인된 증거물 보관실",
                    "봉인 테이프 너머로 잠긴 증거물 보관실이 보인다. 무기나 탄약이 남아 있을 수 있다.",
                    "AMMO");

            BuildingEventVariantSOBJ garage =
                CreateVariant(
                    Root + "/VAR_LockedSpace_Garage.asset",
                    "VAR_LOCKED_SPACE_GARAGE",
                    locked,
                    "Garage",
                    "잠긴 부품 창고",
                    "정비소 안쪽의 부품 창고가 굳게 잠겨 있다. 차량 수리용 부품과 공구가 남아 있을 수 있다.",
                    "VEHICLE_PART");

            BuildingEventDatabaseSOBJ database =
                CreateAsset<BuildingEventDatabaseSOBJ>(
                    Root + "/BuildingEventDatabase_Example.asset");
            database.archetypes = new List<BuildingEventArchetypeSOBJ>
            {
                locked,
                fallback
            };
            database.variants = new List<BuildingEventVariantSOBJ>
            {
                pharmacy,
                police,
                garage
            };
            database.fallbackEvent = fallback;
            EditorUtility.SetDirty(database);

            PlayerEventProfileSOBJ profile =
                CreateAsset<PlayerEventProfileSOBJ>(
                    Root + "/PlayerEventProfile_Example.asset");
            ConfigureProfile(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = database;
            Debug.Log(
                "[BuildingEventExampleCreator] 예시 에셋 생성 완료: " + Root,
                database);
        }

        private static void ConfigureLockedSpace(
            BuildingEventArchetypeSOBJ item)
        {
            item.eventId = "EVT_LOCKED_SPACE";
            item.archetypeId = "ARCH_LOCKED_SPACE";
            item.familyId = "FAMILY_OBSTACLE";
            item.defaultTitle = "잠긴 공간";
            item.defaultBody = "잠긴 문 너머에 아직 쓸 만한 물자가 남아 있다.";
            item.baseWeight = 10f;
            item.repeatPolicy = BuildingEventRepeatPolicy.OncePerBuilding;
            item.allowPushByDefault = true;
            item.choices = new List<BuildingEventChoiceData>
            {
                CreateToolChoice(),
                CreateStrengthChoice(),
                CreateKeyChoice(),
                CreateGiveUpChoice()
            };
            EditorUtility.SetDirty(item);
        }

        private static BuildingEventChoiceData CreateToolChoice()
        {
            BuildingEventChoiceData choice = new BuildingEventChoiceData();
            choice.choiceId = "OPEN_WITH_TOOL";
            choice.displayText = "공구로 잠금장치를 연다";
            choice.conditionDescription = "필요: TOOLKIT / 실패 위험: 소음";
            choice.useCheck = true;
            choice.abilityType = BuildingEventAbilityType.Intelligence;
            choice.difficulty = BuildingEventDifficulty.Normal;
            choice.requiredItems.Add(new BuildingEventItemRequirement
            {
                itemId = "TOOLKIT",
                amount = 1
            });
            choice.allowPush = true;
            choice.pushAPCost = 1f;
            choice.normalSuccess.resultText = "잠금장치를 조용히 해제했다.";
            choice.failure.resultText = "잠금장치가 움직이지 않는다.";
            choice.pushFailureAdditional.resultText =
                "무리하게 건드리는 순간 공구가 망가지고 큰 소음이 발생했다.";
            choice.pushFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.RemoveItem,
                id = "TOOLKIT",
                amount = 1
            });
            choice.pushFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.ChangeNoise,
                amount = 2
            });
            choice.pushFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.SpawnZombies,
                amount = 1
            });
            choice.criticalFailureAdditional.resultText =
                "잠금장치가 완전히 파손되며 손을 다쳤다.";
            choice.criticalFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.ChangeHealth,
                amount = -1
            });
            return choice;
        }

        private static BuildingEventChoiceData CreateStrengthChoice()
        {
            BuildingEventChoiceData choice = new BuildingEventChoiceData();
            choice.choiceId = "FORCE_OPEN";
            choice.displayText = "힘으로 문을 뜯는다";
            choice.conditionDescription = "실패 위험: 부상, 소음";
            choice.useCheck = true;
            choice.abilityType = BuildingEventAbilityType.Strength;
            choice.difficulty = BuildingEventDifficulty.Hard;
            choice.allowPush = true;
            choice.normalSuccess.resultText = "문을 강제로 뜯어냈다.";
            choice.failure.resultText = "문은 버텼고 큰 소리만 울렸다.";
            choice.failure.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.ChangeNoise,
                amount = 1
            });
            choice.pushFailureAdditional.resultText =
                "다시 힘을 주는 순간 몸이 꺾이고 주변의 감염자까지 끌어들였다.";
            choice.pushFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.ChangeHealth,
                amount = -1
            });
            choice.pushFailureAdditional.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.SpawnZombies,
                amount = 1
            });
            return choice;
        }

        private static BuildingEventChoiceData CreateKeyChoice()
        {
            BuildingEventChoiceData choice = new BuildingEventChoiceData();
            choice.choiceId = "USE_KEY";
            choice.displayText = "보관실 열쇠를 사용한다";
            choice.conditionDescription = "소모: STORAGE_KEY";
            choice.useCheck = false;
            choice.consumedItems.Add(new BuildingEventItemRequirement
            {
                itemId = "STORAGE_KEY",
                amount = 1
            });
            choice.normalSuccess.resultText = "열쇠가 정확히 맞았다.";
            return choice;
        }

        private static BuildingEventChoiceData CreateGiveUpChoice()
        {
            BuildingEventChoiceData choice = new BuildingEventChoiceData();
            choice.choiceId = "LEAVE";
            choice.displayText = "포기하고 물러난다";
            choice.useCheck = false;
            choice.allowPush = false;
            choice.normalSuccess.resultText = "위험을 감수하지 않고 물러났다.";
            return choice;
        }

        private static void ConfigureFallback(
            BuildingEventArchetypeSOBJ item)
        {
            item.eventId = "EVT_SAFE_SEARCH";
            item.archetypeId = "ARCH_SAFE_SEARCH";
            item.familyId = "FAMILY_SUPPLY";
            item.defaultTitle = "남겨진 물자";
            item.defaultBody = "큰 위험 없이 사용할 수 있는 물자를 조금 발견했다.";
            item.baseWeight = 1f;
            item.repeatPolicy = BuildingEventRepeatPolicy.Always;
            item.choices.Clear();
            item.defaultResult.resultText = "물자 1을 획득했다.";
            item.defaultResult.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.ChangeSupplies,
                amount = 1
            });
            EditorUtility.SetDirty(item);
        }

        private static BuildingEventVariantSOBJ CreateVariant(
            string path,
            string variantId,
            BuildingEventArchetypeSOBJ archetype,
            string buildingTypeId,
            string title,
            string body,
            string rewardItemId)
        {
            BuildingEventVariantSOBJ variant =
                CreateAsset<BuildingEventVariantSOBJ>(path);
            variant.variantId = variantId;
            variant.archetype = archetype;
            variant.allowedBuildingTypeIds = new List<string>
            {
                buildingTypeId
            };
            variant.title = title;
            variant.body = body;
            variant.weightMultiplier = 1f;
            variant.choiceOverrides = new List<BuildingEventChoiceOverrideData>
            {
                CreateRewardOverride("OPEN_WITH_TOOL", rewardItemId),
                CreateRewardOverride("FORCE_OPEN", rewardItemId),
                CreateRewardOverride("USE_KEY", rewardItemId)
            };
            EditorUtility.SetDirty(variant);
            return variant;
        }

        private static BuildingEventChoiceOverrideData CreateRewardOverride(
            string choiceId,
            string rewardItemId)
        {
            BuildingEventChoiceOverrideData item =
                new BuildingEventChoiceOverrideData();
            item.choiceId = choiceId;
            item.overrideNormalSuccess = true;
            item.normalSuccess.resultText =
                "보관실을 열고 " + rewardItemId + "을 획득했다.";
            item.normalSuccess.effects.Add(new BuildingEventEffectData
            {
                effectType = BuildingEventEffectType.AddItem,
                id = rewardItemId,
                amount = 1
            });
            return item;
        }

        private static void ConfigureProfile(PlayerEventProfileSOBJ profile)
        {
            profile.profileId = "EXAMPLE";
            profile.maximumHealth = 10;
            profile.abilities = new List<PlayerEventProfileSOBJ.AbilityValue>
            {
                new PlayerEventProfileSOBJ.AbilityValue
                {
                    abilityType = BuildingEventAbilityType.Strength,
                    value = 55
                },
                new PlayerEventProfileSOBJ.AbilityValue
                {
                    abilityType = BuildingEventAbilityType.Intelligence,
                    value = 65
                },
                new PlayerEventProfileSOBJ.AbilityValue
                {
                    abilityType = BuildingEventAbilityType.Search,
                    value = 60
                }
            };
            profile.startingItems = new List<PlayerEventProfileSOBJ.StartingItem>
            {
                new PlayerEventProfileSOBJ.StartingItem
                {
                    itemId = "TOOLKIT",
                    amount = 1
                },
                new PlayerEventProfileSOBJ.StartingItem
                {
                    itemId = "STORAGE_KEY",
                    amount = 1
                }
            };
            EditorUtility.SetDirty(profile);
        }

        private static T CreateAsset<T>(string path)
            where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null)
            {
                return existing;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
