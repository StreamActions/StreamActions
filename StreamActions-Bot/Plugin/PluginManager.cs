using System;
using System.Collections.Generic;
using System.Text;

namespace StreamActions.Plugin
{
    /// <summary>
    /// Loads and manages the state of plugins, as well as running the plugin event handlers
    /// </summary>
    public class PluginManager
    {
        #region Public Properties

        public static PluginManager Instance { get { return _instance.Value; } }

        #endregion Public Properties

        #region Private Fields

        private static readonly Lazy<PluginManager> _instance = new Lazy<PluginManager>(() => new PluginManager());

        #endregion Private Fields

        #region Private Constructors

        private PluginManager()
        {
        }

        #endregion Private Constructors
    }
}