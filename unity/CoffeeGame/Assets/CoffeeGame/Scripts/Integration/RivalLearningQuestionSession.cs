using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoffeeGame.Domain;

namespace CoffeeGame.Integration
{
    public enum RivalLearningQuestionState
    {
        Idle,
        Loading,
        Editing,
        Confirming,
        Submitting,
        Pending,
        CheckingResult,
        Completed,
        NoItems,
        Error
    }

    /// <summary>
    /// Provider-neutral state for one rival question. It never submits directly from text entry:
    /// the player must move Editing -> Confirming -> Submitting explicitly.
    /// </summary>
    public sealed class RivalLearningQuestionSession : IDisposable
    {
        private readonly Func<ILearningBridge> bridgeProvider;
        private readonly Func<string> idFactory;
        private readonly Func<int> weakItemIndexSelector;
        private readonly Random weakItemRandom = new Random();

        private CancellationTokenSource operationCancellation;
        private int operationSerial;
        private string challengeRequestId;
        private string clientAttemptId;
        private string confirmedAnswer = string.Empty;
        private CoffeeGameChallengeDto challenge;
        private string selectedWeakItemId = string.Empty;
        private string lastSelectedWeakItemId = string.Empty;
        private bool disposed;

        public RivalLearningQuestionSession(
            Func<ILearningBridge> bridgeProvider,
            Func<string> idFactory = null,
            Func<int> weakItemIndexSelector = null)
        {
            this.bridgeProvider = bridgeProvider ?? throw new ArgumentNullException(nameof(bridgeProvider));
            this.idFactory = idFactory ?? (() => Guid.NewGuid().ToString("N"));
            this.weakItemIndexSelector = weakItemIndexSelector ?? (() => weakItemRandom.Next());
        }

        public RivalLearningQuestionState State { get; private set; } = RivalLearningQuestionState.Idle;
        public string PromptText { get; private set; } = string.Empty;
        public string AnswerLocale { get; private set; } = string.Empty;
        public string DraftAnswer { get; private set; } = string.Empty;
        public string ErrorCode { get; private set; } = string.Empty;
        public string ResultId { get; private set; } = string.Empty;
        public bool? IsCorrect { get; private set; }
        public string JudgmentFeedback { get; private set; } = string.Empty;
        public bool RewardEligible { get; private set; }
        public CoffeeGameDifficultyDto Difficulty { get; private set; }
        public AuthoritativeLearningOutcome? AuthoritativeOutcome { get; private set; }
        public PlayerLearningRewardApplication? GameRewardApplication { get; private set; }

        public bool HasDraft => !string.IsNullOrWhiteSpace(DraftAnswer);
        public bool IsBusy => State == RivalLearningQuestionState.Loading
            || State == RivalLearningQuestionState.Submitting
            || State == RivalLearningQuestionState.CheckingResult;

        public async Task BeginNewEncounterAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CancelOperation();
            challengeRequestId = CreateClientId("rq_");
            clientAttemptId = string.Empty;
            confirmedAnswer = string.Empty;
            challenge = null;
            selectedWeakItemId = string.Empty;
            PromptText = string.Empty;
            AnswerLocale = string.Empty;
            DraftAnswer = string.Empty;
            ErrorCode = string.Empty;
            ResultId = string.Empty;
            IsCorrect = null;
            JudgmentFeedback = string.Empty;
            RewardEligible = false;
            Difficulty = null;
            AuthoritativeOutcome = null;
            GameRewardApplication = null;
            await PrepareAsync(cancellationToken);
        }

