﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.FileProviders;

namespace Microsoft.AspNetCore.SystemWebAdapters.UI.RuntimeCompilation;

internal interface ICompilationRegistrar
{
    ICompiledPagesCollection Register(IFileProvider files);
}