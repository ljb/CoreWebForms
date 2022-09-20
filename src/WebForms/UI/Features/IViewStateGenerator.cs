// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Web.UI.Features;

internal interface IViewStateManager
{
    string GeneratorId { get; }

    string OriginalState { get; }

    string ClientState { get; }

    void UpdateClientState();

    void RefreshControls();
}
