using System;

namespace Texel.ProcessInterop.Service
{
	public static class MessageSender
	{
		public static void Send(IMessage message)
		{
			var str = MessageParser.Stringify( message );
			Console.WriteLine( str );
		}
	}
}