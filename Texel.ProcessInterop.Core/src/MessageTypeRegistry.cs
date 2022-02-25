using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Texel.ProcessInterop
{
	public static class MessageTypeRegistry
	{
		private static readonly Dictionary<string, Type> typeIdLookup;

		static MessageTypeRegistry()
		{
			typeIdLookup = AppDomain.CurrentDomain
			                        .GetAssemblies()
			                        .SelectMany( a => a.GetTypes() )
			                        .Where( IsValidType )
			                        .ToDictionary( GetTypeId, t => t );
		}

		private static bool IsValidType(Type type)
		{
			if (type.IsAbstract || type.IsInterface || type.IsGenericType)
				return false;

			if (typeof(IMessage).IsAssignableFrom( type ) == false)
				return false;

			if (type.IsDefined( typeof(MessageIdAttribute) ) == false)
				return false;

			return true;
		}

		private static string GetTypeId(Type type)
		{
			return type.GetCustomAttribute<MessageIdAttribute>()!.Id;
		}

		public static Type? GetType(string id)
		{
			if (typeIdLookup.TryGetValue( id, out var type ))
				return type;

			return null;
		}
	}
}