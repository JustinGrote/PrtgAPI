using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using PrtgAPI.PowerShell.Progress;
using PrtgAPI.Reflection;

namespace PrtgAPI.PowerShell.Base
{
    /// <summary>
    /// Base class for all cmdlets requiring authenticated access to a PRTG Server.
    /// </summary>
    public abstract class PrtgCmdlet : PSCmdlet, IDisposable
    {
        private readonly int owningThreadId = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// <para type="description">Specifies one or more PrtgClient instances or server URLs to execute the command against.
        /// If not specified, the command will execute against all connected servers. If an object is piped from another
        /// PrtgAPI cmdlet, the command will automatically target the server that object originated from.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        public object[] Client { get; set; }

        /// <summary>
        /// The current client being used for operations. Used for tagging output objects.
        /// </summary>
        private PrtgClient currentClient;

        /// <summary>
        /// Provides access to the <see cref="PrtgClient"/> stored in the current PowerShell Session State.
        /// This returns the default client for single-client operations. If a specific client has been
        /// selected for the current operation, that client will be returned.
        /// </summary>
        protected PrtgClient client
        {
            get
            {
                if (currentClient != null)
                    return currentClient;

                var targetClient = GetTargetClient();
                currentClient = targetClient;
                return targetClient;
            }
        }

        /// <summary>
        /// Executes an operation across all target clients and aggregates the results.
        /// If a single client is targeted, executes once.
        /// </summary>
        /// <typeparam name="T">The type of object returned by the operation.</typeparam>
        /// <param name="operation">The operation to execute per client.</param>
        /// <returns>An aggregated enumeration of results across all clients.</returns>
        protected IEnumerable<T> ForEachClient<T>(Func<IEnumerable<T>> operation)
        {
            var targets = GetTargetClients().ToList();

            if (targets.Count == 1)
            {
                currentClient = targets[0];
                return operation() ?? Enumerable.Empty<T>();
            }

            var results = new List<T>();

            foreach (var c in targets)
            {
                currentClient = c;
                var part = operation();
                if (part != null)
                    results.AddRange(part);
            }

            return results;
        }

        /// <summary>
        /// Gets all clients that should be targeted by this cmdlet based on the Client parameter
        /// and any pipeline input affinity.
        /// </summary>
        protected IEnumerable<PrtgClient> GetTargetClients()
        {
            // If Client parameter is specified, resolve those clients
            if (Client != null && Client.Length > 0)
            {
                var clients = new List<PrtgClient>();

                foreach (var item in Client)
                {
                    if (item is PrtgClient prtgClient)
                    {
                        clients.Add(prtgClient);
                    }
                    else if (item is string serverUrl)
                    {
                        var client = PrtgSessionState.ClientManager.GetClient(serverUrl);
                        if (client != null)
                            clients.Add(client);
                        else
                            throw new InvalidOperationException($"No connection found for server '{serverUrl}'");
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid Client parameter type. Expected PrtgClient or String, got {item?.GetType().Name ?? "null"}");
                    }
                }

                return clients;
            }

            // Check if we're processing pipeline input with server affinity
            var pipelineObject = GetPipelineInputObject();
            if (pipelineObject != null)
            {
                var affinityClient = PrtgSessionState.ClientManager.GetClientFromObject(pipelineObject);
                if (affinityClient != null)
                    return new[] { affinityClient };
            }

            // Return all connected clients for parallel execution
            var allClients = PrtgSessionState.ClientManager.AllClients.ToList();
            if (allClients.Count == 0)
                throw new InvalidOperationException("You are not connected to a PRTG Server. Please connect first using Connect-PrtgServer.");

            return allClients;
        }

        /// <summary>
        /// Gets the target client for single-client operations. If multiple clients are targeted,
        /// returns the default client.
        /// </summary>
        private PrtgClient GetTargetClient()
        {
            var targetClients = GetTargetClients().ToList();
            return targetClients.Count == 1 ? targetClients[0] : PrtgSessionState.Client;
        }

        /// <summary>
        /// The current pipeline input object being processed.
        /// </summary>
        private object currentPipelineObject;

        /// <summary>
        /// Attempts to get the current pipeline input object to check for server affinity.
        /// </summary>
        private object GetPipelineInputObject()
        {
            return currentPipelineObject;
        }

        /// <summary>
        /// Sets the current pipeline input object for server affinity tracking.
        /// </summary>
        /// <param name="inputObject">The object being processed from the pipeline.</param>
        protected void SetPipelineInputObject(object inputObject)
        {
            currentPipelineObject = inputObject;

            // If we have a pipeline object with server affinity, update the current client
            if (inputObject != null && Client == null)
            {
                var affinityClient = inputObject.GetClientAffinity();
                if (affinityClient != null)
                    currentClient = affinityClient;
            }
        }

