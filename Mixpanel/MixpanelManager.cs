using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace mixpanel
{
    internal class MixpanelManager : MonoBehaviour
    {
        #region Singleton
        private static MixpanelManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            _instance = new GameObject("Mixpanel").AddComponent<MixpanelManager>();
            Mixpanel.Load();
        }
        #endregion

        private bool _isBlocking;
        private bool _needsFlush;
        private const int RetryMaxTries = 10;
        private const int RetryDelayFactor = 2;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(this);
            while (true)
            {
                yield return new WaitForSecondsRealtime(MixpanelSettings.Instance.FlushInterval);
                DoFlush();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Mixpanel.Save();
        }

        private void OnApplicationQuit()
        {
            Mixpanel.Save();
        }

        private void DoFlush()
        {
            _needsFlush = true;
            while (_needsFlush)
            {
                _needsFlush = false;
                foreach (MixpanelBatch item in Mixpanel.PrepareBatches())
                {
                    StartCoroutine(BuildRequest(item));
                }
            }
        }

        private IEnumerator BuildRequest(MixpanelBatch batch, int retryCount = 0)
        {
            string url = batch.Url;
            if (MixpanelSettings.Instance.ShowDebug) Debug.Log($"[Mixpanel] Sending Request - '{url}'");
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            while (!request.isDone) yield return new WaitForEndOfFrame();
            // TODO: Be smarter about the errors coming back?
            if (!request.isNetworkError && !request.isHttpError)
            {
                Mixpanel.SuccessfulBatch(batch);
                yield break;
            }
            if (request.responseCode == 413) // Payload Too Large
            {
                // Rebatch
                int newBatchSize = (int)(batch.Requests.Count * 0.5f);
                foreach (IEnumerable<MixpanelRequest> newBatch in batch.Requests.Batch(newBatchSize))
                {
                    StartCoroutine(BuildRequest(new MixpanelBatch(batch.Endpoint, newBatch)));
                }
                _needsFlush = true;
                yield break;
            }
            if (retryCount > RetryMaxTries)
            {
                // Split Batch
                foreach (MixpanelRequest item in batch.Requests)
                {
                    StartCoroutine(BuildRequest(new MixpanelBatch(batch.Endpoint, item)));
                }
                _needsFlush = true;
                yield break;
            }
            Debug.LogWarning($"[Mixpanel] Failed to sent batch because - '{request.error}'");
            retryCount += 1;
            // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 = 2046 seconds total
            yield return new WaitForSecondsRealtime((float)Math.Pow(2, retryCount));
            StartCoroutine(BuildRequest(batch, retryCount));
        }

        #region Static

        internal static void Flush()
        {
            _instance.DoFlush();
        }
        
        #endregion
    }
}
