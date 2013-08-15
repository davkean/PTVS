﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Language {
    /// <summary>
    /// IVsTextViewFilter is implemented to statisfy new VS2012 requirement for debugger tooltips.
    /// Do not use this from VS2010, it will break debugger tooltips!
    /// </summary>
    public sealed class TextViewFilter : IOleCommandTarget, IVsTextViewFilter {
        private static IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;
        private readonly IOleCommandTarget _next;
        private readonly IWpfTextView _wpfTextView;

        public TextViewFilter(IVsTextView vsTextView) {
            if (_vsEditorAdaptersFactoryService == null) {
                _vsEditorAdaptersFactoryService = PythonToolsPackage.ComponentModel.GetService<IVsEditorAdaptersFactoryService>();
            }

            _wpfTextView = _vsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);

            ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
        }

        #region IOleCommandTarget Members

        /// <summary>
        /// Called from VS when we should handle a command or pass it on.
        /// </summary>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        /// <summary>
        /// Called from VS to see what commands we support.  
        /// </summary>
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < cCmds; i++) {
                    switch((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.MarkerCmd0:
                        case VSConstants.VSStd97CmdID.MarkerCmd1:
                        case VSConstants.VSStd97CmdID.MarkerCmd2:
                        case VSConstants.VSStd97CmdID.MarkerCmd3:
                        case VSConstants.VSStd97CmdID.MarkerCmd4:
                        case VSConstants.VSStd97CmdID.MarkerCmd5:
                        case VSConstants.VSStd97CmdID.MarkerCmd6:
                        case VSConstants.VSStd97CmdID.MarkerCmd7:
                        case VSConstants.VSStd97CmdID.MarkerCmd8:
                        case VSConstants.VSStd97CmdID.MarkerCmd9:
                        case VSConstants.VSStd97CmdID.MarkerEnd:
                            // marker commands are broken on projection buffers, hide them.
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            }
            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText) {
            if (pSpan.Length != 1) {
                throw new ArgumentException("Array parameter should contain exactly one TextSpan", "pSpan");
            }

            // Debugger will handle this itself by evaluating the expression in the span if TIP_S_NODEFAULTTIP is returned.
            pbstrText = null;

            // Adjust the span to expression boundaries.
            var snapshot = _wpfTextView.TextSnapshot;
            var snapshotSpan = TextSpanToSnapshotSpan(snapshot, pSpan[0]);
            var trackingSpan = snapshot.CreateTrackingSpan(snapshotSpan.Span, SpanTrackingMode.EdgeExclusive);
            var rep = new ReverseExpressionParser(snapshot, _wpfTextView.TextBuffer, trackingSpan);
            var exprSpan = rep.GetExpressionRange(forCompletion: false);
            if (exprSpan != null) {
                pSpan[0] = SnapshotSpanToTextSpan(exprSpan.Value);
            } else {
                // If it's not an expression, suppress the tip.
                return VSConstants.E_FAIL;
            }

            return (int)TipSuccesses2.TIP_S_NODEFAULTTIP;
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        private static SnapshotPoint LineAndColumnNumberToSnapshotPoint(ITextSnapshot snapshot, int lineNumber, int columnNumber) {
            var line = snapshot.GetLineFromLineNumber(lineNumber);
            var snapShotPoint = new SnapshotPoint(snapshot, line.Start + columnNumber);
            return snapShotPoint;
        }

        private static SnapshotSpan TextSpanToSnapshotSpan(ITextSnapshot snapshot, TextSpan textSpan) {
            var start = LineAndColumnNumberToSnapshotPoint(snapshot, textSpan.iStartLine, textSpan.iStartIndex);
            var end = LineAndColumnNumberToSnapshotPoint(snapshot, textSpan.iEndLine, textSpan.iEndIndex);
            return new SnapshotSpan(start, end);
        }

        private static void SnapshotPointToLineAndColumnNumber(SnapshotPoint snapshotPoint, out int lineNumber, out int columnNumber) {
            var line = snapshotPoint.GetContainingLine();
            lineNumber = line.LineNumber;
            columnNumber = snapshotPoint.Position - line.Start.Position;
        }

        private static TextSpan SnapshotSpanToTextSpan(SnapshotSpan snapshotSpan) {
            TextSpan textSpan;
            SnapshotPointToLineAndColumnNumber(snapshotSpan.Start, out textSpan.iStartLine, out textSpan.iStartIndex);
            SnapshotPointToLineAndColumnNumber(snapshotSpan.End, out textSpan.iEndLine, out textSpan.iEndIndex);
            return textSpan;
        }
    }
}
