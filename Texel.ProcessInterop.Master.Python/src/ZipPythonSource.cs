using System;
using System.IO;
using System.IO.Compression;

namespace Texel.ProcessInterop.Service.Python
{
	public class ZipPythonSource : IPythonSource
	{
		public string ArchivePath { get; }
		public string EntryFile { get; }

		private static readonly Random rng = new();

		public ZipPythonSource(string archivePath, string entryFile)
		{
			this.ArchivePath = archivePath;
			this.EntryFile = entryFile;
		}
		
		private static string GetRandomName(int length)
		{
			var chars = new char[length];
			for (int i = 0; i < length; i++)
			{
				chars[i] = rng.Next( 0, 3 ) switch
				{
					0 => (char)( 'A' + rng.Next( 0, 26 ) ),
					1 => (char)( 'a' + rng.Next( 0, 26 ) ),
					2 => (char)( '0' + rng.Next( 0, 10 ) ),
					_ => throw new Exception()
				};
			}

			return new string( chars );
		}

		public string CreateEntryPoint()
		{
			if (File.Exists( this.ArchivePath ) == false)
				throw new IOException( "Failed to find the archive file" );

			using var file = File.OpenRead( this.ArchivePath );
			using var archive = new ZipArchive( file, ZipArchiveMode.Read );

			if (archive.GetEntry( this.EntryFile ) == null)
				throw new IOException( "Entry file was not present in the archive" );

			string tempDir;
			do
			{
				tempDir = Path.Combine( Path.GetTempPath(), GetRandomName( 28 ) );
			} while (Directory.Exists( tempDir ));

			archive.ExtractToDirectory( tempDir );
			var entryPath = Path.Combine( tempDir, this.EntryFile );

			if (File.Exists( entryPath ) == false)
				throw new IOException( "Failed to find the the extracted entry file" );

			return entryPath;
		}
	}
}