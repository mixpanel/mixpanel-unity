using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace mixpanel.queue
{
    public class PersistentQueueSession : IDisposable
    {
	    private static readonly object CtorLock = new object();

	    private readonly List<PersistentQueueOperation> _operations = new List<PersistentQueueOperation>();
		private readonly IList<Exception> _pendingWritesFailures = new List<Exception>();
		private readonly IList<WaitHandle> _pendingWritesHandles = new List<WaitHandle>();
		private readonly PersistentQueue _queue;
		private readonly List<Stream> _streamsToDisposeOnFlush = new List<Stream>();
		private readonly List<byte[]> _buffer = new List<byte[]>();
		private volatile bool _disposed;
		private Stream _currentStream;
		private int _bufferSize;

		/// <summary>
		/// Create a default persistent queue session.
		/// <para>You should use <see cref="PersistentQueue.OpenSession"/> to get a session.</para>
		/// <example>using (var q = PersistentQueue.WaitFor("myQueue")) using (var session = q.OpenSession()) { ... }</example>
		/// </summary>
		public PersistentQueueSession(PersistentQueue queue, Stream currentStream)
		{
			lock (CtorLock)
			{
				_queue = queue;
				_currentStream = currentStream;
				_disposed = false;
			}
		}
		
		/// <summary>
		/// Dispose queue on destructor. This is a safety-valve. You should ensure you
		/// dispose of sessions normally.
		/// </summary>
		~PersistentQueueSession()
		{
			if (_disposed) return;
			Dispose();
		}

		/// <summary>
		/// Queue data for a later decode. Data is written on `Flush()`
		/// </summary>
		public void Enqueue(byte[] data)
		{
			_buffer.Add(data);
			_bufferSize += data.Length;
			if (_bufferSize > PersistentQueueUtils.MinSizeThatMakeAsyncWritePractical)
			{
				AsyncFlushBuffer();
			}
		}

		private void AsyncFlushBuffer()
		{
			_queue.AcquireWriter(_currentStream, AsyncWriteToStream, OnReplaceStream);
		}

		private void SyncFlushBuffer()
		{
			_queue.AcquireWriter(_currentStream, stream =>
			{
				byte[] data = ConcatenateBufferAndAddIndividualOperations(stream);
				stream.Write(data, 0, data.Length);
				return stream.Position;
			}, OnReplaceStream);
		}

		private long AsyncWriteToStream(Stream stream)
		{
			byte[] data = ConcatenateBufferAndAddIndividualOperations(stream);
			ManualResetEvent resetEvent = new ManualResetEvent(false);
			_pendingWritesHandles.Add(resetEvent);
			long positionAfterWrite = stream.Position + data.Length;
			stream.BeginWrite(data, 0, data.Length, delegate(IAsyncResult ar)
			{
				try
				{
					stream.EndWrite(ar);
				}
				catch (Exception e)
				{
					lock (_pendingWritesFailures)
					{
						_pendingWritesFailures.Add(e);
					}
				}
				finally
				{
					resetEvent.Set();
				}
			}, null);
			return positionAfterWrite;
		}

		private byte[] ConcatenateBufferAndAddIndividualOperations(Stream stream)
		{
			byte[] data = new byte[_bufferSize];
			int start = (int)stream.Position;
			int index = 0;
			foreach (byte[] bytes in _buffer)
			{
				_operations.Add(new PersistentQueueOperation(
					PersistentQueueOperationTypes.ENQUEUE,
					_queue.CurrentFileNumber,
					start,
					bytes.Length
				));
				Buffer.BlockCopy(bytes, 0, data, index, bytes.Length);
				start += bytes.Length;
				index += bytes.Length;
			}
			_bufferSize = 0;
			_buffer.Clear();
			return data;
		}

		private void OnReplaceStream(Stream newStream)
		{
			_streamsToDisposeOnFlush.Add(_currentStream);
			_currentStream = newStream;
		}

		/// <summary>
		/// Try to pull data from the queue. Data is removed from the queue on `Flush()`
		/// </summary>
		public byte[] Dequeue()
		{
			PersistentQueueEntry entry = _queue.Dequeue();
			if (entry == null)
				return null;
			_operations.Add(new PersistentQueueOperation(
				PersistentQueueOperationTypes.DEQUEUE,
				entry.FileNumber,
				entry.Start,
				entry.Length
			));
			return entry.Data;
		}

		/// <summary>
		/// Commit actions taken in this session since last flush.
		/// If the session is disposed with no flush, actions are not persisted 
		/// to the queue (Enqueues are not written, dequeues are left on the queue)
		/// </summary>
		public void Flush()
		{
			try
			{
				WaitForPendingWrites();
				SyncFlushBuffer();
			}
			finally
			{
				foreach (Stream stream in _streamsToDisposeOnFlush)
				{
					stream.Flush();
					stream.Dispose();
				}
				_streamsToDisposeOnFlush.Clear();
			}
			_currentStream.Flush();
			_queue.CommitTransaction(_operations);
			_operations.Clear();
		}

		private void WaitForPendingWrites()
		{
			while (_pendingWritesHandles.Count != 0)
			{
				WaitHandle[] handles = _pendingWritesHandles.Take(64).ToArray();
				foreach (WaitHandle handle in handles)
				{
					_pendingWritesHandles.Remove(handle);
				}
				WaitHandle.WaitAll(handles);
				foreach (WaitHandle handle in handles)
				{
					handle.Close();
				}
				AssertNoPendingWritesFailures();
			}
		}

		private void AssertNoPendingWritesFailures()
		{
			lock (_pendingWritesFailures)
			{
				if (_pendingWritesFailures.Count == 0)
					return;

				Exception[] array = _pendingWritesFailures.ToArray();
				_pendingWritesFailures.Clear();
				throw new PendingWriteException(array);
			}
		}

		/// <summary>
		/// Close session, restoring any non-flushed operations
		/// </summary>
		public void Dispose()
		{
			lock (CtorLock)
			{
				if (_disposed) return;
				_disposed = true;
				_queue.Reinstate(_operations);
				_operations.Clear();
				foreach (Stream stream in _streamsToDisposeOnFlush)
				{
					stream.Dispose();
				}
				_currentStream.Dispose();
				GC.SuppressFinalize(this);
			}
			Thread.Sleep(0);
		}
    }
}
