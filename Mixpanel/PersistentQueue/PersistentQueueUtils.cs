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
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace mixpanel.queue
{
	/// <summary>
	/// Exception thrown when data can't be persisted
	/// </summary>
	[Serializable]
	public class PendingWriteException : Exception
	{
		private readonly Exception[] _pendingWritesExceptions;
		
		/// <summary>
		/// Aggregate causing exceptions
		/// </summary>
		public PendingWriteException(Exception[] pendingWritesExceptions) : base("Error during pending writes") => this._pendingWritesExceptions = pendingWritesExceptions;

		/// <summary>
		/// Gets a message that describes the current exception.
		/// </summary>
		public override string Message
		{
			get
			{
				StringBuilder sb = new StringBuilder(base.Message).Append(":");
				foreach (Exception exception in _pendingWritesExceptions)
				{
					sb.AppendLine().Append(" - ").Append(exception.Message);
				}
				return sb.ToString();
			}
		}

		/// <summary>
		/// Creates and returns a string representation of the current exception.
		/// </summary>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(base.Message).Append(":");
			foreach (Exception exception in _pendingWritesExceptions)
			{
				sb.AppendLine().Append(" - ").Append(exception);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Sets the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with information about the exception.
		/// </summary>
		/// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown. </param><param name="context">The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination. </param><exception cref="T:System.ArgumentNullException">The <paramref name="info"/> parameter is a null reference (Nothing in Visual Basic). </exception><filterpriority>2</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="*AllFiles*" PathDiscovery="*AllFiles*"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="SerializationFormatter"/></PermissionSet>
		[SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("PendingWritesExceptions", _pendingWritesExceptions);
		}
	}
		
	/// <summary>
	/// List of marker constants
	/// </summary>
	public enum PersistentQueueMarkers
	{
		START = 0,
		END = -1
	}
		
    internal static class PersistentQueueUtils
    {
	    /// <summary> Operation end marker </summary>
	    public const int OperationSeparator = 0x42FEBCA1;

	    /// <summary> Bytes of operation end marker </summary>
	    public static readonly byte[] OperationSeparatorBytes = BitConverter.GetBytes(OperationSeparator);

	    /// <summary> Start of transaction marker </summary>
	    /// <remarks>If this is ever changed, existing queue files will be unreadable</remarks>
	    public static readonly Guid StartTransactionSeparatorGuid = new Guid("b75bfb12-93bb-42b6-acb1-a897239ea3a5");

	    /// <summary> Bytes of the start of transaction marker </summary>
	    public static readonly byte[] StartTransactionSeparator = StartTransactionSeparatorGuid.ToByteArray();

	    /// <summary> End of transaction marker </summary>
	    /// <remarks>If this is ever changed, existing queue files will be unreadable</remarks>
	    public static readonly Guid EndTransactionSeparatorGuid = new Guid("866c9705-4456-4e9d-b452-3146b3bfa4ce");

	    /// <summary> Bytes of end of transaction marker </summary>
	    public static readonly byte[] EndTransactionSeparator = EndTransactionSeparatorGuid.ToByteArray();
	    
	    /// <summary> Minimum amount of bytes that should be done in an async write</summary>
	    public const int MinSizeThatMakeAsyncWritePractical = 64 * 1024;

	    /// <summary> 16MiB in bytes </summary>
	    public const int _16Megabytes = 16 * 1024 * 1024;
	    
	    /// <summary> 32MiB in bytes </summary>
	    public const int _32Megabytes = 32 * 1024 * 1024;
	    
	    /// <summary> 1MiB in bytes </summary>
	    public const int _1Megabytes = 1042 * 1024;
	    
	    private static readonly object Lock = new object();
	    
	    private static FileStream CreateFileStream(string path, FileMode mode, FileAccess access, FileOptions options) =>
		    new FileStream(path, mode, access, FileShare.None, 0x10000, options);

	    private static FileStream CreateReadStream(string path) => 
		    CreateFileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileOptions.SequentialScan);

	    private static FileStream CreateWriteStream(string path) => 
		    CreateFileStream(path, FileMode.Create, FileAccess.Write, FileOptions.WriteThrough | FileOptions.SequentialScan);
		
		internal static void Read(string path, Action<Stream> action)
		{
			lock (Lock)
			{
				if (File.Exists(path + ".old_copy"))
				{
					if (WaitDelete(path))
						File.Move(path + ".old_copy", path);
				}

				using (FileStream stream = CreateReadStream(path))
				{
					action(stream);
				}
			}
		}
		
		internal static void Write(string path, Action<Stream> action)
		{
			lock (Lock)
			{
				// if the old copy file exists, this means that we have
				// a previous corrupt write, so we will not overwrite it, but 
				// rather overwrite the current file and keep it as our backup.
				if (File.Exists(path + ".old_copy") == false)
					File.Move(path, path + ".old_copy");

				using (FileStream stream = CreateWriteStream(path))
				{
					action(stream);
					stream.Flush();
				}
				
				WaitDelete(path + ".old_copy");
			}
		}

		private static bool WaitDelete(string s)
		{
			for (int i = 0; i < 5; i++)
			{
				try
				{
					File.Delete(s);
					return true;
				}
				catch
				{
					Thread.Sleep(100);
				}
			}
			return false;
		}
    }
}
