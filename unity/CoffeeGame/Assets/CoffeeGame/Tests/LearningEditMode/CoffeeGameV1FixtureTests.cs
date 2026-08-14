using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeGameV1FixtureTests
    {
        [Test]
        public void FrozenProviderFixture_DeserializesWithExactV1Fields()
        {
            var fixture = DeserializeFixture(ReadFixture());

            Assert.That(fixture.contractVersion, Is.EqualTo("1.0"));
            Assert.That(fixture.basePath, Is.EqualTo("/api/integrations/coffee-game/v1"));
            Assert.That(fixture.weakSync.response.items[0].weakItemId, Is.EqualTo("wi_fixture_resilient_001"));
            Assert.That(fixture.weakSync.response.items[0].difficulty.band, Is.EqualTo("intermediate"));
            Assert.That(fixture.weakSync.response.items[0].difficulty.level, Is.EqualTo(3));
            Assert.That(fixture.challengeIssue.request.body.clientRequestId, Is.EqualTo("cr_fixture_encounter_001"));
            Assert.That(
                fixture.challengeIssue.response.challenge.acceptedInputModes,
                Is.EqualTo(new[] { "typed", "speechTranscript" }));
            Assert.That(fixture.answerSubmit.request.body.answer.inputMode, Is.EqualTo("speechTranscript"));
            Assert.That(fixture.answerSubmit.response.result.status, Is.EqualTo("pending"));
            Assert.That(fixture.resultRecovery.response.result.status, Is.EqualTo("completed"));
            Assert.That(fixture.resultRecovery.response.result.resultId, Is.EqualTo("rs_fixture_resilient_001"));
            Assert.That(
                fixture.resultRecovery.response.result.rewardEligibility.grantId,
                Is.EqualTo("gr_fixture_resilient_001"));
            Assert.That(fixture.errors.invalidRequest.body.error.fields[0].field, Is.EqualTo("body.answer.text"));

            CoffeeGameContractV1.RequireSupportedVersion(fixture.contractVersion);
            CoffeeGameContractV1.RequireSupportedVersion(fixture.resultRecovery.response.contractVersion);
            CoffeeGameContractV1.RequireSupportedDifficulty(
                fixture.resultRecovery.response.result.rewardEligibility.difficulty);
        }

        [Test]
        public void JsonUtility_ToleratesAdditiveUnknownFields()
        {
            var json = ReadFixture().Replace(
                "\"basePath\": \"/api/integrations/coffee-game/v1\",",
                "\"basePath\": \"/api/integrations/coffee-game/v1\",\n  \"futureAdditiveField\": { \"ignored\": true },");

            var fixture = DeserializeFixture(json);

            Assert.That(fixture.contractVersion, Is.EqualTo(CoffeeGameContractV1.Version));
            Assert.That(fixture.resultRecovery.response.result.judgment.isCorrect, Is.True);
        }

        [Test]
        public void ContractGate_RejectsUnsupportedVersionExplicitly()
        {
            var fixture = DeserializeFixture(ReadFixture().Replace(
                "\"contractVersion\": \"1.0\"",
                "\"contractVersion\": \"2.0\""));

            Assert.Throws<UnsupportedContractVersionException>(
                () => CoffeeGameContractV1.RequireSupportedVersion(fixture.contractVersion));
        }

        private static CoffeeGameV1FixtureDto DeserializeFixture(string json)
        {
            var fixture = JsonUtility.FromJson<CoffeeGameV1FixtureDto>(json);
            Assert.That(fixture, Is.Not.Null);
            return fixture;
        }

        private static string ReadFixture()
        {
            var path = Path.Combine(
                Application.dataPath,
                "CoffeeGame",
                "Tests",
                "LearningEditMode",
                "Fixtures",
                "coffee-game-v1.fixture.json");
            return File.ReadAllText(path);
        }

        [Serializable]
        public sealed class CoffeeGameV1FixtureDto
        {
            public string contractVersion;
            public string basePath;
            public WeakSyncFixtureDto weakSync;
            public ChallengeIssueFixtureDto challengeIssue;
            public AnswerSubmitFixtureDto answerSubmit;
            public ResultRecoveryFixtureDto resultRecovery;
            public ErrorsFixtureDto errors;
        }

        [Serializable]
        public sealed class WeakSyncFixtureDto
        {
            public WeakSyncRequestMetadataDto request;
            public WeakSyncResponseDto response;
        }

        [Serializable]
        public sealed class WeakSyncRequestMetadataDto
        {
            public string method;
            public string path;
            public WeakSyncRequestDto query;
        }

        [Serializable]
        public sealed class ChallengeIssueFixtureDto
        {
            public ChallengeIssueRequestMetadataDto request;
            public ChallengeIssueResponseDto response;
        }

        [Serializable]
        public sealed class ChallengeIssueRequestMetadataDto
        {
            public string method;
            public string path;
            public ChallengeIssueRequestDto body;
        }

        [Serializable]
        public sealed class AnswerSubmitFixtureDto
        {
            public AnswerSubmitRequestMetadataDto request;
            public AnswerResultResponseDto response;
        }

        [Serializable]
        public sealed class AnswerSubmitRequestMetadataDto
        {
            public string method;
            public string path;
            public AnswerSubmitRequestDto body;
        }

        [Serializable]
        public sealed class ResultRecoveryFixtureDto
        {
            public ResultRecoveryRequestMetadataDto request;
            public AnswerResultResponseDto response;
        }

        [Serializable]
        public sealed class ResultRecoveryRequestMetadataDto
        {
            public string method;
            public string path;
        }

        [Serializable]
        public sealed class ErrorsFixtureDto
        {
            public ErrorCaseDto disabled;
            public ErrorCaseDto invalidRequest;
        }

        [Serializable]
        public sealed class ErrorCaseDto
        {
            public int status;
            public CoffeeGameErrorEnvelopeDto body;
        }
    }
}
