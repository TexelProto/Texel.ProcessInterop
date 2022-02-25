namespace Texel.ProcessInterop.Service.Python
{
	public class PhysicalPythonSource : IPythonSource
	{
		public string EntryPoint { get; }

		public PhysicalPythonSource(string entryPoint) {
			this.EntryPoint = entryPoint;
		}

		public string CreateEntryPoint()
		{
			return this.EntryPoint;
		}
	}
}