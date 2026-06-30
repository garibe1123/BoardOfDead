using UnityEngine;

namespace BoardOfDead
{
    public static class D100CheckManager
    {
        public static BuildingEventCheckResult CalculatePreview(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty)
        {
            BuildingEventCheckResult result = BuildBaseResult(
                baseAbility,
                situationModifier,
                difficulty);

            int successCount = 0;

            for (int roll = 1; roll <= 100; roll++)
            {
                BuildingEventSuccessLevel level = EvaluateLevel(
                    roll,
                    result.FinalAbility,
                    result.NormalThreshold,
                    result.HardThreshold,
                    result.ExtremeThreshold);

                if (MeetsDifficulty(level, difficulty))
                {
                    successCount++;
                }
            }

            result.SuccessProbability = successCount;
            return result;
        }

        public static BuildingEventCheckResult Roll(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty,
            System.Random random)
        {
            BuildingEventCheckResult result = CalculatePreview(
                baseAbility,
                situationModifier,
                difficulty);

            result.Roll = random != null
                ? random.Next(1, 101)
                : Random.Range(1, 101);

            result.SuccessLevel = EvaluateLevel(
                result.Roll,
                result.FinalAbility,
                result.NormalThreshold,
                result.HardThreshold,
                result.ExtremeThreshold);

            result.IsCriticalFailure =
                result.SuccessLevel == BuildingEventSuccessLevel.CriticalFailure;

            result.MeetsDifficulty = MeetsDifficulty(
                result.SuccessLevel,
                difficulty);

            return result;
        }

        public static bool MeetsDifficulty(
            BuildingEventSuccessLevel level,
            BuildingEventDifficulty difficulty)
        {
            if (level == BuildingEventSuccessLevel.CriticalFailure ||
                level == BuildingEventSuccessLevel.Failure ||
                level == BuildingEventSuccessLevel.None)
            {
                return false;
            }

            switch (difficulty)
            {
                case BuildingEventDifficulty.Easy:
                case BuildingEventDifficulty.Normal:
                    return level >= BuildingEventSuccessLevel.NormalSuccess;

                case BuildingEventDifficulty.Hard:
                    return level >= BuildingEventSuccessLevel.HardSuccess;

                case BuildingEventDifficulty.Extreme:
                    return level >= BuildingEventSuccessLevel.ExtremeSuccess;

                default:
                    return false;
            }
        }

        private static BuildingEventCheckResult BuildBaseResult(
            int baseAbility,
            int situationModifier,
            BuildingEventDifficulty difficulty)
        {
            int clampedModifier = Mathf.Clamp(situationModifier, -20, 20);
            int difficultyBonus =
                difficulty == BuildingEventDifficulty.Easy ? 20 : 0;
            int finalAbility = Mathf.Clamp(
                baseAbility + clampedModifier + difficultyBonus,
                10,
                95);

            BuildingEventCheckResult result = new BuildingEventCheckResult();
            result.BaseAbility = baseAbility;
            result.SituationModifier = clampedModifier + difficultyBonus;
            result.FinalAbility = finalAbility;
            result.NormalThreshold = finalAbility;
            result.HardThreshold = Mathf.FloorToInt(finalAbility / 2f);
            result.ExtremeThreshold = Mathf.FloorToInt(finalAbility / 5f);
            result.Difficulty = difficulty;
            return result;
        }

        private static BuildingEventSuccessLevel EvaluateLevel(
            int roll,
            int finalAbility,
            int normalThreshold,
            int hardThreshold,
            int extremeThreshold)
        {
            if (roll == 1)
            {
                return BuildingEventSuccessLevel.ExtremeSuccess;
            }

            bool criticalFailure = finalAbility < 50
                ? roll >= 96
                : roll == 100;

            if (criticalFailure)
            {
                return BuildingEventSuccessLevel.CriticalFailure;
            }

            if (roll <= extremeThreshold)
            {
                return BuildingEventSuccessLevel.ExtremeSuccess;
            }

            if (roll <= hardThreshold)
            {
                return BuildingEventSuccessLevel.HardSuccess;
            }

            if (roll <= normalThreshold)
            {
                return BuildingEventSuccessLevel.NormalSuccess;
            }

            return BuildingEventSuccessLevel.Failure;
        }
    }
}
