// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace System.Configuration
{
    // obsolete
    [ComVisible(false)]
    public interface IConfigurationSystem
    {
        // Returns the config object for the specified key.
        object GetConfig(string configKey);

        // Initializes the configuration system.
        void Init();
    }
}
