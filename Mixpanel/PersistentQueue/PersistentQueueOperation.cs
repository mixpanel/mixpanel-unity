namespace mixpanel.queue
{
    /// <summary>
    /// Type of change applicable to a queue
    /// </summary>
    public enum PersistentQueueOperationTypes : byte
    {
        /// <summary> Add new data to the queue </summary>
        ENQUEUE = 1,

        /// <summary> Retrieve and remove data from a queue </summary>
        DEQUEUE = 2,

        /// <summary> Revert a dequeue. Data will remain present on the queue </summary>
        REINSTATE = 3
    }
    
    public class PersistentQueueOperation
    {
        public PersistentQueueOperation(PersistentQueueOperationTypes type, int fileNumber, int start, int length)
        {
            Type = type;
            FileNumber = fileNumber;
            Start = start;
            Length = length;
        }

        public readonly PersistentQueueOperationTypes Type;
        public readonly int FileNumber;
        public readonly int Start;
        public readonly int Length;
    }
}
