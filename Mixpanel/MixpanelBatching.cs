using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace mixpanel
{
    internal struct MixpanelBatch
    {
        internal string Endpoint;
        internal Value Data;

        internal string Url => $"{Endpoint}/?ip=1&data={Base64Encode(Data.ToString())}";

        private static string Base64Encode(string text) {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }
    }
    
    public static partial class Mixpanel
    {
        private const string BatchesName = "Mixpanel.Batches";

        private static readonly Dictionary<string, List<Value>> Buffer = new Dictionary<string, List<Value>>();
        internal static readonly Dictionary<string, MixpanelBatch> Batches = new Dictionary<string, MixpanelBatch>();

        private static void Enqueue(string endpoint, Value data)
        {
            if (Buffer.ContainsKey(endpoint))  Buffer[endpoint].Add(data);
            else Buffer[endpoint] = new List<Value>{ data };
        }

        private static MixpanelBatch CreateBatch(string endpoint, Value data)
        {
            return new MixpanelBatch {Endpoint = endpoint, Data = data};
        }

        private static void InsertBatch(MixpanelBatch batch)
        {
            Batches.Add(Guid.NewGuid().ToString(), batch);
        }
        
        private static void PrepareBatches()
        {
            foreach (KeyValuePair<string, List<Value>> item in Buffer)
            {
                foreach (IEnumerable<Value> batch in item.Value.Batch(40))
                {
                    InsertBatch(CreateBatch(item.Key, new Value(batch)));
                }
            }
            Buffer.Clear();
        }

        internal static void SuccessfulBatch(string id)
        {
            Batches.Remove(id);
        }
        
        internal static void BisectBatch(string id, MixpanelBatch batch)
        {
            Batches.Remove(id);
            foreach (Value item in batch.Data)
            {
                InsertBatch(CreateBatch(batch.Endpoint, item));
            }
        }

        internal static void ReBatch(string id, MixpanelBatch batch)
        {
            Batches.Remove(id);
            int newBatchSize = (int)(batch.Data.Count * 0.5f);
            foreach (IEnumerable<Value> newBatch in batch.Data.Values.Batch(newBatchSize))
            {
                InsertBatch(CreateBatch(batch.Endpoint, new Value(newBatch)));
            }
        }

        internal static void LoadBatches()
        {
            if (!PlayerPrefs.HasKey(BatchesName))
            {
                PlayerPrefs.SetString(BatchesName, Value.Array.Serialize());
            }
            Batches.Clear();
            Value data = Value.Deserialize(PlayerPrefs.GetString(BatchesName));
            foreach (Value item in data)
            {
                InsertBatch(CreateBatch(item["Endpoint"], item["Data"]));
            }
        }

        internal static void SaveBatches()
        {
            PrepareBatches();
            Value data = Value.Array;
            foreach (MixpanelBatch batch in Batches.Values)
            {
                data.Add(new Value{ {"Endpoint", batch.Endpoint}, {"Data", batch.Data}});
            }
            PlayerPrefs.SetString(BatchesName, data.Serialize());
        }
    }
}