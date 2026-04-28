using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PrtgAPI.PowerShell.Base
{
	/// <summary>
	/// Provides functionality for executing operations against multiple PRTG servers in parallel.
	/// </summary>
	internal class MultiClientExecutor
	{
		/// <summary>
		/// Executes a function against multiple clients in parallel and returns combined results.
		/// </summary>
		/// <typeparam name="T">The type of result returned by each operation.</typeparam>
		/// <param name="clients">The clients to execute against.</param>
		/// <param name="operation">The operation to execute for each client.</param>
		/// <returns>Combined results from all clients.</returns>
		public static IEnumerable<T> ExecuteInParallel<T>(IEnumerable<PrtgClient> clients, Func<PrtgClient, IEnumerable<T>> operation)
		{
			var clientList = clients.ToList();

			if (clientList.Count == 0)
				return Enumerable.Empty<T>();

			if (clientList.Count == 1)
			{
				// Single client - execute synchronously
				return operation(clientList[0]);
			}

			// Multiple clients - execute in parallel
			var tasks = clientList.Select(client => Task.Run(() => operation(client).ToList())).ToArray();

			Task.WaitAll(tasks);

			return tasks.SelectMany(t => t.Result);
		}

		/// <summary>
		/// Executes a function against multiple clients in parallel and returns results tagged with their source client.
		/// </summary>
		/// <typeparam name="T">The type of result returned by each operation.</typeparam>
		/// <param name="clients">The clients to execute against.</param>
		/// <param name="operation">The operation to execute for each client.</param>
		/// <returns>Results from all clients with their source server information.</returns>
		public static IEnumerable<ClientResult<T>> ExecuteInParallelWithSource<T>(IEnumerable<PrtgClient> clients, Func<PrtgClient, IEnumerable<T>> operation)
		{
			var clientList = clients.ToList();

			if (clientList.Count == 0)
				return Enumerable.Empty<ClientResult<T>>();

			if (clientList.Count == 1)
			{
				// Single client - execute synchronously
				var client = clientList[0];
				return operation(client).Select(result => new ClientResult<T>(result, client));
			}

			// Multiple clients - execute in parallel
			var tasks = clientList.Select(client =>
					Task.Run(() => new
					{
						Client = client,
						Results = operation(client).ToList()
					})
			).ToArray();

			Task.WaitAll(tasks);

			return tasks.SelectMany(t =>
					t.Result.Results.Select(result => new ClientResult<T>(result, t.Result.Client))
			);
		}

		/// <summary>
		/// Executes an action against multiple clients in parallel.
		/// </summary>
		/// <param name="clients">The clients to execute against.</param>
		/// <param name="action">The action to execute for each client.</param>
		public static void ExecuteInParallel(IEnumerable<PrtgClient> clients, Action<PrtgClient> action)
		{
			var clientList = clients.ToList();

			if (clientList.Count == 0)
				return;

			if (clientList.Count == 1)
			{
				// Single client - execute synchronously
				action(clientList[0]);
				return;
			}

			// Multiple clients - execute in parallel
			var tasks = clientList.Select(client => Task.Run(() => action(client))).ToArray();

			Task.WaitAll(tasks);
		}
	}

	/// <summary>
	/// Represents a result from a multi-client operation along with its source client.
	/// </summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	internal class ClientResult<T>
	{
		/// <summary>
		/// The result of the operation.
		/// </summary>
		public T Result { get; }

		/// <summary>
		/// The client that produced this result.
		/// </summary>
		public PrtgClient Client { get; }

		/// <summary>
		/// The server URL of the client that produced this result.
		/// </summary>
		public string Server => Client.Server;

		public ClientResult(T result, PrtgClient client)
		{
			Result = result;
			Client = client;
		}
	}
}
