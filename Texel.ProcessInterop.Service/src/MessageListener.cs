using System;
using System.Threading;

namespace Texel.ProcessInterop.Service
{
	public static class MessageListener
	{
		public static event Action<IMessage>? ReceivedMessage;

		static MessageListener()
		{
			var thread = new Thread( Run );
			thread.Start();
		}

		private static void Run()
		{
			while (true)
			{
				string? line = Console.ReadLine();
				if (string.IsNullOrEmpty( line ))
					continue;

				var message = MessageParser.Parse( line );
				ReceivedMessage?.Invoke( message );
			}
		}
	}
}