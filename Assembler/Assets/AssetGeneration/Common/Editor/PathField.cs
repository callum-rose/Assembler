using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
    /// <summary>
    /// Shared editor helpers for the AssetGeneration windows' file/folder path inputs:
    /// a browse start directory that follows the last entered path, and drag-and-drop of
    /// a project asset (or an OS file/folder) onto a field to fill in its path.
    /// </summary>
    public static class PathField
    {
        /// <summary>
        /// Directory a browse panel should open at: the folder holding <paramref name="path"/>
        /// (or the path itself when it is already a directory), falling back to the project's
        /// Assets folder when nothing has been entered yet.
        /// </summary>
        public static string GuessStartDir(string path) => path switch
        {
            null or "" => Application.dataPath,
            _ when Directory.Exists(path) => path,
            _ => Path.GetDirectoryName(path) is { Length: > 0 } dir ? dir : Application.dataPath,
        };

        /// <summary>
        /// Handle a drag-and-drop of an asset or OS file/folder over <paramref name="rowRect"/>.
        /// Returns the dropped path (absolute) when a matching item is dropped on this event,
        /// otherwise <paramref name="current"/> unchanged. Set <paramref name="wantFolder"/> to
        /// accept directories rather than files. Call once per field, passing the rect of the
        /// field row (e.g. <c>GUILayoutUtility.GetLastRect()</c> after the row's layout group).
        /// </summary>
        public static string HandleDrop(Rect rowRect, string current, bool wantFolder = false)
        {
            var evt = Event.current;
            if (evt.type is not (EventType.DragUpdated or EventType.DragPerform))
                return current;
            if (!rowRect.Contains(evt.mousePosition))
                return current;

            var dropped = FirstMatchingPath(wantFolder);
            if (dropped is null)
                return current;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type != EventType.DragPerform)
            {
                evt.Use();
                return current;
            }

            DragAndDrop.AcceptDrag();
            GUI.changed = true;
            evt.Use();
            return dropped;
        }

        // Absolute path of the first dragged item of the wanted kind (file vs folder), or null.
        // Project assets arrive as "Assets/…" relative paths; resolve them to absolute so the
        // result matches what the browse panels return.
        private static string? FirstMatchingPath(bool wantFolder)
        {
            foreach (var path in DragAndDrop.paths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                var full = Path.GetFullPath(path);
                if (wantFolder ? Directory.Exists(full) : File.Exists(full))
                    return full;
            }

            return null;
        }
    }
}
