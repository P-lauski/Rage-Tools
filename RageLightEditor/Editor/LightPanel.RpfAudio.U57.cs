using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public readonly WavePlayer_U57 Audio_U57 = new WavePlayer_U57();
        public List<AwcTrack_U57> AwcTracks_U57 = new List<AwcTrack_U57>();
        public string AwcName_U57 = "";
        public string AwcError_U57;
        public int AwcSelected_U57 = -1;
        public bool AwcAutoPlay_U57;
        public AwcTrack_U57 RequestAwcExport_U57;
        public bool RequestAwcExportAll_U57;
        private string awcKey_U57;
        private int awcPlayingTrack_U57 = -1;
        private bool awcDrawn_U57, awcDrawnLast_U57;
        private string awcPlayError_U57;

        public void LoadAwc_U57(string key, Func<byte[]> data, string name, RpfFileEntry entry)
        {
            if (key == awcKey_U57) return;
            Audio_U57.Stop();
            awcKey_U57 = key;
            AwcName_U57 = name ?? "";
            AwcTracks_U57 = new List<AwcTrack_U57>();
            AwcSelected_U57 = -1;
            awcPlayingTrack_U57 = -1;
            AwcError_U57 = null;
            awcPlayError_U57 = null;
            try
            {
                var bytes = data();
                if (bytes == null || bytes.Length == 0) throw new Exception("it came out of the archive empty");
                var awc = AwcAudio_U57.Load(bytes, name, entry);
                AwcTracks_U57 = AwcAudio_U57.TracksOf(awc);
                if (AwcTracks_U57.Count > 0) AwcSelected_U57 = 0;
                if (AwcTracks_U57.Count == 0) AwcError_U57 = "no playable audio in this container";
            }
            catch (Exception ex) { AwcError_U57 = "could not read it: " + ex.Message; }
        }

        public bool PlayAwcTrack_U57(int index)
        {
            if (index < 0 || index >= AwcTracks_U57.Count) return false;
            var t = AwcTracks_U57[index];
            AwcSelected_U57 = index;
            if (!t.Playable)
            {
                awcPlayError_U57 = t.IsMidi ? "MIDI can't be played here - export it as .mid" : "this track's codec can't be decoded";
                return false;
            }
            try
            {
                if (awcPlayingTrack_U57 != index || !Audio_U57.Loaded)
                {
                    Audio_U57.Load(AwcAudio_U57.Pcm16(t), t.Rate, t.Channels);
                    awcPlayingTrack_U57 = index;
                }
                bool ok = Audio_U57.Play(0f);
                awcPlayError_U57 = ok ? null : Audio_U57.LastError;
                return ok;
            }
            catch (Exception ex) { awcPlayError_U57 = "could not decode it: " + ex.Message; return false; }
        }

        private void EndAudioFrame_U57()
        {
            if (!awcDrawnLast_U57 && !awcDrawn_U57 && (Audio_U57.Playing || Audio_U57.Paused)) Audio_U57.Stop();
            awcDrawnLast_U57 = awcDrawn_U57;
            awcDrawn_U57 = false;
        }

        private static string Clock_U57(float s) =>
            TimeSpan.FromSeconds(Math.Max(0, s)).ToString(s >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

        private void DrawAwcPlayer_U57(string key, Func<byte[]> data, string name, RpfFileEntry entry)
        {
            awcDrawn_U57 = true;
            LoadAwc_U57(key, data, name, entry);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("AUDIO");
            if (!string.IsNullOrEmpty(AwcError_U57))
            {
                ImGui.TextColored(UiTheme.Warn, AwcError_U57);
                return;
            }
            var tracks = AwcTracks_U57;
            float total = tracks.Sum(t => t.Seconds);
            ImGui.SameLine();
            ImGui.TextDisabled($"{tracks.Count} track{(tracks.Count == 1 ? "" : "s")}, {Clock_U57(total)}");

            if (AwcAutoPlay_U57)
            {
                AwcAutoPlay_U57 = false;
                int first = tracks.FindIndex(t => t.Playable);
                if (first >= 0) PlayAwcTrack_U57(first);
            }

            float rowH = ImGui.GetTextLineHeightWithSpacing();
            float listH = Math.Clamp(tracks.Count * rowH + 8, rowH * 2 + 8, rowH * 9 + 8);
            if (ImGui.BeginTable("##u57tracks", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY,
                                 new Vector2(-1, listH)))
            {
                ImGui.TableSetupColumn("Track", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Format", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("ADPCM 44.1 kHz").X);
                ImGui.TableSetupColumn("Length", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("00:00").X);
                for (int i = 0; i < tracks.Count; i++)
                {
                    var t = tracks[i];
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    bool playingThis = i == awcPlayingTrack_U57 && (Audio_U57.Playing || Audio_U57.Paused);
                    if (playingThis) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Accent);
                    if (ImGui.Selectable((playingThis ? "> " : "") + t.Name + "##u57t" + i, AwcSelected_U57 == i,
                                         ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        AwcSelected_U57 = i;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) PlayAwcTrack_U57(i);
                    }
                    if (playingThis) ImGui.PopStyleColor();
                    if (ImGui.BeginPopupContextItem("##u57tm" + i))
                    {
                        if (ImGui.MenuItem("Play", null, false, t.Playable)) PlayAwcTrack_U57(i);
                        if (ImGui.MenuItem(t.IsMidi ? "Export as .mid..." : "Export as .wav...", null, false, t.Playable || t.IsMidi))
                            RequestAwcExport_U57 = t;
                        if (ImGui.MenuItem("Copy name")) ImGui.SetClipboardText(t.Name);
                        ImGui.EndPopup();
                    }
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(t.IsMidi ? "MIDI" : t.Rate > 0 ? $"{t.Codec} {t.Rate / 1000.0:0.#} kHz" : t.Codec);
                    ImGui.TableNextColumn();
                    ImGui.TextDisabled(t.IsMidi ? "" : t.LengthText);
                }
                ImGui.EndTable();
            }

            var p = Audio_U57;
            bool active = p.Playing || p.Paused;
            float avail = ImGui.GetContentRegionAvail().X;
            float sp = ImGui.GetStyle().ItemSpacing.X;
            float bw = (avail - sp * 3) / 4f;
            var sel = AwcSelected_U57 >= 0 && AwcSelected_U57 < tracks.Count ? tracks[AwcSelected_U57] : null;

            if (ImGui.Button("Prev##u57", new Vector2(bw, 0)) && tracks.Count > 0)
                PlayAwcTrack_U57(Math.Max(0, (AwcSelected_U57 < 0 ? 0 : AwcSelected_U57) - 1));
            ImGui.SameLine();
            string playLabel = p.Playing && !p.Paused ? "Pause" : "Play";
            if (p.Playing && !p.Paused) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.ButtonOn);
            bool playHit = ImGui.Button(playLabel + "##u57", new Vector2(bw, 0));
            if (p.Playing && !p.Paused) ImGui.PopStyleColor();
            if (playHit)
            {
                if (p.Playing && !p.Paused) p.Pause();
                else if (p.Paused && awcPlayingTrack_U57 == AwcSelected_U57) p.Play();
                else if (AwcSelected_U57 >= 0) PlayAwcTrack_U57(AwcSelected_U57);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Space plays and pauses while the list is focused. Double-click a track to play it.");
            ImGui.SameLine();
            if (ImGui.Button("Stop##u57", new Vector2(bw, 0))) p.Stop();
            ImGui.SameLine();
            if (ImGui.Button("Next##u57", new Vector2(bw, 0)) && tracks.Count > 0)
                PlayAwcTrack_U57(Math.Min(tracks.Count - 1, AwcSelected_U57 + 1));

            if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.GetIO().WantTextInput &&
                ImGui.IsKeyPressed(ImGuiKey.Space, false))
            {
                if (p.Playing && !p.Paused) p.Pause();
                else if (p.Paused) p.Play();
                else if (AwcSelected_U57 >= 0) PlayAwcTrack_U57(AwcSelected_U57);
            }

            float dur = awcPlayingTrack_U57 >= 0 && p.Loaded ? p.Duration : (sel?.Seconds ?? 0f);
            float pos = active ? p.Position : 0f;
            float seek = pos;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##u57seek", ref seek, 0f, Math.Max(dur, 0.01f), $"{Clock_U57(pos)} / {Clock_U57(dur)}") && active)
                p.Seek(seek);
            float vol = p.Volume * 100f;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.SliderFloat("##u57vol", ref vol, 0f, 100f, "volume %.0f%%")) p.Volume = vol / 100f;

            float half = (ImGui.GetContentRegionAvail().X - sp) * 0.5f;
            ImGui.BeginDisabled(sel == null || (!sel.Playable && !sel.IsMidi));
            if (ImGui.Button((sel?.IsMidi == true ? "Export .mid..." : "Export .wav...") + "##u57x", new Vector2(half, 0)))
                RequestAwcExport_U57 = sel;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Saves the selected track as a normal .wav (16-bit PCM) any player can open.");
            ImGui.SameLine();
            ImGui.BeginDisabled(tracks.Count == 0);
            if (ImGui.Button("Export all...##u57xa", new Vector2(-1, 0))) RequestAwcExportAll_U57 = true;
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every track as .wav into a folder you choose.");
            if (!string.IsNullOrEmpty(awcPlayError_U57)) ImGui.TextColored(UiTheme.Warn, awcPlayError_U57);
        }
    }
}
