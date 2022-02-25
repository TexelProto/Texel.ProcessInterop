using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Texel.ProcessInterop.Master
{
	public class MessagingProcessWrapper : ProcessWrapper, IObservable<IMessage>
	{
		private struct MessageObserverHandle : IDisposable
		{
			private readonly MessagingProcessWrapper process;
			private readonly IObserver<IMessage> observer;
			private bool disposed;

			public MessageObserverHandle(MessagingProcessWrapper process, IObserver<IMessage> observer)
			{
				this.process = process;
				this.observer = observer;
				this.disposed = false;
			}

			public void Bind()
			{
				this.process.Next += this.observer.OnNext;
				this.process.Error += this.observer.OnError;
				this.process.Completed += this.observer.OnCompleted;
			}

			public void Dispose()
			{
				if (this.disposed)
					return;
				this.disposed = true;

				this.process.Next -= this.observer.OnNext;
				this.process.Error -= this.observer.OnError;
				this.process.Completed -= this.observer.OnCompleted;
			}
		}

		private readonly JsonSerializer serializer;

		private event Action<IMessage>? Next;
		private event Action<Exception>? Error;
		private event Action? Completed;

		public MessagingProcessWrapper(ProcessStartInfo processStartInfo)
			: this( processStartInfo, null ) { }

		public MessagingProcessWrapper(ProcessStartInfo processStartInfo, JsonSerializerSettings? settings)
			: base( processStartInfo )
		{
			this.serializer = JsonSerializer.CreateDefault( settings );
		}

		public void Send(IMessage message)
		{
			this.ThrowIfDisposed();

			if (this.IsRunning == false)
				throw new InvalidOperationException( "Attempted to send message to a process before starting it" );

			var stream = this.StandardInput;
			lock (stream)
			{
				using var writer = new JsonTextWriter( stream ) { CloseOutput = false };
				this.serializer.Serialize( writer, message );
				writer.Flush();

				stream.WriteLine();
				stream.Flush();
			}
		}

		protected override void OnPublish(string line)
		{
			var message = MessageParser.Parse( line );
			this.Next?.Invoke( message );
		}

		protected override void OnError(Exception exception)
		{
			this.Error?.Invoke( exception );
		}

		protected override void OnCompleted()
		{
			this.Completed?.Invoke();
		}

		public IDisposable Subscribe(IObserver<IMessage> observer)
		{
			this.ThrowIfDisposed();
			var handle = new MessageObserverHandle( this, observer );
			handle.Bind();
			return handle;
		}
	}

	public class ProcessWrapper : IObservable<string>, IDisposable
	{
		#region SubClasses

		private struct StringObserverHandle : IDisposable
		{
			private readonly ProcessWrapper process;
			private readonly IObserver<string> observer;
			private bool disposed;

			public StringObserverHandle(ProcessWrapper process, IObserver<string> observer)
			{
				this.process = process;
				this.observer = observer;
				this.disposed = false;
			}

			public void Bind()
			{
				this.process.Next += this.observer.OnNext;
				this.process.Error += this.observer.OnError;
				this.process.Completed += this.observer.OnCompleted;
			}

			public void Dispose()
			{
				if (this.disposed)
					return;
				this.disposed = true;

				this.process.Next -= this.observer.OnNext;
				this.process.Error -= this.observer.OnError;
				this.process.Completed -= this.observer.OnCompleted;
			}
		}

		#endregion

		private readonly BlockingCollection<string> messageCollection = new();
		private readonly Process process;
		private readonly Thread publishThread;
		private Task? observeTask;

		protected StreamWriter StandardInput => this.process.StandardInput;

		public bool IsRunning => this.observeTask != null;
		public bool IsDisposed { get; private set; }
		public bool IsCompleted { get; private set; }

		private event Action<string>? Next;
		private event Action<Exception>? Error;
		private event Action? Completed;

		public ProcessWrapper(ProcessStartInfo processStartInfo)
		{
			processStartInfo.RedirectStandardError = true;
			processStartInfo.RedirectStandardInput = true;
			processStartInfo.RedirectStandardOutput = true;

			this.process = new Process
			{
				StartInfo = processStartInfo,
				EnableRaisingEvents = true
			};

			this.publishThread = new Thread( this.PublishAsync );
		}

		#region Send

		public void Send(string str)
		{
			var stream = this.process.StandardInput;
			stream.WriteLine( str );
			stream.Flush();
		}

		#endregion

		#region Process Observation

		public void Start()
		{
			this.ThrowIfDisposed();

			if (this.observeTask != null)
				return;

			this.StartAndObserveInternal();
		}

		private void StartAndObserveInternal()
		{
			if (this.process.Start() == false)
				throw new InvalidOperationException( "Failed to start process" );

			var outputTask = this.ObserveOutputAsync();
			var errorTask = this.ObserveErrorAsync();
			this.observeTask = Task.WhenAll( outputTask, errorTask );

			this.publishThread.Start();

			this.process.Exited += (_, _) =>
			{
				this.FinalizeMessages();
			};
		}

		private void PublishAsync()
		{
			var enumerable = this.messageCollection.GetConsumingEnumerable();
			foreach (var message in enumerable)
			{
				this.Next?.Invoke( message );
				this.OnPublish( message );
			}
		}

		protected virtual void OnPublish(string line) { }

		private async Task ObserveOutputAsync()
		{
			var stream = this.process.StandardOutput;

			do
			{
				var line = await stream.ReadLineAsync();
				if (string.IsNullOrEmpty( line ))
					continue;

				this.messageCollection.TryAdd( line );
			} while (this.process.HasExited == false);
		}

		private async Task ObserveErrorAsync()
		{
			do
			{
				string? error = await this.process.StandardError.ReadLineAsync();

				if (string.IsNullOrEmpty( error ))
					return;

				var exception = new Exception( error );
				this.Error?.Invoke( exception );
				this.OnError( exception );
			} while (this.process.HasExited == false);
		}

		protected virtual void OnError(Exception exception) { }

		private void FinalizeMessages()
		{
			if (this.IsCompleted)
				return;
			this.IsCompleted = true;

			if (this.process.HasExited == false)
			{
				this.process.Close();
				this.process.WaitForExit();
			}

			this.messageCollection.CompleteAdding();

			this.publishThread.Join();
			this.messageCollection.Dispose();
			this.Completed?.Invoke();
			this.OnCompleted();
		}

		protected virtual void OnCompleted() { }

		#endregion

		#region IObservable

		public IDisposable Subscribe(IObserver<string> observer)
		{
			this.ThrowIfDisposed();
			var handle = new StringObserverHandle( this, observer );
			handle.Bind();
			return handle;
		}

		#endregion

		#region IDisposable

		protected void ThrowIfDisposed()
		{
			if (this.IsDisposed)
				throw new ObjectDisposedException( nameof(ProcessWrapper) );
		}

		public void Dispose()
		{
			if (this.IsDisposed)
				return;

			this.IsDisposed = true;
			this.FinalizeMessages();
		}

		#endregion
	}
}