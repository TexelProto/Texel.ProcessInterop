using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Texel.ProcessInterop
{
	public static class MessageParser
	{
		private static readonly Regex idPropertyPattern = new("\"Id\":\"(.*?[^\\])?\"",
		                                                      RegexOptions.IgnoreCase | 
		                                                      RegexOptions.Singleline | 
		                                                      RegexOptions.Compiled );

		private static readonly JsonSerializer serializer = JsonSerializer.CreateDefault();

		public static IMessage Parse(string line)
		{
			var match = idPropertyPattern.Match( line );
			if (match.Success == false)
				throw new Exception( $"Received malformed message '{line}'" );

			string id = match.Groups[1].Value;
			var type = MessageTypeRegistry.GetType( id );

			if (type == null)
				throw new Exception( $"Failed to find message type for {id}" );
			
			using var reader = new StringReader( line );
			object result = serializer.Deserialize( reader, type )!;
			return (IMessage)result;
		}

		public static string Stringify(IMessage message)
		{
			using var writer = new StringWriter();
			serializer.Serialize( writer, message );
			return writer.ToString();
		}
	}
}