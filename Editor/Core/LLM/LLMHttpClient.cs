using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SemanticSearch.Editor.Core.LLM
{
    public class LLMHttpClient
    {
        readonly LLMApiConfig _config;
        readonly SemaphoreSlim _semaphore;

        public LLMHttpClient(LLMApiConfig config)
        {
            _config = config;
            _semaphore = new SemaphoreSlim(config.MaxConcurrent, config.MaxConcurrent);
        }

        public async Task<string> PostJsonAsync(string url, string jsonBody, string apiKey)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await PostWithRetryAsync(url, jsonBody, apiKey);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        async Task<string> PostWithRetryAsync(string url, string jsonBody, string apiKey)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    return await SendRequestAsync(url, jsonBody, apiKey);
                }
                catch (LLMRequestException ex) when (ex.IsRetryable && retries < _config.MaxRetries)
                {
                    retries++;
                    int delayMs = (int)(Math.Pow(2, retries - 1) * 1000);
                    await Task.Delay(delayMs);
                }
            }
        }

        Task<string> SendRequestAsync(string url, string jsonBody, string apiKey)
        {
            var tcs = new TaskCompletionSource<string>();
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");

            if (_config.ProviderType == LLMProviderType.Gemini)
                request.SetRequestHeader("x-goog-api-key", apiKey);
            else
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var op = request.SendWebRequest();

            void Cleanup()
            {
                EditorApplication.update -= PollComplete;
                AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
                request.Abort();
                request.Dispose();
                tcs.TrySetException(new LLMRequestException("Request aborted due to domain unload.", 0, true));
            }

            void PollComplete()
            {
                if (!op.isDone) return;
                EditorApplication.update -= PollComplete;
                AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;

                try
                {
                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        tcs.TrySetException(new LLMRequestException(
                            $"Network error: {request.error}", 0, true));
                        return;
                    }

                    long code = request.responseCode;
                    string body = request.downloadHandler.text;

                    if (code >= 200 && code < 300)
                    {
                        tcs.TrySetResult(body);
                        return;
                    }

                    bool retryable = code == 429 || code >= 500;
                    tcs.TrySetException(new LLMRequestException(
                        $"HTTP {code}: {body}", code, retryable));
                }
                finally
                {
                    request.Dispose();
                }
            }

            EditorApplication.update += PollComplete;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            return tcs.Task;
        }
    }

    public class LLMRequestException : Exception
    {
        public long StatusCode { get; }
        public bool IsRetryable { get; }

        public LLMRequestException(string message, long statusCode, bool isRetryable)
            : base(message)
        {
            StatusCode = statusCode;
            IsRetryable = isRetryable;
        }
    }
}
