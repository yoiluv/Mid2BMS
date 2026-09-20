using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Mid2BMS
{
    // Reads legacy hashvalues files before generated outputs may be overwritten.
    internal sealed class GeneratedFileHashGuard
    {
        private static readonly string[] MidiOutputNames = {
            @"text0_stdout_part1.mml",
            @"text1_midtable_debug.txt",
            @"text2_mmlrenew_debug.txt",
            @"text3_tanon.mml",
            @"text4_renamer.txt",
            @"text5_renamer_array.txt",
            @"text6_bms.txt",
            @"text7_bms_for_debug.txt",
            @"text8_errorlog_debug.txt",
            @"text9_trackname_csv.txt",
            @"wavesplitter_input.txt",
        };

        private static readonly string[] WaveOutputNames = {
            @"wavesplitter_renameresult.txt",
        };

        private readonly string pathBase;

        public GeneratedFileHashGuard(string pathBase)
        {
            this.pathBase = pathBase;
        }

        public bool CheckMidiOutputs(Func<string, bool> confirmOverwrite)
        {
            return Check(MidiOutputNames, @"hashvalues1.txt", confirmOverwrite);
        }

        public bool CheckWaveOutputs(Func<string, bool> confirmOverwrite)
        {
            return Check(WaveOutputNames, @"hashvalues2.txt", confirmOverwrite);
        }

        private bool Check(string[] fileNames, string hashFileName, Func<string, bool> confirmOverwrite)
        {
            if (!File.Exists(pathBase + hashFileName)) return true;
            string[] hashValues = File.ReadAllLines(pathBase + hashFileName);
            for (int i = 0; i < fileNames.Length && i < hashValues.Length; i++)
            {
                if (!File.Exists(pathBase + fileNames[i])) continue;
                if (GetHash(pathBase + fileNames[i]) != hashValues[i])
                {
                    string message = "ファイル " + fileNames[i] + "が変更されています。\r\n処理によっては上書きされることがあります。続行しますか？";
                    if (!confirmOverwrite(message)) return false;
                }
            }
            return true;
        }

        private static string GetHash(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(stream);
                var result = new StringBuilder();
                foreach (byte value in hash) result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }
    }
}
