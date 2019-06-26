using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mixpanel
{
    public class MixpanelAsync : MonoBehaviour
    {
        private Stack<IEnumerator> _queue;

        private void Awake()
        {
            this._queue = new Stack<IEnumerator>();
        }

        private void Start()
        {
            DontDestroyOnLoad(this);
        }

        private void LateUpdate()
        {
            foreach (IEnumerator item in this._queue)
            {
                StartCoroutine(item);
            }
        }

        internal void Enqueue(IEnumerator request)
        {
            this._queue.Push(request);
        }
    }
}
