﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace ImGuiNET
{
    /// <summary>
    /// Adapted from Mellinoe's file picker for imgui
    /// https://github.com/mellinoe/synthapp/blob/master/src/synthapp/Widgets/FilePicker.cs
    /// </summary>
    public class FilePicker
    {
        private const string FilePickerID = "###FilePicker";
        private static readonly Dictionary<object, FilePicker> s_filePickers = new Dictionary<object, FilePicker>();
        private static readonly Vector2 DefaultFilePickerSize = new Vector2(600, 400);

        public string CurrentFolder { get; set; }
        public string SelectedFile { get; set; }

        public static FilePicker GetFilePicker(object o, string startingPath)
        {
            if (File.Exists(startingPath))
            {
                startingPath = new FileInfo(startingPath).DirectoryName;
            }
            else if (string.IsNullOrEmpty(startingPath) || !Directory.Exists(startingPath))
            {
                startingPath = Environment.CurrentDirectory;
                if (string.IsNullOrEmpty(startingPath))
                {
                    startingPath = AppContext.BaseDirectory;
                }
            }

            if (!s_filePickers.TryGetValue(o, out FilePicker fp))
            {
                fp = new FilePicker();
                fp.CurrentFolder = startingPath;
                s_filePickers.Add(o, fp);
            }

            return fp;
        }

        public bool Draw(ref string selected, bool returnOnSelection)
        {
            bool result = false;
            result = DrawFolder(ref selected, returnOnSelection);
            return result;
        }

        private bool DrawFolder(ref string selected, bool returnOnSelection = false)
        {
            ImGui.Text("Current Folder: " + CurrentFolder);
            bool result = false;

            if (ImGui.BeginChildFrame(1, ImGui.GetContentRegionAvailable() - new Vector2(20, Values.ButtonHeight),
                WindowFlags.Default))
            {
                DirectoryInfo di = new DirectoryInfo(CurrentFolder);
                if (di.Exists)
                {
                    if (di.Parent != null)
                    {
                        ImGui.PushStyleColor(ColorTarget.Text, Values.Color.Yellow);

                        if (ImGui.Selectable("../", false, SelectableFlags.DontClosePopups
                            , new Vector2(ImGui.GetContentRegionAvailableWidth(), Values.SelectibleHeight)))
                        {
                            CurrentFolder = di.Parent.FullName;
                        }

                        ImGui.PopStyleColor();
                    }
                    foreach (var dir in Directory.EnumerateFileSystemEntries(di.FullName))
                    {
                        if (Directory.Exists(dir))
                        {
                            string name = Path.GetFileName(dir);
                            bool isSelected = SelectedFile == dir;

                            ImGui.PushStyleColor(ColorTarget.Text, Values.Color.Yellow);

                            if (ImGui.Selectable(name + "/", isSelected, SelectableFlags.DontClosePopups
                               , new Vector2(ImGui.GetContentRegionAvailableWidth(), Values.SelectibleHeight)))
                            {
                                SelectedFile = dir;
                                selected = SelectedFile;
                            }

                            if (SelectedFile != null)
                                if (ImGui.IsMouseDoubleClicked(0) && SelectedFile.Equals(dir))
                                {
                                    SelectedFile = null;
                                    selected = null;
                                    CurrentFolder = dir;
                                }

                            ImGui.PopStyleColor();
                        }
                    }
                    foreach (var file in Directory.EnumerateFiles(di.FullName))
                    {
                        string name = Path.GetFileName(file);
                        bool isSelected = SelectedFile == file;

                        if (ImGui.Selectable(name, isSelected, SelectableFlags.DontClosePopups
                            , new Vector2(ImGui.GetContentRegionAvailableWidth(), Values.SelectibleHeight)))
                        {
                            SelectedFile = file;
                            if (returnOnSelection)
                            {
                                selected = SelectedFile;
                            }
                        }

                        if (SelectedFile != null)
                            if (ImGui.IsMouseDoubleClicked(0) && SelectedFile.Equals(file))
                            {
                                selected = file;
                                result = true;
                            }
                    }
                }
            }
            ImGui.EndChildFrame();


            if (ImGui.Button("Cancel", new Vector2(Values.ButtonWidth, Values.ButtonHeight)))
            {
                result = false;
            }

            if (SelectedFile != null)
            {
                ImGui.SameLine();
                if (ImGui.Button("Open", new Vector2(Values.ButtonWidth, Values.ButtonHeight)))
                {
                    result = true;
                    selected = SelectedFile;
                }
            }

            return result;
        }
    }
}
