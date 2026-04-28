namespace PrtgAPI.PowerShell
{
    class PrtgSessionState
    {
        private static PrtgClientManager clientManager;

        /// <summary>
        /// Gets or sets the client manager for handling multiple server connections.
        /// </summary>
        internal static PrtgClientManager ClientManager
        {
            get
            {
                if (clientManager == null)
                    clientManager = new PrtgClientManager();
                return clientManager;
            }
            set { clientManager = value; }
        }

        /// <summary>
        /// Gets or sets the default PrtgClient for backward compatibility.
        /// When set, it replaces the default client in the manager.
        /// When get, it returns the default client from the manager.
        /// </summary>
        internal static PrtgClient Client
        {
            get { return ClientManager.DefaultClient; }
            set
            {
                if (value != null)
                    ClientManager.AddOrUpdateClient(value, setAsDefault: true);
                else
                    ClientManager.Clear();
            }
        }

        internal static PSEdition? PSEdition { get; set; }
        internal static bool EnableProgress { get; set; }
    }
}
