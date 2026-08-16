using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CoffeeGame.Integration
{
    public sealed class CoffeeLearningHttpBridgeOptions
    {
        public const string ProductionProviderBaseUrl = "https://www.coffeewebjp.com";
        public const string ProductionIntegrationBaseUrl =
            ProductionProviderBaseUrl + "/api/integrations/coffee-game/v1";
        public const int DefaultTimeoutSeconds = 30;
        public const int MinimumTimeoutSeconds = 1;
        public const int MaximumTimeoutSeconds = 120;

        public CoffeeLearningHttpBridgeOptions(
            string integrationBaseUrl = ProductionIntegrationBaseUrl,
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (!Uri.TryCreate(integrationBaseUrl, UriKind.Absolute, out var baseUri)
                || !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(baseUri.Query)
                || !string.IsNullOrEmpty(baseUri.Fragment))
            {
                throw new ArgumentException(
                    "CoffeeLearning integration base URL must be an absolute HTTPS URL without a query or fragment.",
                    nameof(integrationBaseUrl));
            }

            if (timeoutSeconds < MinimumTimeoutSeconds || timeoutSeconds > MaximumTimeoutSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds),
                    $"HTTP timeout must be between {MinimumTimeoutSeconds} and {MaximumTimeoutSeconds} seconds.");
            }

            IntegrationBaseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
            TimeoutSeconds = timeoutSeconds;
        }

        public Uri IntegrationBaseUri { get; }
        public int TimeoutSeconds { get; }
    }

    public sealed class CoffeeGameHttpRequest
    {
        public CoffeeGameHttpRequest(string method, Uri uri, string jsonBody = null)
        {
            Method = string.IsNullOrWhiteSpace(method)
                ? throw new ArgumentException("HTTP method is required.", nameof(method))
                : method;
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            JsonBody = jsonBody;
        }

        public string Method { get; }
        public Uri Uri { get; }
        public string JsonBody { get; }

        public override string ToString()
        {
            return Method + " " + Uri.AbsoluteUri;
        }
    }

    public sealed class CoffeeGameHttpResponse
    {
        public CoffeeGameHttpResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public int StatusCode { get; }
        public string Body { get; }
    }

    public interface ICoffeeGameHttpTransport
    {
        Task<CoffeeGameHttpResponse> SendAsync(
            CoffeeGameHttpRequest request,
            string accessToken,
            int timeoutSeconds,
            CancellationToken cancellationToken = default);
    }

    public sealed class CoffeeGameHttpTransportException : Exception
    {
        public CoffeeGameHttpTransportException(string code, bool retryable)
            : base("CoffeeLearning could not complete the HTTP request.")
        {
            Code = string.IsNullOrWhiteSpace(code) ? "NETWORK_UNAVAILABLE" : code;
            Retryable = retryable;
        }

        public string Code { get; }
        public bool Retryable { get; }
    }

    /// <summary>
    /// Contract-v1 HTTP bridge. It owns request validation and response mapping, but receives
    /// credentials and transport through explicit seams so gameplay never owns raw token storage.
    /// </summary>
    public sealed class CoffeeLearningHttpBridge : ILearningBridge
    {
        private readonly CoffeeLearningHttpBridgeOptions options;
        private readonly ICoffeeGameAccessTokenProvider tokenProvider;
        private readonly ICoffeeGameHttpTransport transport;

        public CoffeeLearningHttpBridge(
            CoffeeLearningHttpBridgeOptions options,
            ICoffeeGameAccessTokenProvider tokenProvider,
            ICoffeeGameHttpTransport transport)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public bool IsSignedIn => tokenProvider.HasAccessToken;

        public Task<LearningClaimResult> ClaimTodayAsync(CancellationToken cancellationToken = default)
        {
            // CoffeeLearning contract v1 intentionally exposes no daily-claim endpoint.
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LearningClaimResult(string.Empty, 0, false));
        }

        public Task<AccountIdentityResponseDto> GetAccountIdentityAsync(
            CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(
                new CoffeeGameHttpRequest("GET", BuildUri("account")),
                CreateAccountError,
                cancellationToken);
        }

        public Task<WeakSyncResponseDto> SyncWeakItemsAsync(
            WeakSyncRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return Task.FromResult(CreateWeakError(CreateInvalidRequest("query", "is required")));
            }

            if (request.limit < 1 || request.limit > 100)
            {
                return Task.FromResult(CreateWeakError(
                    CreateInvalidRequest("query.limit", "must be between 1 and 100")));
            }

            if (!CoffeeGameContractV1.IsSupportedWeakLookbackDays(request.lookbackDays))
            {
                return Task.FromResult(CreateWeakError(CreateInvalidRequest(
                    "query.lookbackDays",
                    $"must be between {CoffeeGameContractV1.MinimumWeakLookbackDays} and {CoffeeGameContractV1.MaximumWeakLookbackDays}")));
            }

            var query = string.IsNullOrEmpty(request.cursor)
                ? string.Empty
                : "cursor=" + Uri.EscapeDataString(request.cursor) + "&";
            query += "limit=" + request.limit.ToString(CultureInfo.InvariantCulture)
                + "&lookbackDays=" + request.lookbackDays.ToString(CultureInfo.InvariantCulture);

            return ExecuteAsync(
                new CoffeeGameHttpRequest("GET", BuildUri("weak-items?" + query)),
                CreateWeakError,
                cancellationToken);
        }

        public Task<ChallengeIssueResponseDto> IssueChallengeAsync(
            ChallengeIssueRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.weakItemId)
                || string.IsNullOrWhiteSpace(request.clientRequestId))
            {
                return Task.FromResult(CreateChallengeError(
                    CreateInvalidRequest("body", "weakItemId and clientRequestId are required")));
            }

            return ExecuteAsync(
                new CoffeeGameHttpRequest("POST", BuildUri("challenges"), JsonUtility.ToJson(request)),
                CreateChallengeError,
                cancellationToken);
        }

        public Task<AnswerResultResponseDto> SubmitAnswerAsync(
            AnswerSubmitRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var validationError = ValidateAnswerRequest(request);
            if (validationError != null)
            {
                return Task.FromResult(CreateAnswerError(validationError));
            }

            return ExecuteAsync(
                new CoffeeGameHttpRequest("POST", BuildUri("answers"), JsonUtility.ToJson(request)),
                CreateAnswerError,
                cancellationToken);
        }

        public Task<AnswerResultResponseDto> RecoverResultAsync(
            string resultId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resultId))
            {
                return Task.FromResult(CreateAnswerError(
                    CreateInvalidRequest("path.resultId", "is required")));
            }

            return ExecuteAsync(
                new CoffeeGameHttpRequest(
                    "GET",
                    BuildUri("results/" + Uri.EscapeDataString(resultId))),
                CreateAnswerError,
                cancellationToken);
        }

        private async Task<TResponse> ExecuteAsync<TResponse>(
            CoffeeGameHttpRequest request,
            Func<CoffeeGameErrorDto, TResponse> createErrorResponse,
            CancellationToken cancellationToken)
            where TResponse : class, ICoffeeGameContractResponse
        {
            cancellationToken.ThrowIfCancellationRequested();

            string accessToken;
            try
            {
                if (!tokenProvider.HasAccessToken)
                {
                    return createErrorResponse(CreateError(
                        "AUTHENTICATION_REQUIRED",
                        "CoffeeLearning is not connected.",
                        false));
                }

                accessToken = CoffeeGameAccessToken.Normalize(
                    await tokenProvider.LoadAccessTokenAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return createErrorResponse(CreateError(
                    "CREDENTIAL_UNAVAILABLE",
                    "The CoffeeLearning credential is unavailable.",
                    false));
            }

            CoffeeGameHttpResponse response;
            try
            {
                response = await transport.SendAsync(
                    request,
                    accessToken,
                    options.TimeoutSeconds,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CoffeeGameHttpTransportException exception)
            {
                return createErrorResponse(CreateError(
                    exception.Code,
                    "CoffeeLearning could not be reached.",
                    exception.Retryable));
            }
            catch
            {
                return createErrorResponse(CreateError(
                    "NETWORK_UNAVAILABLE",
                    "CoffeeLearning could not be reached.",
                    true));
            }

            if (response.StatusCode < 200 || response.StatusCode > 299)
            {
                return createErrorResponse(ParseHttpError(response, accessToken));
            }

            try
            {
                var parsed = JsonUtility.FromJson<TResponse>(response.Body);
                if (parsed == null || string.IsNullOrWhiteSpace(parsed.ContractVersion))
                {
                    return createErrorResponse(CreateInvalidResponse());
                }

                CoffeeGameContractV1.RequireSupportedVersion(parsed.ContractVersion);
                if (parsed.Error != null && string.IsNullOrWhiteSpace(parsed.Error.code))
                {
                    // JsonUtility may materialize a missing reference field as an empty
                    // object. Only an allowlisted provider error code makes it an error.
                    parsed.Error = null;
                }
                return parsed;
            }
            catch (UnsupportedContractVersionException)
            {
                throw;
            }
            catch
            {
                return createErrorResponse(CreateInvalidResponse());
            }
        }

        private Uri BuildUri(string relativePath)
        {
            return new Uri(options.IntegrationBaseUri, relativePath);
        }

        private static CoffeeGameErrorDto ParseHttpError(
            CoffeeGameHttpResponse response,
            string accessToken)
        {
            try
            {
                var envelope = JsonUtility.FromJson<CoffeeGameErrorEnvelopeDto>(response.Body);
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.contractVersion))
                {
                    CoffeeGameContractV1.RequireSupportedVersion(envelope.contractVersion);
                }

                if (envelope?.error != null && !string.IsNullOrWhiteSpace(envelope.error.code))
                {
                    RedactSecret(envelope.error, accessToken);
                    return envelope.error;
                }
            }
            catch (UnsupportedContractVersionException)
            {
                throw;
            }
            catch
            {
                // Fall through to a status-only error. Raw response content is never surfaced.
            }

            switch (response.StatusCode)
            {
                case 400:
                    return CreateError("INVALID_REQUEST", "CoffeeLearning rejected the request.", false);
                case 401:
                    return CreateError("AUTHENTICATION_REQUIRED", "CoffeeLearning authentication is required.", false);
                case 403:
                    return CreateError("AUTHORIZATION_DENIED", "CoffeeLearning denied the request.", false);
                case 404:
                    return CreateError("NOT_FOUND", "The CoffeeLearning resource was not found.", false);
                case 408:
                    return CreateError("REQUEST_TIMEOUT", "The CoffeeLearning request timed out.", true);
                case 409:
                    return CreateError("REQUEST_CONFLICT", "CoffeeLearning rejected conflicting request state.", false);
                case 410:
                    return CreateError("RESOURCE_EXPIRED", "The CoffeeLearning resource expired.", false);
                case 413:
                    return CreateError("REQUEST_TOO_LARGE", "The CoffeeLearning request was too large.", false);
                case 429:
                    return CreateError("RATE_LIMITED", "CoffeeLearning temporarily rate limited the request.", true);
                default:
                    return response.StatusCode >= 500
                        ? CreateError("SERVICE_UNAVAILABLE", "CoffeeLearning is temporarily unavailable.", true)
                        : CreateError("HTTP_REQUEST_FAILED", "CoffeeLearning rejected the HTTP request.", false);
            }
        }

        private static CoffeeGameErrorDto ValidateAnswerRequest(AnswerSubmitRequestDto request)
        {
            if (request == null)
            {
                return CreateInvalidRequest("body", "is required");
            }

            if (string.IsNullOrWhiteSpace(request.challengeId))
            {
                return CreateInvalidRequest("body.challengeId", "is required");
            }

            if (string.IsNullOrWhiteSpace(request.clientAttemptId))
            {
                return CreateInvalidRequest("body.clientAttemptId", "is required");
            }

            if (request.answer == null || string.IsNullOrWhiteSpace(request.answer.text))
            {
                return CreateInvalidRequest("body.answer.text", "must contain at least 1 character(s)");
            }

            try
            {
                CoffeeGameContractV1.RequireSupportedInputMode(request.answer.inputMode);
            }
            catch (ArgumentException)
            {
                return CreateInvalidRequest("body.answer.inputMode", "must be typed or speechTranscript");
            }

            return null;
        }

        private static void RedactSecret(CoffeeGameErrorDto error, string accessToken)
        {
            if (error == null || string.IsNullOrEmpty(accessToken))
            {
                return;
            }

            error.code = Redact(error.code, accessToken);
            error.message = Redact(error.message, accessToken);
            if (error.fields == null)
            {
                return;
            }

            foreach (var field in error.fields)
            {
                if (field != null)
                {
                    field.field = Redact(field.field, accessToken);
                    field.issue = Redact(field.issue, accessToken);
                }
            }
        }

        private static string Redact(string value, string accessToken)
        {
            return string.IsNullOrEmpty(value)
                ? value
                : value.Replace(accessToken, "[REDACTED]");
        }

        private static WeakSyncResponseDto CreateWeakError(CoffeeGameErrorDto error)
        {
            return new WeakSyncResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                items = Array.Empty<WeakItemDto>(),
                error = error
            };
        }

        private static AccountIdentityResponseDto CreateAccountError(CoffeeGameErrorDto error)
        {
            return new AccountIdentityResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = error
            };
        }

        private static ChallengeIssueResponseDto CreateChallengeError(CoffeeGameErrorDto error)
        {
            return new ChallengeIssueResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = error
            };
        }

        private static AnswerResultResponseDto CreateAnswerError(CoffeeGameErrorDto error)
        {
            return new AnswerResultResponseDto
            {
                contractVersion = CoffeeGameContractV1.Version,
                error = error
            };
        }

        private static CoffeeGameErrorDto CreateInvalidRequest(string field, string issue)
        {
            return new CoffeeGameErrorDto
            {
                code = "INVALID_REQUEST",
                message = "The CoffeeGAME request does not match contract v1.",
                retryable = false,
                fields = new[] { new CoffeeGameErrorFieldDto { field = field, issue = issue } }
            };
        }

        private static CoffeeGameErrorDto CreateInvalidResponse()
        {
            return CreateError(
                "INVALID_RESPONSE",
                "CoffeeLearning returned an invalid contract response.",
                true);
        }

        private static CoffeeGameErrorDto CreateError(string code, string message, bool retryable)
        {
            return new CoffeeGameErrorDto
            {
                code = code,
                message = message,
                retryable = retryable,
                fields = Array.Empty<CoffeeGameErrorFieldDto>()
            };
        }

    }
}
