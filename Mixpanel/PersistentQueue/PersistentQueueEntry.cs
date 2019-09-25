namespace mixpanel.queue
{
    /// <summary>
    /// Internal storage file entry
    /// </summary>
    public class PersistentQueueEntry
    {
        /// <summary>
        /// Represents an entry in a file by file number, position and span
        /// </summary>
        public PersistentQueueEntry(int fileNumber, int start, int length)
        {
            FileNumber = fileNumber;
            Start = start;
            Length = length;
        }

        /// <summary>
        /// Map a queue/log operation to a file entry
        /// </summary>
        public PersistentQueueEntry(PersistentQueueOperation operation) : this(operation.FileNumber, operation.Start, operation.Length) {}

        /// <summary>
        /// The actual data for this entry. 
        /// This only has value coming _out_ of the queue.
        /// </summary>
        public byte[] Data;

        public readonly int FileNumber;

        /// <summary> offset of start of entry in file </summary>
        public readonly int Start;

        /// <summary> length of entry on disk </summary>
        public readonly int Length;

        /// <summary>
        /// Compare this entry to other for exact equality
        /// </summary>
        private bool Equals(PersistentQueueEntry obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.FileNumber == FileNumber && obj.Start == Start && obj.Length == Length;
        }
		
        /// <summary>
        /// Compare this entry to other for exact equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PersistentQueueEntry);
        }
		
        /// <summary>
        /// Storage hash code
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int result = FileNumber;
                result = (result * 397) ^ Start;
                result = (result * 397) ^ Length;
                return result;
            }
        }

        /// <summary>
        /// Compare entries by value
        /// </summary>
        public static bool operator ==(PersistentQueueEntry left, PersistentQueueEntry right) => Equals(left, right);

        /// <summary>
        /// Compare entries by value
        /// </summary>
        public static bool operator !=(PersistentQueueEntry left, PersistentQueueEntry right) => !Equals(left, right);
    }
}
