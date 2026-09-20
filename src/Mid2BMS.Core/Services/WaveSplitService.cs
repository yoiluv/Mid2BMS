using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mid2BMS
{
    internal sealed class WaveSplitService
    {
        private readonly string PathBase;
        private readonly string WavePathBase;
        private readonly string FileName_WaveFile;

        public WaveSplitService(string pathBase, string wavePathBase, string waveFileName)
        {
            PathBase = pathBase;
            WavePathBase = wavePathBase;
            FileName_WaveFile = waveFileName;
        }

        public int Split(
            double tailcut_threshold, int fadeintime, int fadeouttime, double silence_time, bool inputFileIndicated,
            bool renamingEnabled, string renamingFilename, float[] SilenceLevelsSquare,
            ref double ProgressBarValue, out int RenameRequiredFilesCount)
        {
            String[][] WaveSplitter_Text;
            IEnumerable<String[]> WaveRenamer_Text;

            if (renamingEnabled)
            {
                KeySoundManifest manifest = KeySoundManifest.FromLegacyRenamerText(
                    FileIO.ReadAllText(PathBase + @"text5_renamer_array.txt"));
                WaveRenamer_Text = manifest.ToLegacyWaveRenamerRows();

                if (inputFileIndicated)
                {
                    WaveSplitter_Text = new[] { new[] { FileName_WaveFile } };
                }
                else
                {
                    WaveSplitter_Text = manifest.Tracks.Select(track => new[] { track.InputPrefix + track.InputSuffix }).ToArray();
                }

                RenameRequiredFilesCount = manifest.Tracks.Sum(track => track.RequiredWaveFileCount);
            }
            else
            {
                if (inputFileIndicated == false) throw new Exception("enableRenaming == false && inputFileIndicated == false の組み合わせは使用できません");
                
                WaveSplitter_Text = new[] { new[] { FileName_WaveFile } };
                WaveRenamer_Text = new LambdaEnumerable<String[]>(i => new[] { "ハッピー", "ハードコア", "1", String.Format(renamingFilename, (object)(i + 1)) });  // ボックス化を避けたい・・・？

                RenameRequiredFilesCount = -1;
            }

            Directory.CreateDirectory(PathBase + @"renamed\");
            var ws = new WaveSplitter2();
            ws.ThresholdInDB = tailcut_threshold;
            ws.FadeInSamples = fadeintime;
            ws.FadeOutSamples = fadeouttime;
            ws.SilenceTime = silence_time;
            int createdWavCount = ws.Process(
                WaveSplitter_Text, WaveRenamer_Text, 
                WavePathBase, PathBase + @"renamed\",
                SilenceLevelsSquare,
                ref ProgressBarValue, 0.00, 1.00);

            return createdWavCount;
        }
    }
}
