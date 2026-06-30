using System.Collections.Generic;
using UnityEngine;

namespace BoardOfDead
{
    [CreateAssetMenu(
        fileName = "BuildingEventDatabase",
        menuName = "Board Of Dead/Event/Building Event Database")]
    public class BuildingEventDatabaseSOBJ : ScriptableObject
    {
        public List<BuildingEventArchetypeSOBJ> archetypes =
            new List<BuildingEventArchetypeSOBJ>();

        public List<BuildingEventVariantSOBJ> variants =
            new List<BuildingEventVariantSOBJ>();

        [Tooltip("후보가 없을 때 사용하는 안전한 기본 사건입니다.")]
        public BuildingEventArchetypeSOBJ fallbackEvent;

        [Tooltip("Variant와 Archetype 삽화가 모두 없을 때 사용합니다.")]
        public Sprite fallbackIllustration;

        public BuildingEventArchetypeSOBJ FindArchetype(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return null;
            }

            for (int index = 0; index < archetypes.Count; index++)
            {
                BuildingEventArchetypeSOBJ item = archetypes[index];

                if (item != null && item.eventId == eventId)
                {
                    return item;
                }
            }

            if (fallbackEvent != null && fallbackEvent.eventId == eventId)
            {
                return fallbackEvent;
            }

            return null;
        }

        public List<BuildingEventVariantSOBJ> GetVariants(
            BuildingEventArchetypeSOBJ archetype)
        {
            List<BuildingEventVariantSOBJ> result =
                new List<BuildingEventVariantSOBJ>();

            if (archetype == null)
            {
                return result;
            }

            for (int index = 0; index < variants.Count; index++)
            {
                BuildingEventVariantSOBJ variant = variants[index];

                if (variant != null && variant.archetype == archetype)
                {
                    result.Add(variant);
                }
            }

            return result;
        }

        public List<string> ValidateDatabase()
        {
            List<string> errors = new List<string>();
            HashSet<string> eventIds = new HashSet<string>();
            HashSet<string> variantIds = new HashSet<string>();

            for (int index = 0; index < archetypes.Count; index++)
            {
                BuildingEventArchetypeSOBJ item = archetypes[index];

                if (item == null)
                {
                    errors.Add("Archetypes[" + index + "]가 비어 있습니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.eventId))
                {
                    errors.Add(item.name + ": Event ID가 비어 있습니다.");
                }
                else if (!eventIds.Add(item.eventId))
                {
                    errors.Add("중복 Event ID: " + item.eventId);
                }

                if (item.baseWeight <= 0f && item != fallbackEvent)
                {
                    errors.Add(item.name + ": 기본 가중치가 0 이하입니다.");
                }

                if (item.choices == null || item.choices.Count == 0)
                {
                    if (item.defaultResult == null || item.defaultResult.IsEmpty)
                    {
                        errors.Add(item.name + ": 선택지와 기본 결과가 모두 없습니다.");
                    }
                }
                else
                {
                    ValidateChoices(item, errors);
                }

                if (item.defaultIllustration == null)
                {
                    errors.Add(item.name + ": 기본 삽화가 없습니다. Fallback 삽화를 사용합니다.");
                }
            }

            for (int index = 0; index < variants.Count; index++)
            {
                BuildingEventVariantSOBJ variant = variants[index];

                if (variant == null)
                {
                    errors.Add("Variants[" + index + "]가 비어 있습니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(variant.variantId))
                {
                    errors.Add(variant.name + ": Variant ID가 비어 있습니다.");
                }
                else if (!variantIds.Add(variant.variantId))
                {
                    errors.Add("중복 Variant ID: " + variant.variantId);
                }

                if (variant.archetype == null)
                {
                    errors.Add(variant.name + ": Archetype 연결이 없습니다.");
                }

                if (variant.weightMultiplier <= 0f)
                {
                    errors.Add(variant.name + ": 가중치 보정이 0 이하입니다.");
                }

                if (variant.minimumThreat > variant.maximumThreat)
                {
                    errors.Add(variant.name + ": 최소 위협도가 최대 위협도보다 큽니다.");
                }
            }

            return errors;
        }

        private static void ValidateChoices(
            BuildingEventArchetypeSOBJ item,
            List<string> errors)
        {
            HashSet<string> choiceIds = new HashSet<string>();

            for (int index = 0; index < item.choices.Count; index++)
            {
                BuildingEventChoiceData choice = item.choices[index];

                if (choice == null)
                {
                    errors.Add(item.name + ": Choices[" + index + "]가 비어 있습니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choice.choiceId))
                {
                    errors.Add(item.name + ": 선택지 ID가 비어 있습니다.");
                }
                else if (!choiceIds.Add(choice.choiceId))
                {
                    errors.Add(item.name + ": 중복 선택지 ID " + choice.choiceId);
                }

                if (string.IsNullOrWhiteSpace(choice.displayText))
                {
                    errors.Add(item.name + ": " + choice.choiceId + " 문구가 비어 있습니다.");
                }

                if (choice.useCheck &&
                    choice.normalSuccess.IsEmpty &&
                    choice.hardSuccess.IsEmpty &&
                    choice.extremeSuccess.IsEmpty &&
                    choice.failure.IsEmpty)
                {
                    errors.Add(item.name + ": " + choice.choiceId + " 판정 결과가 모두 비어 있습니다.");
                }

                if (choice.allowPush && choice.pushFailureAdditional.IsEmpty)
                {
                    errors.Add(item.name + ": " + choice.choiceId +
                               " 밀어붙이기 가능이지만 추가 실패 결과가 비어 있습니다.");
                }
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            List<string> messages = ValidateDatabase();

            for (int index = 0; index < messages.Count; index++)
            {
                Debug.LogWarning(
                    "[BuildingEventDatabase] " + messages[index],
                    this);
            }
#endif
        }
    }
}
