using System;
using System.Windows;

namespace DownloadsInTray;

/// <summary>Starts a file drag from a flyout. The target performs the transfer: Explorer moves within a drive and copies across drives.</summary>
internal static class FileDrag
{
    /// <summary>Calls <paramref name="onLeftWindow"/> once the drag leaves the window, so targets behind it can be reached.</summary>
    public static DragDropEffects Run(UIElement source, string[] paths, IntPtr hwnd, Action onLeftWindow)
    {
        var data = new DataObject(DataFormats.FileDrop, paths);

        QueryContinueDragEventHandler hideOutside = (_, _) =>
        {
            if (source.IsVisible && !Native.IsCursorOver(hwnd)) onLeftWindow();
        };
        DragDrop.AddQueryContinueDragHandler(source, hideOutside);

        source.ReleaseMouseCapture();
        DragDropEffects result = DragDrop.DoDragDrop(source, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

        DragDrop.RemoveQueryContinueDragHandler(source, hideOutside);
        return result;
    }
}
