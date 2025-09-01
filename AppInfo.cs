// ============================================================================
//  ApolloGUI — AppInfo.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

using System;
using System.Diagnostics;
using System.Reflection;

namespace ApolloGUI
{
    public static class AppInfo
    {
        public static string ProductName => "ApolloGUI";

        public static string Version
        {
            get
            {
                try
                {
                    var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (infoAttr != null && !string.IsNullOrWhiteSpace(infoAttr.InformationalVersion))
                        return infoAttr.InformationalVersion;

                    var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                    if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                        return fvi.ProductVersion;

                    var v = asm.GetName().Version;
                    return v != null ? v.ToString() : "1.0.0";
                }
                catch
                {
                    var v = Assembly.GetExecutingAssembly().GetName().Version;
                    return v != null ? v.ToString() : "1.0.0";
                }
            }
        }

        public static string WindowTitle => $"{ProductName} v{Version}";
    }
}