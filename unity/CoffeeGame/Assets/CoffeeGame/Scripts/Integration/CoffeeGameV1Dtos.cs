using System;

namespace CoffeeGame.Integration
{
    public static class CoffeeGameContractV1
    {
        public const string Version = "1.0";
        public const string TypedInputMode = "typed";
        public const string SpeechTranscriptInputMode = "speechTranscript";
        public const string PendingStatus = "pending";
        public const string CompletedStatus = "completed";
        public const string OkLearningState = "ok";
        public const string MistakeLearningState = "mistake";
        public const string FoundationBand = "foundation";
        public const string IntermediateBand = "intermediate";
        public const string AdvancedBand = "advanced";

        public static void RequireSupportedVersion(string contractVersion)
        {
            if (!string.Equals(contractVersion, Version, StringComparison.Ordinal))
            {
                throw new UnsupportedContractVersionException(contractVersion);
            }
        }

        public static void RequireSupportedInputMode(string inputMode)
        {
            if (!string.Equals(inputMode, TypedInputMode, StringComparison.Ordinal)
                && !string.Equals(inputMode, SpeechTranscriptInputMode, StringComparison.Ordinal))
            {
                throw new ArgumentException("Input mode must be typed or speechTranscript.", nameof(inputMode));
            }
        }

        public static void RequireSupportedDifficulty(CoffeeGameDifficultyDto difficulty)
        {
            if (difficulty == null)
            {
                throw new ArgumentNullException(nameof(difficulty));
            }

            if (!string.Equals(difficulty.band, FoundationBand, StringComparison.Ordinal)
                && !string.Equals(difficulty.band, IntermediateBand, StringComparison.Ordinal)
                && !string.Equals(difficulty.band, AdvancedBand, StringComparison.Ordinal))
            {
                throw new ArgumentException("Difficulty band is not part of CoffeeGAME contract v1.", nameof(difficulty));
            }

            if (difficulty.level < 1 || difficulty.level > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty level must be between 1 and 5.");
            }
        }
    }

    public sealed class UnsupportedContractVersionException : InvalidOperationException
    {
        public UnsupportedContractVersionException(string actualVersion)
            : base($"CoffeeGAME contract version '{actualVersion ?? "<null>"}' is unsupported; expected 1.0.")
        {
            ActualVersion = actualVersion;
        }

        public string ActualVersion { get; }
    }

    [Serializable]
    public sealed class WeakSyncRequestDto
    {
        public string cursor;
        public int limit = 50;
    }

    [Serializable]
    public sealed class CoffeeGamePromptDto
    {
        public string text;
        public string answerLocale;
    }

    [Serializable]
    public sealed class CoffeeGameDifficultyDto
    {
        public string band;
        public int level;
    }

    [Serializable]
    public sealed class WeakItemDto
    {
        public string weakItemId;
        public CoffeeGamePromptDto prompt;
        public CoffeeGameDifficultyDto difficulty;
    }

    [Serializable]
    public sealed class CoffeeGameErrorFieldDto
    {
        public string field;
        public string issue;
    }

    [Serializable]
    public sealed class CoffeeGameErrorDto
    {
        public string code;
        public string message;
        public bool retryable;
        public CoffeeGameErrorFieldDto[] fields;
    }

    [Serializable]
    public sealed class CoffeeGameErrorEnvelopeDto
    {
        public string contractVersion;
        public CoffeeGameErrorDto error;
    }

    [Serializable]
    public sealed class WeakSyncResponseDto
    {
        public string contractVersion;
        public WeakItemDto[] items;
        public string nextCursor;
        public bool hasMore;
        public int syncAfterSeconds;
        public CoffeeGameErrorDto error;
    }

    [Serializable]
    public sealed class ChallengeIssueRequestDto
    {
        public string weakItemId;
        public string clientRequestId;
    }

    [Serializable]
    public sealed class CoffeeGameChallengeDto
    {
        public string challengeId;
        public string weakItemId;
        public CoffeeGamePromptDto prompt;
        public CoffeeGameDifficultyDto difficulty;
        public string[] acceptedInputModes;
        public string expiresAt;
    }

    [Serializable]
    public sealed class ChallengeIssueResponseDto
    {
        public string contractVersion;
        public CoffeeGameChallengeDto challenge;
        public CoffeeGameErrorDto error;
    }

    [Serializable]
    public sealed class CoffeeGameAnswerDto
    {
        public string text;
        public string inputMode;
    }

    [Serializable]
    public sealed class AnswerSubmitRequestDto
    {
        public string challengeId;
        public string clientAttemptId;
        public CoffeeGameAnswerDto answer;
    }

    [Serializable]
    public sealed class ResultRecoveryDto
    {
        public string method;
        public string path;
    }

    [Serializable]
    public sealed class ResultJudgmentDto
    {
        public bool isCorrect;
    }

    [Serializable]
    public sealed class LearningMutationDto
    {
        public string state;
        public bool mutationApplied;
    }

    [Serializable]
    public sealed class RewardEligibilityDto
    {
        public bool eligible;
        public string grantId;
        public CoffeeGameDifficultyDto difficulty;
    }

    [Serializable]
    public sealed class CoffeeGameResultDto
    {
        public string resultId;
        public string challengeId;
        public string clientAttemptId;
        public string status;
        public int retryAfterSeconds;
        public ResultRecoveryDto recovery;
        public ResultJudgmentDto judgment;
        public LearningMutationDto learning;
        public RewardEligibilityDto rewardEligibility;
        public string completedAt;
    }

    [Serializable]
    public sealed class AnswerResultResponseDto
    {
        public string contractVersion;
        public CoffeeGameResultDto result;
        public CoffeeGameErrorDto error;
    }
}