        /// <summary>
        /// Writes an object to the pipeline, tagging it with server affinity if available.
        /// </summary>
        /// <param name="sendToPipeline">The object to write to the pipeline.</param>
        public new void WriteObject(object sendToPipeline)
        {
            if (sendToPipeline != null && currentClient != null)
            {
                var psObject = PSObject.AsPSObject(sendToPipeline);
                psObject.TagWithServer(currentClient);
            }

            base.WriteObject(sendToPipeline);
        }

        /// <summary>
        /// Writes objects to the pipeline, tagging them with server affinity if available.
        /// </summary>
        /// <param name="sendToPipeline">The objects to write to the pipeline.</param>
        /// <param name="enumerateCollection">Whether to enumerate the collection.</param>
        public new void WriteObject(object sendToPipeline, bool enumerateCollection)
        {
            if (sendToPipeline != null && currentClient != null && enumerateCollection)
            {
                // Tag each object in the collection
                var enumerable = sendToPipeline as System.Collections.IEnumerable;
                if (enumerable != null)
                {
                    foreach (var item in enumerable)
                    {
                        if (item != null)
                        {
                            var psObject = PSObject.AsPSObject(item);
                            psObject.TagWithServer(currentClient);
                        }
                    }
                }
            }
            else if (sendToPipeline != null && currentClient != null)
            {
                var psObject = PSObject.AsPSObject(sendToPipeline);
                psObject.TagWithServer(currentClient);
            }

            base.WriteObject(sendToPipeline, enumerateCollection);
        }

        private bool noClient;

        internal ProgressManager ProgressManager;

        internal ProgressManagerEx ProgressManagerEx = new ProgressManagerEx();

        private EventManager eventManager = new EventManager();

        private bool disposed;

        /// <summary>
        /// A cancellation token source to use with long running tasks that may need to be interrupted by Ctrl+C.
        /// </summary>
        private readonly CancellationTokenSource TokenSource = new CancellationTokenSource();

        internal CancellationToken CancellationToken => TokenSource.Token;

        internal bool HasParameter(string name) => MyInvocation.BoundParameters.ContainsKey(name);

        /// <summary>
        /// Provides a one-time, preprocessing functionality for the cmdlet.
        /// </summary>
        protected override void BeginProcessing()
        {
            if (PrtgSessionState.Client == null)
                throw new InvalidOperationException("You are not connected to a PRTG Server. Please connect first using Connect-PrtgServer.");

            BeginProcessingEx();
        }

        /// <summary>
        /// Provides an enhanced one-time, preprocessing functionality for the cmdlet.
        /// </summary>
        protected virtual void BeginProcessingEx()
        {
        }

        /// <summary>
        /// Performs record-by-record processing for the cmdlet. Do not override this method; override <see cref="ProcessRecordEx"/> instead.
        /// </summary>
        protected override void ProcessRecord()
        {
            ExecuteWithCoreState(ProcessRecordEx);
        }

        internal void ExecuteWithCoreState(Action action)
        {
            RegisterEvents();

            try
            {
                if (client != null)
                    client.DefaultCancellationToken = TokenSource.Token;

                using (ProgressManager = new ProgressManager(this))
                {
                    try
                    {
                        action();
                    }
                    catch (NonTerminatingException ex)
                    {
                        ProgressManager.CompleteUncompleted(true);
                        WriteInvalidOperation(ex.InnerException, ex.TargetObject, ex.ErrorCategory);
                    }
                    catch (PrtgRequestException ex)
                    {
                        ProgressManager.CompleteUncompleted(true);
                        WriteInvalidOperation(ex);
                    }
                    catch (Exception ex)
                    {
                        if (!(PipeToSelectObject() && ex is PipelineStoppedException))
                            ProgressManager.TryCompleteProgress();

                        ProgressManager.CompleteUncompleted(true);

                        throw;
                    }
                }
            }
            catch(Exception)
            {
                UnregisterEvents(false);
                throw;
            }
            finally
            {
                if (client != null)
                    client.DefaultCancellationToken = CancellationToken.None;

                if (Stopping)
                {
                    UnregisterEvents(false);
                }
            }

            //If we're the last cmdlet in the pipeline, we need to unregister ourselves so that the upstream cmdlet
            //regains the ability to invoke its events when its control is returned to it
            if (!noClient && !Stopping && EventManager.LogVerboseEventStack.Peek().Target == this)
            {
                UnregisterEvents(true);
            }
        }

        internal void WriteInvalidOperation(Exception ex, object targetObject = null, ErrorCategory errorCategory = ErrorCategory.InvalidOperation)
        {
            if (!CanWriteToHost())
            {
                Debug.WriteLine($"{MyInvocation.MyCommand}: {ex}");
                return;
            }

            WriteError(new ErrorRecord(
                ex,
                ex.GetType().Name,
                errorCategory,
                targetObject
            ));
        }

