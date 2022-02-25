using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Texel.ProcessInterop.Master;

namespace Texel.ProcessInterop.Service.Python
{
	public class PythonProcessWrapper : MessagingProcessWrapper
	{
		private static ProcessStartInfo CreateProcessStartInfo(IPythonSource source)
		{
			var entryPath = source.CreateEntryPoint();
			// use the -u flag to prevent buffering print output
			return new ProcessStartInfo( "python", "-u " + entryPath );
		}

		public PythonProcessWrapper(IPythonSource source)
			: base( CreateProcessStartInfo( source ) ) { }

		public PythonProcessWrapper(IPythonSource source, JsonSerializerSettings serializerSettings)
			: base( CreateProcessStartInfo( source ), serializerSettings ) { }
	}
}