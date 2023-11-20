//------------------------------------------------------------------------------
// <copyright file="GeometryWatch.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

namespace GraphicalDebugging
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("a9b7cbec-2f38-4ece-b2d1-fddf233cee59")]
    public class GeometryWatch : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryWatch"/> class.
        /// </summary>
        public GeometryWatch() : base(null)
        {
            this.Caption = "Geometry Watch";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new GeometryWatchControl();
        }

        protected override void OnClose()
        {
            (this.Content as GeometryWatchControl).OnClose();
            base.OnClose();
        }
    }
}
