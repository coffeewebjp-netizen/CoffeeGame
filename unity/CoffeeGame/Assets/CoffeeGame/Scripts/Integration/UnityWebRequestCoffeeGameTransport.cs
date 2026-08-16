using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace CoffeeGame.Integration
{
    public sealed class UnityWebRequestCoffeeGameTransport : ICoffeeGameHttpTransport
    {
        public async Task<CoffeeGameHttpResponse> SendAsync(
            CoffeeGameHttpRequest requestData,
            string accessToken,
            int timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
            if (requestData == null)
            {
                throw new ArgumentNullException(nameof(requestData));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var normalizedToken = CoffeeGameAccessToken.Normalize(accessToken);

            using (var request = new UnityWebRequest(requestData.Uri.AbsoluteUri, requestData.Method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + normalizedToken);

                if (requestData.JsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestData.JsonBody));
                    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }

                var completion = new TaskCompletionSource<CoffeeGameHttpResponse>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var operation = request.SendWebRequest();
                void CompleteRequest(UnityEngine.AsyncOperation _)
                {
                    try
                    {
                        if (request.result == UnityWebRequest.Result.ConnectionError
                            || request.result == UnityWebRequest.Result.DataProcessingError)
                        {
                            var isTimeout = !string.IsNullOrEmpty(request.error)
                                && request.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
                            completion.TrySetException(new CoffeeGameHttpTransportException(
                                isTimeout ? "REQUEST_TIMEOUT" : "NETWORK_UNAVAILABLE",
                                true));
                            return;
                        }

                        var statusCode = request.responseCode > int.MaxValue
                            ? int.MaxValue
                            : (int)request.responseCode;
                        completion.TrySetResult(new CoffeeGameHttpResponse(
                            statusCode,
                            request.downloadHandler?.text ?? string.Empty));
                    }
                    catch
                    {
                        completion.TrySetException(new CoffeeGameHttpTransportException(
                            "NETWORK_UNAVAILABLE",
                            true));
                    }
                }

                operation.completed += CompleteRequest;
                if (operation.isDone)
                {
                    CompleteRequest(operation);
                }

                using (cancellationToken.Register(() =>
                {
                    request.Abort();
                    completion.TrySetCanceled();
                }))
                {
                    CoffeeGameHttpResponse response = await completion.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                    return response;
                }
            }
        }
    }
}
