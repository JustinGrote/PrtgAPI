using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace PrtgAPI.PowerShell
{
	/// <summary>
	/// Manages multiple PrtgClient connections within a PowerShell session.
	/// </summary>
	internal class PrtgClientManager
	{
		private readonly ConcurrentDictionary<string, PrtgClient> clients = new ConcurrentDictionary<string, PrtgClient>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// The default client to use when no specific client is specified.
		/// This is typically the first client connected or the most recently set default.
		/// </summary>
		public PrtgClient DefaultClient { get; set; }

		/// <summary>
		/// Gets all connected clients.
		/// </summary>
		public IEnumerable<PrtgClient> AllClients => clients.Values;

		/// <summary>
		/// Gets the number of connected clients.
		/// </summary>
		public int Count => clients.Count;

		/// <summary>
		/// Adds a client to the manager.
		/// </summary>
		/// <param name="client">The client to add.</param>
		/// <param name="setAsDefault">Whether to set this client as the default.</param>
		public void AddClient(PrtgClient client, bool setAsDefault = false)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));

			var key = GetClientKey(client.Server);

			if (clients.TryAdd(key, client))
			{
				if (setAsDefault || DefaultClient == null)
					DefaultClient = client;
			}
			else
			{
				throw new InvalidOperationException($"A client for server '{client.Server}' already exists. Use -Force to replace it.");
			}
		}

		/// <summary>
		/// Adds or updates a client in the manager.
		/// </summary>
		/// <param name="client">The client to add or update.</param>
		/// <param name="setAsDefault">Whether to set this client as the default.</param>
		public void AddOrUpdateClient(PrtgClient client, bool setAsDefault = false)
		{
			if (client == null)
				throw new ArgumentNullException(nameof(client));

			var key = GetClientKey(client.Server);
			clients[key] = client;

			if (setAsDefault || DefaultClient == null)
				DefaultClient = client;
		}

		/// <summary>
		/// Removes a client from the manager.
		/// </summary>
		/// <param name="server">The server URL of the client to remove.</param>
		/// <returns>True if the client was removed; otherwise, false.</returns>
		public bool RemoveClient(string server)
		{
			if (string.IsNullOrEmpty(server))
				throw new ArgumentNullException(nameof(server));

			var key = GetClientKey(server);

			if (clients.TryRemove(key, out var removedClient))
			{
				if (DefaultClient == removedClient)
				{
					DefaultClient = clients.Values.FirstOrDefault();
				}
				return true;
			}

			return false;
		}

		/// <summary>
		/// Removes all clients from the manager.
		/// </summary>
		public void Clear()
		{
			clients.Clear();
			DefaultClient = null;
		}

		/// <summary>
		/// Gets a client by server URL.
		/// </summary>
		/// <param name="server">The server URL.</param>
		/// <returns>The client if found; otherwise, null.</returns>
		public PrtgClient GetClient(string server)
		{
			if (string.IsNullOrEmpty(server))
				return DefaultClient;

			var key = GetClientKey(server);
			return clients.TryGetValue(key, out var client) ? client : null;
		}

		/// <summary>
		/// Gets multiple clients by server URLs.
		/// </summary>
		/// <param name="servers">The server URLs. If null or empty, returns all clients.</param>
		/// <returns>The matching clients.</returns>
		public IEnumerable<PrtgClient> GetClients(IEnumerable<string> servers)
		{
			if (servers == null || !servers.Any())
				return AllClients;

			return servers.Select(s => GetClient(s)).Where(c => c != null);
		}

		/// <summary>
		/// Checks if a client exists for the specified server.
		/// </summary>
		/// <param name="server">The server URL.</param>
		/// <returns>True if a client exists; otherwise, false.</returns>
		public bool HasClient(string server)
		{
			var key = GetClientKey(server);
			return clients.ContainsKey(key);
		}

		/// <summary>
		/// Normalizes a server URL to use as a dictionary key.
		/// </summary>
		private string GetClientKey(string server)
		{
			if (string.IsNullOrEmpty(server))
				throw new ArgumentNullException(nameof(server));

			// Normalize the URL by removing trailing slashes and converting to lowercase
			var normalized = server.TrimEnd('/').ToLowerInvariant();

			// If no protocol is specified, it will be HTTPS by default in PrtgClient
			// So we need to ensure consistent key generation
			if (!normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
					!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				normalized = "https://" + normalized;
			}

			return normalized;
		}

		/// <summary>
		/// Gets the client that an object originated from based on embedded metadata.
		/// </summary>
		/// <param name="obj">The object to check.</param>
		/// <returns>The client if found; otherwise, the default client.</returns>
		public PrtgClient GetClientFromObject(object obj)
		{
			if (obj == null)
				return DefaultClient;

			// First, check for PSObject server affinity metadata
			var affinityClient = obj.GetClientAffinity();
			if (affinityClient != null)
				return affinityClient;

			var serverUrl = obj.GetServerAffinity();
			if (!string.IsNullOrEmpty(serverUrl))
			{
				var client = GetClient(serverUrl);
				if (client != null)
					return client;
			}

			// If we can't determine the server, use the default client
			return DefaultClient;
		}
	}
}
