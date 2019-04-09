using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mixpanel
{
    public class MixpanelAsync : MonoBehaviour
    {
        private Stack<IEnumerator> queue;

        private void Awake()
        {
            queue = new Stack<IEnumerator>();
        }

        private void Start()
        {
            DontDestroyOnLoad(this);
        }

        private void LateUpdate()
        {
            foreach (var item in queue)
            {
                StartCoroutine(item);
            }
        }

        internal void Enqueue(IEnumerator request)
        {
            queue.Push(request);
        }
    }
}
