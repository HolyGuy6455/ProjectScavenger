using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Scavenger.Core
{
    /// <summary>Updates catalogs and caches startup content before any game assets are loaded.</summary>
    public sealed class StartupContentPatcher
    {
        private readonly HashSet<string> loadedCatalogUrls = new HashSet<string>();
        public string Status { get; private set; } = "Checking for updates...";
        public string Error { get; private set; }
        public long RequiredBytes { get; private set; }
        public long DownloadedBytes { get; private set; }
        public int UpdatedCatalogs { get; private set; }
        public bool IsReady { get; private set; }
        public bool WasCancelled { get; private set; }

        public async Task<bool> RunAsync(string additionalCatalogUrl, IEnumerable<string> downloadKeys,
            Func<long, Task<bool>> confirmDownload = null)
        {
            IsReady = false;
            WasCancelled = false;
            Error = null;
            RequiredBytes = DownloadedBytes = 0;
            UpdatedCatalogs = 0;
            var previousOverride = Addressables.WebRequestOverride;
            Action<UnityWebRequest> requestOverride = request =>
            {
                previousOverride?.Invoke(request);
                if (request.timeout == 0) request.timeout = 30;
            };
            Addressables.WebRequestOverride = requestOverride;
            try
            {
                Status = "Checking for updates...";
                await CompleteAsync(Addressables.InitializeAsync(false));

                if (!string.IsNullOrWhiteSpace(additionalCatalogUrl) && !loadedCatalogUrls.Contains(additionalCatalogUrl))
                {
                    if (!Uri.TryCreate(additionalCatalogUrl, UriKind.Absolute, out var uri)
                        || (uri.Scheme != "http" && uri.Scheme != "https"))
                        throw new InvalidOperationException("The content catalog requires an HTTP(S) URL.");
                    // Release this handle so future catalog checks can download a newer version.
                    await CompleteAsync(Addressables.LoadContentCatalogAsync(additionalCatalogUrl, false));
                    loadedCatalogUrls.Add(additionalCatalogUrl);
                }

                List<string> catalogs = await CompleteAsync(Addressables.CheckForCatalogUpdates(false));
                if (catalogs != null && catalogs.Count > 0)
                {
                    Status = "Updating content catalog...";
                    await CompleteAsync(Addressables.UpdateCatalogs(catalogs, false));
                    UpdatedCatalogs = catalogs.Count;
                }

                var keys = (downloadKeys ?? Array.Empty<string>())
                    .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct().Cast<object>().ToList();
                if (keys.Count > 0)
                {
                    Status = "Checking download size...";
                    RequiredBytes = await CompleteAsync(Addressables.GetDownloadSizeAsync(keys));
                    if (RequiredBytes > 0)
                    {
                        if (confirmDownload != null)
                        {
                            Status = "Waiting for download confirmation...";
                            if (!await confirmDownload(RequiredBytes))
                            {
                                WasCancelled = true;
                                Status = "Download cancelled. Tap to retry.";
                                return false;
                            }
                        }

                        var download = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);
                        try
                        {
                            while (!download.IsDone)
                            {
                                var progress = download.GetDownloadStatus();
                                DownloadedBytes = progress.DownloadedBytes;
                                Status = $"Downloading content... {progress.Percent:P0}";
                                await Task.Yield();
                            }
                            RequireSuccess(download);
                            DownloadedBytes = RequiredBytes;
                        }
                        finally
                        {
                            if (download.IsValid()) Addressables.Release(download);
                        }
                    }
                }

                IsReady = true;
                Status = "Content is up to date.";
                Debug.Log($"[ContentPatch] Ready. Updated catalogs: {UpdatedCatalogs}, download bytes: {RequiredBytes}.");
                return true;
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                Status = "Content update failed. Tap to retry.";
                Debug.LogError($"[ContentPatch] {exception.Message}");
                return false;
            }
            finally
            {
                if (Addressables.WebRequestOverride == requestOverride)
                    Addressables.WebRequestOverride = previousOverride;
            }
        }

        private static async Task<T> CompleteAsync<T>(AsyncOperationHandle<T> handle)
        {
            try
            {
                await handle.Task;
                RequireSuccess(handle);
                return handle.Result;
            }
            finally
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        private static void RequireSuccess(AsyncOperationHandle handle)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw handle.OperationException ?? new InvalidOperationException("Content request failed.");
        }
    }
}
