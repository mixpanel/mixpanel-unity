// This is a modified version of DiskQueue implementation that can be found here: https://github.com/i-e-b/DiskQueue

// Copyright (c) 2005 - 2008 Ayende Rahien (ayende@ayende.com)
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//     * Neither the name of Ayende Rahien nor the names of its
//     contributors may be used to endorse or promote products derived from this
//     software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
// THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace mixpanel.queue
{
	public class PersistentQueue
	{
		private static readonly object ConfigLock = new object();
		
		private readonly HashSet<PersistentQueueEntry> _checkedOutEntries = new HashSet<PersistentQueueEntry>();
		private readonly Dictionary<int, int> _countOfItemsPerFile = new Dictionary<int, int>();
		private readonly LinkedList<PersistentQueueEntry> _entries = new LinkedList<PersistentQueueEntry>();
		private readonly string _path;
		private readonly object _transactionLogLock = new object();
		private readonly object _writerLock = new object();
		private readonly bool _trimTransactionLogOnDispose;
		private readonly int _maxFileSize;
		private volatile bool _disposed;
		private FileStream _fileLock;

		public PersistentQueue(string path, int maxFileSize = PersistentQueueUtils._16Megabytes)
		{
			lock (ConfigLock)
			{
				_disposed = true;
				_trimTransactionLogOnDispose = true;
				_maxFileSize = maxFileSize;
				try
				{
					_path = Path.GetFullPath(path);
					if (!Directory.Exists(_path))
						Directory.CreateDirectory(_path);
					LockQueue();
				}
				catch (UnauthorizedAccessException)
				{
					throw new UnauthorizedAccessException($"Directory \"{path}\" does not exist or is missing write permissions");
				}
				catch (IOException e)
				{
					GC.SuppressFinalize(this); //avoid finalizing invalid instance
					throw new InvalidOperationException("Another instance of the queue is already in action, or directory does not exists", e);
				}

				try
				{
					ReadMetaState();
					ReadTransactionLog();
				}
				catch (Exception)
				{
					GC.SuppressFinalize(this); //avoid finalizing invalid instance
					UnlockQueue();
					throw;
				}

				_disposed = false;
			}
		}
		
		~PersistentQueue()
		{
			if (!_disposed) Dispose();
		}

		private void UnlockQueue()
		{
			if (_path == null) return;
			string target = Path.Combine(_path, "lock");
			if (_fileLock != null)
			{
				_fileLock.Dispose();
				File.Delete(target);
			}

			_fileLock = null;
		}

		private void LockQueue()
		{
			string target = Path.Combine(_path, "lock");
			_fileLock = new FileStream(
				target,
				FileMode.Create,
				FileAccess.ReadWrite,
				FileShare.None);
		}
		
		public int CurrentCountOfItemsInQueue
		{
			get
			{
				lock (_entries)
				{
					return _entries.Count + _checkedOutEntries.Count;
				}
			}
		}

		private long CurrentFilePosition { get; set; }

		private string TransactionLog => Path.Combine(_path, "transaction.log");

		private string Meta => Path.Combine(_path, "meta.state");

		public int CurrentFileNumber { get; private set; }

		public void Dispose()
		{
			lock (ConfigLock)
			{
				if (_disposed) return;
				try
				{
					_disposed = true;
					lock (_transactionLogLock)
					{
						if (_trimTransactionLogOnDispose) FlushTrimmedTransactionLog();
					}

					GC.SuppressFinalize(this);
				}
				finally
				{
					UnlockQueue();
				}
			}
		}

		public void AcquireWriter(Stream stream, Func<Stream, long> action, Action<Stream> onReplaceStream)
		{
			lock (_writerLock)
			{
				if (stream.Position != CurrentFilePosition)
				{
					stream.Position = CurrentFilePosition;
				}

				CurrentFilePosition = action(stream);
				if (CurrentFilePosition < _maxFileSize) return;

				CurrentFileNumber += 1;
				FileStream writer = CreateWriter();
				// we assume same size messages, or near size messages
				// that gives us a good heuristic for creating the size of 
				// the new file, so it wouldn't be fragmented
				writer.SetLength(CurrentFilePosition);
				CurrentFilePosition = 0;
				onReplaceStream(writer);
			}
		}

		public void CommitTransaction(ICollection<PersistentQueueOperation> operations)
		{
			if (operations.Count == 0)
				return;

			byte[] transactionBuffer = GenerateTransactionBuffer(operations);

			lock (_transactionLogLock)
			{
				long txLogSize;
				using (FileStream stream = WaitForTransactionLog(transactionBuffer))
				{
					stream.Write(transactionBuffer, 0, transactionBuffer.Length);
					txLogSize = stream.Position;
					stream.Flush();
				}

				ApplyTransactionOperations(operations);
				TrimTransactionLogIfNeeded(txLogSize);

				PersistentQueueUtils.Write(Meta, stream =>
				{
					byte[] bytes = BitConverter.GetBytes(CurrentFileNumber);
					stream.Write(bytes, 0, bytes.Length);
					bytes = BitConverter.GetBytes(CurrentFilePosition);
					stream.Write(bytes, 0, bytes.Length);
				});

				FlushTrimmedTransactionLog();
			}
		}

		private FileStream WaitForTransactionLog(IReadOnlyCollection<byte> transactionBuffer)
		{
			for (int i = 0; i < 10; i++)
			{
				try
				{
					return new FileStream(TransactionLog,
						FileMode.Append,
						FileAccess.Write,
						FileShare.None,
						transactionBuffer.Count,
						FileOptions.SequentialScan | FileOptions.WriteThrough);
				}
				catch (Exception)
				{
					Thread.Sleep(250);
				}
			}

			throw new TimeoutException("Could not acquire transaction log lock");
		}

		public PersistentQueueEntry Dequeue()
		{
			lock (_entries)
			{
				LinkedListNode<PersistentQueueEntry> first = _entries.First;
				if (first == null)
					return null;
				PersistentQueueEntry entry = first.Value;

				if (entry.Data == null)
				{
					ReadAhead();
				}

				_entries.RemoveFirst();
				// we need to create a copy so we will not hold the data
				// in memory as well as the position
				lock (_checkedOutEntries)
				{
					_checkedOutEntries.Add(new PersistentQueueEntry(entry.FileNumber, entry.Start, entry.Length));
				}

				return entry;
			}
		}

		/// <summary>
		/// Assumes that entries has at least one entry. Should be called inside a lock.
		/// </summary>
		private void ReadAhead()
		{
			long currentBufferSize = 0;
			PersistentQueueEntry firstEntry = _entries.First.Value;
			PersistentQueueEntry lastEntry = firstEntry;
			foreach (PersistentQueueEntry entry in _entries)
			{
				// we can't read ahead to another file or
				// if we have unordered queue, or sparse items
				if (entry != lastEntry &&
				    (entry.FileNumber != lastEntry.FileNumber ||
				     entry.Start != (lastEntry.Start + lastEntry.Length)))
					break;
				if (currentBufferSize + entry.Length > PersistentQueueUtils._1Megabytes)
					break;
				lastEntry = entry;
				currentBufferSize += entry.Length;
			}

			if (lastEntry == firstEntry)
				currentBufferSize = lastEntry.Length;

			byte[] buffer = ReadEntriesFromFile(firstEntry, currentBufferSize);

			int index = 0;
			foreach (PersistentQueueEntry entry in _entries)
			{
				entry.Data = new byte[entry.Length];
				Buffer.BlockCopy(buffer, index, entry.Data, 0, entry.Length);
				index += entry.Length;
				if (entry == lastEntry)
					break;
			}
		}

		private byte[] ReadEntriesFromFile(PersistentQueueEntry firstEntry, long currentBufferSize)
		{
			byte[] buffer = new byte[currentBufferSize];
			if (firstEntry.Length < 1) return buffer;
			using (FileStream reader = new FileStream(GetDataPath(firstEntry.FileNumber),
				FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
			{
				reader.Position = firstEntry.Start;
				int totalRead = 0;
				do
				{
					int bytesRead = reader.Read(buffer, totalRead, buffer.Length - totalRead);
					if (bytesRead == 0)
						throw new InvalidOperationException("End of file reached while trying to read queue item");
					totalRead += bytesRead;
				} while (totalRead < buffer.Length);
			}

			return buffer;
		}

		public PersistentQueueSession OpenSession()
		{
			if (_disposed) throw new Exception("This queue has been disposed");
			return new PersistentQueueSession(this, CreateWriter());
		}

		public void Reinstate(IEnumerable<PersistentQueueOperation> reinstatedOperations)
		{
			lock (_entries)
			{
				ApplyTransactionOperations(
					from entry in reinstatedOperations.Reverse()
					where entry.Type == PersistentQueueOperationTypes.DEQUEUE
					select new PersistentQueueOperation(
						PersistentQueueOperationTypes.REINSTATE,
						entry.FileNumber,
						entry.Start,
						entry.Length
					)
				);
			}
		}

		private void ReadTransactionLog()
		{
			var requireTxLogTrimming = false;
			PersistentQueueUtils.Read(TransactionLog, stream =>
			{
				using (var binaryReader = new BinaryReader(stream))
				{
					bool readingTransaction = false;
					try
					{
						int txCount = 0;
						while (true)
						{
							txCount += 1;
							// this code ensures that we read the full transaction
							// before we start to apply it. The last truncated transaction will be
							// ignored automatically.
							AssertTransactionSeparator(binaryReader, txCount, PersistentQueueMarkers.START,
								() => readingTransaction = true);

							var opsCount = binaryReader.ReadInt32();
							var txOps = new List<PersistentQueueOperation>(opsCount);
							for (var i = 0; i < opsCount; i++)
							{
								AssertOperationSeparator(binaryReader);
								var operation = new PersistentQueueOperation(
									(PersistentQueueOperationTypes) binaryReader.ReadByte(),
									binaryReader.ReadInt32(),
									binaryReader.ReadInt32(),
									binaryReader.ReadInt32()
								);
								txOps.Add(operation);
								//if we have non enqueue entries, this means 
								// that we have not closed properly, so we need
								// to trim the log
								if (operation.Type != PersistentQueueOperationTypes.ENQUEUE)
									requireTxLogTrimming = true;
							}

							// check that the end marker is in place
							AssertTransactionSeparator(binaryReader, txCount, PersistentQueueMarkers.END, () => { });
							readingTransaction = false;
							ApplyTransactionOperations(txOps);
						}
					}
					catch (EndOfStreamException)
					{
						// we have a truncated transaction, need to clear that
						if (readingTransaction) requireTxLogTrimming = true;
					}
				}
			});
			if (requireTxLogTrimming) FlushTrimmedTransactionLog();
		}

		private void FlushTrimmedTransactionLog()
		{
			byte[] transactionBuffer;
			using (MemoryStream ms = new MemoryStream())
			{
				ms.Write(PersistentQueueUtils.StartTransactionSeparator, 0, PersistentQueueUtils.StartTransactionSeparator.Length);

				byte[] count = BitConverter.GetBytes(CurrentCountOfItemsInQueue);
				ms.Write(count, 0, count.Length);

				PersistentQueueEntry[] checkedOut;
				lock (_checkedOutEntries)
				{
					checkedOut = _checkedOutEntries.ToArray();
				}

				foreach (var entry in checkedOut)
				{
					WriteEntryToTransactionLog(ms, entry, PersistentQueueOperationTypes.ENQUEUE);
				}

				PersistentQueueEntry[] listedEntries;
				lock (_entries)
				{
					listedEntries = _entries.ToArray();
				}

				foreach (var entry in listedEntries)
				{
					WriteEntryToTransactionLog(ms, entry, PersistentQueueOperationTypes.ENQUEUE);
				}

				ms.Write(PersistentQueueUtils.EndTransactionSeparator, 0, PersistentQueueUtils.EndTransactionSeparator.Length);
				ms.Flush();
				transactionBuffer = ms.ToArray();
			}

			PersistentQueueUtils.Write(TransactionLog, stream =>
			{
				stream.SetLength(transactionBuffer.Length);
				stream.Write(transactionBuffer, 0, transactionBuffer.Length);
			});
		}

		/// <summary>
		/// This special purpose function is to work around potential issues with Mono
		/// </summary>
//		private static PersistentQueueEntry[] ToArray(LinkedList<PersistentQueueEntry> list)
//		{
//			if (list == null) return new PersistentQueueEntry[0];
//			List<PersistentQueueEntry> outp = new List<PersistentQueueEntry>(25);
//			LinkedListNode<PersistentQueueEntry> cur = list.First;
//			while (cur != null)
//			{
//				outp.Add(cur.Value);
//				cur = cur.Next;
//			}
//
//			return outp.ToArray();
//		}

		private static void WriteEntryToTransactionLog(Stream ms, PersistentQueueEntry entry, PersistentQueueOperationTypes operationType)
		{
			ms.Write(PersistentQueueUtils.OperationSeparatorBytes, 0, PersistentQueueUtils.OperationSeparatorBytes.Length);

			ms.WriteByte((byte) operationType);

			byte[] fileNumber = BitConverter.GetBytes(entry.FileNumber);
			ms.Write(fileNumber, 0, fileNumber.Length);

			byte[] start = BitConverter.GetBytes(entry.Start);
			ms.Write(start, 0, start.Length);

			byte[] length = BitConverter.GetBytes(entry.Length);
			ms.Write(length, 0, length.Length);
		}

		private void AssertOperationSeparator(BinaryReader reader)
		{
			int separator = reader.ReadInt32();
			if (separator == PersistentQueueUtils.OperationSeparator) return; // OK

			throw new EndOfStreamException("Unexpected data in transaction log. Expected to get transaction separator but got unknown data");
		}

		private IEnumerable<int> ApplyTransactionOperationsInMemory(IEnumerable<PersistentQueueOperation> operations)
		{
			foreach (PersistentQueueOperation operation in operations)
			{
				switch (operation.Type)
				{
					case PersistentQueueOperationTypes.ENQUEUE:
						PersistentQueueEntry entryToAdd = new PersistentQueueEntry(operation);
						lock (_entries)
						{
							_entries.AddLast(entryToAdd);
						}
						int itemCountAddition = _countOfItemsPerFile.GetValueOrDefault(entryToAdd.FileNumber);
						_countOfItemsPerFile[entryToAdd.FileNumber] = itemCountAddition + 1;
						break;

					case PersistentQueueOperationTypes.DEQUEUE:
						PersistentQueueEntry entryToRemove = new PersistentQueueEntry(operation);
						lock (_checkedOutEntries)
						{
							_checkedOutEntries.Remove(entryToRemove);
						}

						int itemCountRemoval = _countOfItemsPerFile.GetValueOrDefault(entryToRemove.FileNumber);
						_countOfItemsPerFile[entryToRemove.FileNumber] = itemCountRemoval - 1;
						break;

					case PersistentQueueOperationTypes.REINSTATE:
						PersistentQueueEntry entryToReinstate = new PersistentQueueEntry(operation);
						lock (_entries)
						{
							_entries.AddFirst(entryToReinstate);
						}

						lock (_checkedOutEntries)
						{
							_checkedOutEntries.Remove(entryToReinstate);
						}

						break;
				}
			}

			HashSet<int> filesToRemove = new HashSet<int>(
				from pair in _countOfItemsPerFile
				where pair.Value < 1
				select pair.Key
			);

			foreach (int i in filesToRemove)
			{
				_countOfItemsPerFile.Remove(i);
			}

			return filesToRemove.ToArray();
		}

		private void AssertTransactionSeparator(BinaryReader binaryReader, int txCount, PersistentQueueMarkers whichSeparator, Action hasData)
		{
			byte[] bytes = binaryReader.ReadBytes(16);
			if (bytes.Length == 0) throw new EndOfStreamException();

			hasData();
			if (bytes.Length != 16)
			{
				// looks like we have a truncated transaction in this case, we will 
				// say that we run into end of stream and let the log trimming to deal with this
				if (binaryReader.BaseStream.Length == binaryReader.BaseStream.Position)
				{
					throw new EndOfStreamException();
				}

				throw new EndOfStreamException($"Unexpected data in transaction log. Expected to get transaction separator but got truncated data. Tx #{txCount}");
			}

			Guid expectedValue, otherValue;
			PersistentQueueMarkers otherSeparator;
			if (whichSeparator == PersistentQueueMarkers.START)
			{
				expectedValue = PersistentQueueUtils.StartTransactionSeparatorGuid;
				otherValue = PersistentQueueUtils.EndTransactionSeparatorGuid;
				otherSeparator = PersistentQueueMarkers.END;
			}
			else if (whichSeparator == PersistentQueueMarkers.END)
			{
				expectedValue = PersistentQueueUtils.EndTransactionSeparatorGuid;
				otherValue = PersistentQueueUtils.StartTransactionSeparatorGuid;
				otherSeparator = PersistentQueueMarkers.START;
			}
			else throw new InvalidProgramException("Wrong kind of separator in inner implementation");

			Guid separator = new Guid(bytes);
			if (separator == expectedValue) return;
			if (separator == otherValue) // found a marker, but of the wrong type
			{
				throw new EndOfStreamException($"Unexpected data in transaction log. Expected {whichSeparator} but found {otherSeparator}");
			}
			throw new EndOfStreamException($"Unexpected data in transaction log. Expected to get transaction separator but got unknown data. Tx #{txCount}");
		}


		private void ReadMetaState()
		{
			PersistentQueueUtils.Read(Meta, stream =>
			{
				using (BinaryReader binaryReader = new BinaryReader(stream))
				{
					try
					{
						CurrentFileNumber = binaryReader.ReadInt32();
						CurrentFilePosition = binaryReader.ReadInt64();
					}
					catch (EndOfStreamException)
					{
					}
				}
			});
		}


		private void TrimTransactionLogIfNeeded(long txLogSize)
		{
			if (txLogSize < PersistentQueueUtils._32Megabytes) return; // it is not big enough to care

			long optimalSize = GetOptimalTransactionLogSize();
			if (txLogSize < (optimalSize * 2)) return; // not enough disparity to bother trimming

			FlushTrimmedTransactionLog();
		}

		private void ApplyTransactionOperations(IEnumerable<PersistentQueueOperation> operations)
		{
			IEnumerable<int> filesToRemove = ApplyTransactionOperationsInMemory(operations);

			foreach (int fileNumber in filesToRemove)
			{
				if (CurrentFileNumber == fileNumber)
					continue;

				File.Delete(GetDataPath(fileNumber));
			}
		}

		private static byte[] GenerateTransactionBuffer(ICollection<PersistentQueueOperation> operations)
		{
			byte[] transactionBuffer;
			using (MemoryStream ms = new MemoryStream())
			{
				ms.Write(PersistentQueueUtils.StartTransactionSeparator, 0, PersistentQueueUtils.StartTransactionSeparator.Length);

				byte[] count = BitConverter.GetBytes(operations.Count);
				ms.Write(count, 0, count.Length);

				foreach (PersistentQueueOperation operation in operations)
				{
					WriteEntryToTransactionLog(ms, new PersistentQueueEntry(operation), operation.Type);
				}

				ms.Write(PersistentQueueUtils.EndTransactionSeparator, 0, PersistentQueueUtils.EndTransactionSeparator.Length);

				ms.Flush();
				transactionBuffer = ms.ToArray();
			}

			return transactionBuffer;
		}

		private FileStream CreateWriter()
		{
			string dataFilePath = GetDataPath(CurrentFileNumber);
			FileStream stream = new FileStream(
				dataFilePath,
				FileMode.OpenOrCreate,
				FileAccess.Write,
				FileShare.ReadWrite,
				0x10000,
				FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);

			return stream;
		}

		private string GetDataPath(int index) => Path.Combine(_path, "data." + index);

		private long GetOptimalTransactionLogSize() => 16 + sizeof(int) + sizeof(int) * 4 * CurrentCountOfItemsInQueue;

		public void Clear()
		{
			using (PersistentQueueSession session = OpenSession())
			{
				while (session.Dequeue() != null) {}
				session.Flush();
			}
		}
	}
}
