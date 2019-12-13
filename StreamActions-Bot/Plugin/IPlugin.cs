using System;
using System.Collections.Generic;
using System.Text;

namespace StreamActions.Plugin
{
    /// <summary>
    /// An interface for a plugin that processes messages, commands, and other input
    /// </summary>
    public interface IPlugin
    {
        #region Public Methods

        /// <summary>
        /// Called when the plugin is disabled. Event delegates should be unregistered here
        /// </summary>
        public void Disabled();

        /// <summary>
        /// Called when the plugin is enabled. Event delegates should be registered here
        /// </summary>
        public void Enabled();

        /// <summary>
        /// Returns a basic, friendly description of the plugin
        /// </summary>
        /// <returns>A description of the plugin</returns>
        public string GetPluginDescription();

        /// <summary>
        /// Returns a friendly name of the plugin
        /// </summary>
        /// <returns>The name of the plugin</returns>
        public string GetPluginName();

        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>The plugin version</returns>
        public string GetPluginVersion();

        #endregion Public Methods
    }
}