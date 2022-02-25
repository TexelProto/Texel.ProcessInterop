using System;

namespace Texel.ProcessInterop
{
	[AttributeUsage( AttributeTargets.Class, Inherited = false )]
	public sealed class MessageIdAttribute : Attribute
	{
		public string Id { get; }

		public MessageIdAttribute(string id)
		{
			this.Id = id;
		}
	}
}