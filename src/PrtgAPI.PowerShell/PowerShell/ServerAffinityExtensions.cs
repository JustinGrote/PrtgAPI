using System.Management.Automation;

namespace PrtgAPI.PowerShell
{
	/// <summary>
	/// Provides extension methods for tagging objects with server affinity information.
	/// </summary>
	internal static class ServerAffinityExtensions
	{
		private const string ServerPropertyName = "PSPrtgServer";
		private const string ClientPropertyName = "PSPrtgClient";

		/// <summary>
		/// Tags a PSObject with the server it originated from.
		/// </summary>
		/// <param name="psObject">The PSObject to tag.</param>
		/// <param name="client">The client that produced this object.</param>
		public static void TagWithServer(this PSObject psObject, PrtgClient client)
		{
			if (psObject == null || client == null)
				return;

			// Add properties to track the source server
			var serverProperty = new PSNoteProperty(ServerPropertyName, client.Server);
			var clientProperty = new PSNoteProperty(ClientPropertyName, client);

			// Remove existing properties if present
			psObject.Properties.Remove(ServerPropertyName);
			psObject.Properties.Remove(ClientPropertyName);

			psObject.Properties.Add(serverProperty);
			psObject.Properties.Add(clientProperty);
		}

		/// <summary>
		/// Gets the server URL that an object originated from.
		/// </summary>
		/// <param name="obj">The object to check.</param>
		/// <returns>The server URL if found; otherwise, null.</returns>
		public static string GetServerAffinity(this object obj)
		{
			if (obj == null)
				return null;

			var psObject = obj as PSObject ?? PSObject.AsPSObject(obj);
			var serverProperty = psObject.Properties[ServerPropertyName];

			return serverProperty?.Value as string;
		}

		/// <summary>
		/// Gets the PrtgClient that an object originated from.
		/// </summary>
		/// <param name="obj">The object to check.</param>
		/// <returns>The PrtgClient if found; otherwise, null.</returns>
		public static PrtgClient GetClientAffinity(this object obj)
		{
			if (obj == null)
				return null;

			var psObject = obj as PSObject ?? PSObject.AsPSObject(obj);
			var clientProperty = psObject.Properties[ClientPropertyName];

			return clientProperty?.Value as PrtgClient;
		}
	}
}
