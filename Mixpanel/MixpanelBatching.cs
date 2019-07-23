using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using mixpanel.serialization;
using UnityEngine;

namespace mixpanel
{
    [Serializable]
    internal struct MixpanelRequest
    {
        [SerializeField]
        internal string id;
        [SerializeField]
        internal Value data;
    }
    
    internal struct MixpanelBatch
    {
        internal string Endpoint;
        internal List<MixpanelRequest> Requests;
        private string Payload;
        
        internal MixpanelBatch(string endpoint, MixpanelRequest request)
        {
            Endpoint = endpoint;
            Requests = new List<MixpanelRequest> { request };
            Payload = Base64Encode(new Value(Requests.Select(x => x.data)).ToString());
        }

        internal MixpanelBatch(string endpoint, IEnumerable<MixpanelRequest> requests)
        {
            Endpoint = endpoint;
            Requests = new List<MixpanelRequest>(requests);
            Payload = Base64Encode(new Value(Requests.Select(x => x.data)).ToString());
        }

        internal string Url => $"{Endpoint}/?ip=1&data={Payload}";

        private static string Base64Encode(string text) {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }
    }
    
    public static partial class Mixpanel
    {
        private static void Enqueue(string endpoint, Value data)
        {
            MixpanelRequest request = new MixpanelRequest { id = Guid.NewGuid().ToString(), data = data };
            if (!Data.buffer.ContainsKey(endpoint)) Data.buffer[endpoint] = new Dictionary<string, MixpanelRequest>();
            Data.buffer[endpoint].Add(request.id, request);
            Save();
        }

        internal static IEnumerable<MixpanelBatch> PrepareBatches()
        {
            foreach (KeyValuePair<string, Dictionary<string, MixpanelRequest>> item in Data.buffer)
            {
                foreach (IEnumerable<MixpanelRequest> batch in item.Value.Values.Batch(40))
                {
                    yield return new MixpanelBatch(item.Key, batch);
                }
            }
        }

        internal static void SuccessfulBatch(MixpanelBatch batch)
        {
            Dictionary<string, MixpanelRequest> buffer = Data.buffer[batch.Endpoint];
            foreach (string id in batch.Requests.Select(x => x.id))
            {
                buffer.Remove(id);
            }

            Data.buffer[batch.Endpoint] = buffer;
            Save();
        }
    }
}