        internal void WriteInvalidOperation(string message, object targetObject = null)
        {
            WriteInvalidOperation(new InvalidOperationException(message), targetObject);
        }

        /// <summary>
        /// Performs enhanced record-by-record processing functionality for the cmdlet.
        /// </summary>
        protected abstract void ProcessRecordEx();

        /// <summary>
        /// Performs one-time, post-processing functionality for the cmdlet. This function is only run when the cmdlet successfully runs to completion.
        /// </summary>
        protected override void EndProcessing()
        {
            EndProcessing();
        }

        internal void EndProcessing(bool endExtended = true)
        {
            if (endExtended)
                EndProcessingEx();

            ProgressManager?.CompleteUncompleted();

            UnregisterEvents(false);
        }

        /// <summary>
        /// Provides an enhanced one-time, postprocessing functionality for the cmdlet.
        /// </summary>
        protected virtual void EndProcessingEx()
        {
        }

        /// <summary>
        /// Interrupts the currently running code to signal the cmdlet has been requested to stop.<para/>
        /// Do not override this method; override <see cref="StopProcessingEx"/> instead.
        /// </summary>
        [ExcludeFromCodeCoverage]
        protected override void StopProcessing()
        {
            StopProcessingEx();

            TokenSource.Cancel();
        }

        /// <summary>
        /// Interrupts the currently running code to signal the cmdlet has been requested to stop.
        /// </summary>
        protected virtual void StopProcessingEx()
        {
        }

        /// <summary>
        /// Disposes of all managed and unmanaged resources used by the cmdlet.
        /// </summary>
        public void Dispose()
        {
            if (disposed == false)
            {
                TokenSource.Dispose();

                disposed = true;
            }
        }

        private bool PipeToSelectObject()
        {
            var commands = ProgressManager.CacheManager.GetPipelineCommands();

            var myIndex = commands.IndexOf(this);

            return commands.Skip(myIndex + 1).Where(SelectObjectDescriptor.IsSelectObjectCommand).Any(c => new SelectObjectDescriptor((PSCmdlet) c).HasFilters);
        }

        internal void Sleep(int milliseconds)
        {
            TokenSource.Token.WaitHandle.WaitOne(milliseconds);
        }

        #region Events

        private void RegisterEvents()
        {
            //Cmdlets that optionally depend on a PrtgClient (such as New-SensorParameters) might not have a Client to use for events
            if (PrtgSessionState.Client != null)
            {
                eventManager.AddEvent(OnRetryRequest, eventManager.RetryEventState, EventManager.RetryEventStack);
                eventManager.AddEvent(OnLogVerbose, eventManager.LogVerboseEventState, EventManager.LogVerboseEventStack);
            }
            else
                noClient = true;
        }

        private void UnregisterEvents(bool resetState)
        {
            eventManager.RemoveEvent(eventManager.RetryEventState, EventManager.RetryEventStack, resetState);
            eventManager.RemoveEvent(eventManager.LogVerboseEventState, EventManager.LogVerboseEventStack, resetState);
        }

        [ExcludeFromCodeCoverage]
        private void OnRetryRequest(object sender, RetryRequestEventArgs args)
        {
            var msg = args.Exception.Message.TrimEnd('.');

            TryWriteWarning($"'{MyInvocation.MyCommand}' timed out: {msg}. Retries remaining: {args.RetriesRemaining}");
        }

        private void OnLogVerbose(object sender, LogVerboseEventArgs args)
        {
            //Lazy values will execute in the context of the previous command when retrieved from the next cmdlet
            //(such as Select-Object)
            if (CanWriteToHost())
                TryWriteVerbose($"{MyInvocation.MyCommand}: {args.Message}");

            Debug.WriteLine($"{MyInvocation.MyCommand}: {args.Message}");
        }

        private bool CanWriteToHost()
        {
            if (CommandRuntime is DummyRuntime)
                return true;

            if (Thread.CurrentThread.ManagedThreadId != owningThreadId)
                return false;

            return CommandRuntime.GetInternalProperty("PipelineProcessor").GetInternalField("_permittedToWrite") == this;
        }

        private void TryWriteWarning(string message)
        {
            if (!CanWriteToHost())
            {
                Debug.WriteLine($"{MyInvocation.MyCommand}: {message}");
                return;
            }

            try
            {
                WriteWarning(message);
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine($"{MyInvocation.MyCommand}: {message}");
            }
        }

        private void TryWriteVerbose(string message)
        {
            if (!CanWriteToHost())
            {
                Debug.WriteLine(message);
                return;
            }

            try
            {
                WriteVerbose(message);
            }
            catch (InvalidOperationException)
            {
                Debug.WriteLine(message);
            }
        }

        #endregion
    }
}
