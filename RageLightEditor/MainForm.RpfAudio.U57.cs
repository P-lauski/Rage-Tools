using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RageLightEditor.Editor;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private string awcLastDir_U57;

        private void ServiceAwcExport_U57()
        {
            ServiceAwcProbe_U57();
            var p = panel;
            if (p == null) return;
            if (p.RequestAwcExport_U57 != null)
            {
                var t = p.RequestAwcExport_U57;
                p.RequestAwcExport_U57 = null;
                if (IsHeadless) return;
                using var dlg = new SaveFileDialog
                {
                    Title = "Export " + t.Name,
                    FileName = AwcAudio_U57.FileNameFor(t),
                    Filter = t.IsMidi ? "MIDI (*.mid)|*.mid" : "Wave audio (*.wav)|*.wav",
                    InitialDirectory = awcLastDir_U57 ?? rpfLastExtractDir,
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllBytes(dlg.FileName, AwcAudio_U57.ExportBytes(t));
                    awcLastDir_U57 = Path.GetDirectoryName(dlg.FileName);
                    p.RpfStatus = "exported " + Path.GetFileName(dlg.FileName);
                }
                catch (Exception ex) { p.RpfStatus = "export failed: " + ex.Message; }
            }
            if (p.RequestAwcExportAll_U57)
            {
                p.RequestAwcExportAll_U57 = false;
                if (IsHeadless) return;
                using var dlg = new FolderBrowserDialog
                {
                    Description = "Export every track of " + p.AwcName_U57 + " as .wav into...",
                    UseDescriptionForTitle = true,
                    SelectedPath = awcLastDir_U57 ?? rpfLastExtractDir ?? "",
                };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var folder = Path.Combine(dlg.SelectedPath, Path.GetFileNameWithoutExtension(p.AwcName_U57));
                    var (written, skipped) = AwcAudio_U57.ExportAll(p.AwcTracks_U57, folder);
                    awcLastDir_U57 = dlg.SelectedPath;
                    p.RpfStatus = $"exported {written} track(s) to {folder}" + (skipped > 0 ? $" ({skipped} skipped)" : "");
                }
                catch (Exception ex) { p.RpfStatus = "export failed: " + ex.Message; }
            }
        }
    }
}
