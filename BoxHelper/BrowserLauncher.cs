using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;

namespace Core.BoxHelper
{
    internal static class BrowserLauncher
    {
        /// <summary>
        /// Reads path of default browser from registry
        /// </summary>
        /// <returns>Path to the default web browser executable file</returns>
        private static string GetDefaultBrowserPath()
        {
            string key = @"htmlfile\shell\open\command";

            RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(key, false);

            return ((string)registryKey.GetValue(null, null)).Split('"')[1];

        }

        /// <summary>
        /// Opens <paramref name="url"/> in a default web browser
        /// </summary>
        /// <param name="url">Destination URL</param>
        public static void OpenUrl(string url)
        {
            string defaultBrowserPath = GetDefaultBrowserPath();

            Process.Start(defaultBrowserPath, url);
        }
    }
}