        public bool RecordGameRewardApplication(PlayerLearningRewardApplication application)
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Completed
                || !AuthoritativeOutcome.HasValue
                || GameRewardApplication.HasValue)
            {
                return false;
            }

            GameRewardApplication = application;
            return true;
        }

        public async Task RetryPreparationAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Error)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(challengeRequestId))
            {
                challengeRequestId = CreateClientId("rq_");
            }

            await PrepareAsync(cancellationToken);
        }

        public void UpdateDraft(string answer)
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Editing)
            {
                return;
            }

            string next = answer ?? string.Empty;
            if (next.Length > 1000)
            {
                next = next.Substring(0, 1000);
            }

            if (!string.Equals(DraftAnswer, next, StringComparison.Ordinal))
            {
                DraftAnswer = next;
                clientAttemptId = string.Empty;
                confirmedAnswer = string.Empty;
                ErrorCode = string.Empty;
            }
        }

        public bool RequestConfirmation()
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Editing || !HasDraft)
            {
                return false;
            }

            confirmedAnswer = DraftAnswer.Trim();
            clientAttemptId = CreateClientId("ra_");
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.Confirming;
            return true;
        }

        public bool ReturnToEditing()
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Confirming)
            {
                return false;
            }

            clientAttemptId = string.Empty;
            confirmedAnswer = string.Empty;
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.Editing;
            return true;
        }

        public async Task SubmitConfirmedAnswerAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Confirming
                || challenge == null
                || string.IsNullOrWhiteSpace(clientAttemptId)
                || string.IsNullOrWhiteSpace(confirmedAnswer))
            {
                return;
            }

            int serial = StartOperation(cancellationToken, out var operationToken);
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.Submitting;
            try
            {
                ILearningBridge bridge = RequireSignedInBridge();
                AnswerResultResponseDto response = await bridge.SubmitAnswerAsync(
                    new AnswerSubmitRequestDto
                    {
                        challengeId = challenge.challengeId,
                        clientAttemptId = clientAttemptId,
                        answer = new CoffeeGameAnswerDto
                        {
                            text = confirmedAnswer,
                            inputMode = CoffeeGameContractV1.TypedInputMode
                        }
                    },
                    operationToken);
                if (!IsCurrent(serial))
                {
                    return;
                }

                ApplyAnswerResponse(response, RivalLearningQuestionState.Confirming);
            }
            catch (OperationCanceledException) when (!IsCurrent(serial))
            {
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(serial))
                {
                    State = RivalLearningQuestionState.Confirming;
                }
            }
            catch (Exception exception)
            {
                if (IsCurrent(serial))
                {
                    ErrorCode = GetSafeErrorCode(exception);
                    State = RivalLearningQuestionState.Confirming;
                }
            }
            finally
            {
                FinishOperation(serial);
            }
        }

        public async Task RecoverPendingResultAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (State != RivalLearningQuestionState.Pending || string.IsNullOrWhiteSpace(ResultId))
            {
                return;
            }

            int serial = StartOperation(cancellationToken, out var operationToken);
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.CheckingResult;
            try
            {
                ILearningBridge bridge = RequireSignedInBridge();
                AnswerResultResponseDto response = await bridge.RecoverResultAsync(ResultId, operationToken);
                if (!IsCurrent(serial))
                {
                    return;
                }

                ApplyAnswerResponse(response, RivalLearningQuestionState.Pending);
            }
            catch (OperationCanceledException) when (!IsCurrent(serial))
            {
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(serial))
                {
                    State = RivalLearningQuestionState.Pending;
                }
            }
            catch (Exception exception)
            {
                if (IsCurrent(serial))
                {
                    ErrorCode = GetSafeErrorCode(exception);
                    State = RivalLearningQuestionState.Pending;
                }
            }
            finally
            {
                FinishOperation(serial);
            }
        }

        public void CancelPendingOperation()
        {
            if (disposed)
            {
                return;
            }

            CancelOperation();
            State = RivalLearningQuestionState.Idle;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            CancelOperation();
        }

        private async Task PrepareAsync(CancellationToken cancellationToken)
        {
            int serial = StartOperation(cancellationToken, out var operationToken);
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.Loading;
            try
            {
                ILearningBridge bridge = RequireSignedInBridge();
                WeakSyncResponseDto sync = await bridge.SyncWeakItemsAsync(
                    new WeakSyncRequestDto
                    {
                        limit = 20,
                        lookbackDays = CoffeeGameContractV1.DefaultWeakLookbackDays
                    },
                    operationToken);
                if (!IsCurrent(serial))
                {
                    return;
                }

                if (sync == null || sync.error != null)
                {
                    SetPreparationError(sync?.error?.code ?? "INVALID_RESPONSE");
                    return;
                }

                WeakItemDto selected = SelectUsableItem(
                    sync.items,
                    weakItemIndexSelector(),
                    string.IsNullOrWhiteSpace(selectedWeakItemId)
                        ? lastSelectedWeakItemId
                        : string.Empty);
                if (selected == null)
                {
                    State = RivalLearningQuestionState.NoItems;
                    return;
                }

                if (!string.IsNullOrWhiteSpace(selectedWeakItemId)
                    && !string.Equals(selectedWeakItemId, selected.weakItemId, StringComparison.Ordinal))
                {
                    challengeRequestId = CreateClientId("rq_");
                }
                selectedWeakItemId = selected.weakItemId;
                lastSelectedWeakItemId = selected.weakItemId;

                ChallengeIssueResponseDto issued = await bridge.IssueChallengeAsync(
                    new ChallengeIssueRequestDto
                    {
                        weakItemId = selected.weakItemId,
                        clientRequestId = challengeRequestId
                    },
                    operationToken);
                if (!IsCurrent(serial))
                {
                    return;
                }

                if (issued == null || issued.error != null || !IsUsableChallenge(issued.challenge))
                {
                    SetPreparationError(issued?.error?.code ?? "INVALID_CHALLENGE");
                    return;
                }

                challenge = issued.challenge;
                PromptText = challenge.prompt.text.Trim();
                AnswerLocale = challenge.prompt.answerLocale ?? string.Empty;
                Difficulty = challenge.difficulty;
                State = RivalLearningQuestionState.Editing;
            }
            catch (OperationCanceledException) when (!IsCurrent(serial))
            {
            }
            catch (OperationCanceledException)
            {
                if (IsCurrent(serial))
                {
                    SetPreparationError("REQUEST_CANCELLED");
                }
            }
            catch (Exception exception)
            {
                if (IsCurrent(serial))
                {
                    SetPreparationError(GetSafeErrorCode(exception));
                }
            }
            finally
            {
                FinishOperation(serial);
            }
        }

        private ILearningBridge RequireSignedInBridge()
        {
            ILearningBridge bridge = bridgeProvider();
            if (bridge == null || !bridge.IsSignedIn)
            {
                throw new InvalidOperationException("CoffeeLearning is not connected.");
            }

            return bridge;
        }

        private void ApplyAnswerResponse(
            AnswerResultResponseDto response,
            RivalLearningQuestionState retryState)
        {
            if (response == null || response.error != null || response.result == null)
            {
                ErrorCode = NormalizeProviderError(response?.error?.code ?? "INVALID_RESPONSE");
                State = retryState;
                return;
            }

            CoffeeGameResultDto result = response.result;
            if (string.Equals(result.status, CoffeeGameContractV1.PendingStatus, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(result.resultId))
            {
                ResultId = result.resultId;
                ErrorCode = string.Empty;
                State = RivalLearningQuestionState.Pending;
                return;
            }

            if (!string.Equals(result.status, CoffeeGameContractV1.CompletedStatus, StringComparison.Ordinal))
            {
                ErrorCode = "INVALID_RESULT";
                State = retryState;
                return;
            }

            AuthoritativeLearningOutcome mappedOutcome;
            try
            {
                mappedOutcome = CoffeeGameDomainMapper.ToAuthoritativeOutcome(response);
            }
            catch (Exception)
            {
                ErrorCode = "INVALID_RESULT";
                State = retryState;
                return;
            }

            ResultId = result.resultId ?? ResultId;
            IsCorrect = mappedOutcome.IsCorrect;
            JudgmentFeedback = NormalizeJudgmentFeedback(result.judgment.feedback);
            RewardEligible = mappedOutcome.RewardEligible;
            if (result.rewardEligibility?.difficulty != null)
            {
                Difficulty = result.rewardEligibility.difficulty;
            }
            AuthoritativeOutcome = mappedOutcome;
            GameRewardApplication = null;
            ErrorCode = string.Empty;
            State = RivalLearningQuestionState.Completed;
        }

        private static string NormalizeJudgmentFeedback(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(Math.Min(value.Length, 600));
            foreach (char character in value)
            {
                if (builder.Length >= 600)
                {
                    break;
                }

                if (char.IsControl(character)
                    && character != '\n'
                    && character != '\r'
                    && character != '\t')
                {
                    continue;
                }

                builder.Append(character == '<' ? '＜' : character == '>' ? '＞' : character);
            }

            return builder.ToString().Trim();
        }

        public static WeakItemDto SelectUsableItem(
            WeakItemDto[] items,
            int requestedIndex,
            string excludedWeakItemId = null)
        {
            if (items == null)
            {
                return null;
            }

            var usable = new List<WeakItemDto>();
            foreach (WeakItemDto item in items)
            {
                if (item != null
                    && !string.IsNullOrWhiteSpace(item.weakItemId)
                    && item.prompt != null
                    && !string.IsNullOrWhiteSpace(item.prompt.text))
                {
                    usable.Add(item);
                }
            }

            if (usable.Count > 1 && !string.IsNullOrWhiteSpace(excludedWeakItemId))
            {
                usable.RemoveAll(item => string.Equals(
                    item.weakItemId,
                    excludedWeakItemId,
                    StringComparison.Ordinal));
            }
            if (usable.Count == 0)
            {
                return null;
            }

            int normalizedIndex = requestedIndex % usable.Count;
            if (normalizedIndex < 0)
            {
                normalizedIndex += usable.Count;
            }
            return usable[normalizedIndex];
        }

        private static bool IsUsableChallenge(CoffeeGameChallengeDto candidate)
        {
            if (candidate == null
                || string.IsNullOrWhiteSpace(candidate.challengeId)
                || candidate.prompt == null
                || string.IsNullOrWhiteSpace(candidate.prompt.text)
                || candidate.acceptedInputModes == null)
            {
                return false;
            }

            foreach (string mode in candidate.acceptedInputModes)
            {
                if (string.Equals(mode, CoffeeGameContractV1.TypedInputMode, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int StartOperation(CancellationToken externalToken, out CancellationToken operationToken)
        {
            CancelOperation();
            operationSerial++;
            operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            operationToken = operationCancellation.Token;
            return operationSerial;
        }

        private void CancelOperation()
        {
            operationSerial++;
            if (operationCancellation == null)
            {
                return;
            }

            operationCancellation.Cancel();
            operationCancellation.Dispose();
            operationCancellation = null;
        }

        private bool IsCurrent(int serial)
        {
            return !disposed && serial == operationSerial;
        }

        private void FinishOperation(int serial)
        {
            if (serial != operationSerial || operationCancellation == null)
            {
                return;
            }

            operationCancellation.Dispose();
            operationCancellation = null;
        }

        private void SetPreparationError(string code)
        {
            ErrorCode = NormalizeProviderError(code);
            State = RivalLearningQuestionState.Error;
        }

        private string CreateClientId(string prefix)
        {
            string value = idFactory();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Client ID factory returned an empty value.");
            }

            return prefix + value.Trim();
        }

        private static string GetSafeErrorCode(Exception exception)
        {
            if (exception is CoffeeGameHttpTransportException transportException)
            {
                return NormalizeProviderError(transportException.Code);
            }

            if (exception is UnsupportedContractVersionException)
            {
                return "CONTRACT_VERSION_UNSUPPORTED";
            }

            if (exception is CoffeeGameCredentialException)
            {
                return "CREDENTIAL_UNAVAILABLE";
            }

            if (exception is InvalidOperationException)
            {
                return "NOT_CONNECTED";
            }

            return "REQUEST_FAILED";
        }

        private static string NormalizeProviderError(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
            {
                return "PROVIDER_ERROR";
            }

            foreach (char character in code)
            {
                if ((character < 'A' || character > 'Z')
                    && (character < '0' || character > '9')
                    && character != '_')
                {
                    return "PROVIDER_ERROR";
                }
            }

            return code;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(RivalLearningQuestionSession));
            }
        }
    }
}
