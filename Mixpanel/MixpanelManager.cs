using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
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
                Mixpanel.Flush();
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
                foreach (KeyValuePair<string, MixpanelBatch> item in Mixpanel.Batches)
                {
                    StartCoroutine(BuildRequest(item.Key, item.Value));
                }
            }
        }

        private IEnumerator BuildRequest(string id, MixpanelBatch batch, int retryCount = 0)
        {
            if (MixpanelSettings.Instance.ShowDebug) Debug.Log($"[Mixpanel] Sending Request - '{batch.Url}' with payload '{batch.Payload}'");
            WWWForm form = new WWWForm();
            form.AddField("data", batch.Payload);
            UnityWebRequest request = UnityWebRequest.Post(batch.Url, form);
            yield return request.SendWebRequest();
            while (!request.isDone) yield return new WaitForEndOfFrame();
            // TODO: Be smarter about the errors coming back?
            if (!request.isNetworkError && !request.isHttpError)
            {
                Mixpanel.SuccessfulBatch(id);
                yield break;
            }
            if (request.responseCode == 413) // Payload Too Large
            {
                Mixpanel.ReBatch(id, batch);
                _needsFlush = true;
                yield break;
            }
            if (retryCount > RetryMaxTries)
            {
                Mixpanel.BisectBatch(id, batch);
                _needsFlush = true;
                yield break;
            }
            Debug.LogWarning($"[Mixpanel] Failed to sent batch because - '{request.error}'");
            retryCount += 1;
            // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 = 2046 seconds total
            yield return new WaitForSecondsRealtime((float)Math.Pow(2, retryCount));
            StartCoroutine(BuildRequest(id, batch, retryCount));
        }

        #region Static

        internal static void Flush()
        {
            _instance.DoFlush();
        }
        
        #endregion
    }
}
