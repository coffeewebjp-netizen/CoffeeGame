using System;
using CoffeeGame.Domain;

namespace CoffeeGame.Integration
{
    /// <summary>
    /// The only v1 transport-to-game-domain projection. Game-owned reward code never consumes
    /// transport JSON directly and therefore cannot invent eligibility or difficulty.
    /// </summary>
    public static class CoffeeGameDomainMapper
    {
        public static AnswerSubmitRequestDto ToSubmitRequest(ConfirmedRivalAnswer answer)
        {
            return new AnswerSubmitRequestDto
            {
                challengeId = answer.ChallengeId,
                clientAttemptId = answer.ClientAttemptId,
                answer = new CoffeeGameAnswerDto
                {
                    text = answer.Text,
                    inputMode = answer.InputMode == RivalAnswerInputMode.Typed
                        ? CoffeeGameContractV1.TypedInputMode
                        : CoffeeGameContractV1.SpeechTranscriptInputMode
                }
            };
        }

        public static AuthoritativeLearningOutcome ToAuthoritativeOutcome(
            AnswerResultResponseDto response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            CoffeeGameContractV1.RequireSupportedVersion(response.contractVersion);
            if (response.error != null)
            {
                throw new InvalidOperationException(
                    "Cannot map a provider error envelope to an authoritative learning outcome.");
            }

            if (response.result == null || string.IsNullOrWhiteSpace(response.result.resultId))
            {
                throw new ArgumentException("Provider response has no stable result.", nameof(response));
            }

            if (string.Equals(
                response.result.status,
                CoffeeGameContractV1.PendingStatus,
                StringComparison.Ordinal))
            {
                return new AuthoritativeLearningOutcome(
                    response.result.resultId,
                    AuthoritativeLearningResultStatus.Pending,
                    false,
                    false,
                    false,
                    null,
                    LearningDifficultyBand.Foundation,
                    0);
            }

            if (!string.Equals(
                response.result.status,
                CoffeeGameContractV1.CompletedStatus,
                StringComparison.Ordinal))
            {
                throw new ArgumentException("Provider result status is not part of contract v1.", nameof(response));
            }

            if (response.result.judgment == null
                || response.result.learning == null
                || response.result.rewardEligibility == null)
            {
                throw new ArgumentException("Completed provider result is missing authoritative fields.", nameof(response));
            }

            var expectedLearningState = response.result.judgment.isCorrect
                ? CoffeeGameContractV1.OkLearningState
                : CoffeeGameContractV1.MistakeLearningState;
            if (!string.Equals(
                response.result.learning.state,
                expectedLearningState,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Completed provider learning state is inconsistent with its judgment.",
                    nameof(response));
            }

            var eligibility = response.result.rewardEligibility;
            CoffeeGameContractV1.RequireSupportedDifficulty(eligibility.difficulty);
            if (response.result.judgment.isCorrect
                && (!eligibility.eligible || string.IsNullOrWhiteSpace(eligibility.grantId)))
            {
                throw new ArgumentException("A correct contract v1 result requires an eligible stable grant.", nameof(response));
            }

            if (!response.result.judgment.isCorrect
                && (eligibility.eligible || !string.IsNullOrWhiteSpace(eligibility.grantId)))
            {
                throw new ArgumentException("An incorrect contract v1 result cannot carry a grant.", nameof(response));
            }

            string normalizedGrantId = string.IsNullOrWhiteSpace(eligibility.grantId)
                ? null
                : eligibility.grantId;

            return new AuthoritativeLearningOutcome(
                response.result.resultId,
                AuthoritativeLearningResultStatus.Completed,
                response.result.judgment.isCorrect,
                response.result.learning.mutationApplied,
                eligibility.eligible,
                normalizedGrantId,
                MapDifficultyBand(eligibility.difficulty.band),
                eligibility.difficulty.level);
        }

        private static LearningDifficultyBand MapDifficultyBand(string band)
        {
            switch (band)
            {
                case CoffeeGameContractV1.FoundationBand:
                    return LearningDifficultyBand.Foundation;
                case CoffeeGameContractV1.IntermediateBand:
                    return LearningDifficultyBand.Intermediate;
                case CoffeeGameContractV1.AdvancedBand:
                    return LearningDifficultyBand.Advanced;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band));
            }
        }
    }
}
