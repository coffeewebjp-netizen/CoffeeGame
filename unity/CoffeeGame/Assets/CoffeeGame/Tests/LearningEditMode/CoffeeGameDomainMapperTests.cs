using System;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeGameDomainMapperTests
    {
        [Test]
        public void CompletedProviderResult_MapsOnlyAuthoritativeFields()
        {
            var response = CreateCompletedResponse();

            var outcome = CoffeeGameDomainMapper.ToAuthoritativeOutcome(response);

            Assert.That(outcome.ResultId, Is.EqualTo("rs_test_001"));
            Assert.That(outcome.IsCorrect, Is.True);
            Assert.That(outcome.LearningMutationApplied, Is.True);
            Assert.That(outcome.RewardEligible, Is.True);
            Assert.That(outcome.GrantId, Is.EqualTo("gr_test_001"));
            Assert.That(
                outcome.DifficultyBand,
                Is.EqualTo(CoffeeGame.Domain.LearningDifficultyBand.Intermediate));
            Assert.That(outcome.DifficultyLevel, Is.EqualTo(3));
        }

        [Test]
        public void Mapper_RejectsUnsupportedContractVersion()
        {
            var response = CreateCompletedResponse();
            response.contractVersion = "2.0";

            Assert.Throws<UnsupportedContractVersionException>(
                () => CoffeeGameDomainMapper.ToAuthoritativeOutcome(response));
        }

        [TestCase(true, "mistake")]
        [TestCase(true, null)]
        [TestCase(false, "ok")]
        public void Mapper_RejectsLearningStateInconsistentWithJudgment(
            bool isCorrect,
            string learningState)
        {
            var response = CreateCompletedResponse();
            response.result.judgment.isCorrect = isCorrect;
            response.result.learning.state = learningState;
            if (!isCorrect)
            {
                response.result.rewardEligibility.eligible = false;
                response.result.rewardEligibility.grantId = null;
            }

            Assert.Throws<ArgumentException>(
                () => CoffeeGameDomainMapper.ToAuthoritativeOutcome(response));
        }

        private static AnswerResultResponseDto CreateCompletedResponse()
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                result = new CoffeeGameResultDto
                {
                    resultId = "rs_test_001",
                    challengeId = "ch_test_001",
                    clientAttemptId = "ca_test_001",
                    status = CoffeeGameContractV1.CompletedStatus,
                    judgment = new ResultJudgmentDto { isCorrect = true },
                    learning = new LearningMutationDto
                    {
                        state = CoffeeGameContractV1.OkLearningState,
                        mutationApplied = true
                    },
                    rewardEligibility = new RewardEligibilityDto
                    {
                        eligible = true,
                        grantId = "gr_test_001",
                        difficulty = new CoffeeGameDifficultyDto
                        {
                            band = CoffeeGameContractV1.IntermediateBand,
                            level = 3
                        }
                    }
                }
            };
        }
    }
}
