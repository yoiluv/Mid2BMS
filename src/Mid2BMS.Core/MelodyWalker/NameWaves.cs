using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mid2BMS
{
    class NameWaves
    {
        public List<String> wavnms;
        private readonly IKeySoundNamingStrategy namingStrategy;
        private readonly int trackId;
        private readonly int firstGlobalIndex;
        private readonly bool disambiguateTrackName;

        public NameWaves(IKeySoundNamingStrategy namingStrategy = null, int trackId = 0,
            int firstGlobalIndex = 1, bool disambiguateTrackName = false)
        {
            this.namingStrategy = namingStrategy ?? new LegacyKeySoundNamingStrategy();
            this.trackId = trackId;
            this.firstGlobalIndex = firstGlobalIndex;
            this.disambiguateTrackName = disambiguateTrackName;
        }

        private string Name(int namingway, string trackName, KeySoundMode mode, int index,
            bool isChord, bool isOneShot, IReadOnlyList<MNote> notes, string prefix, string suffix)
        {
            var identity = notes.Select(KeySoundNoteIdentity.FromMNote).ToArray();
            return namingStrategy.GetFileName(new KeySoundContext(
                namingway, trackName, mode, index, isChord, isOneShot, identity, prefix, suffix,
                trackId, firstGlobalIndex + index, disambiguateTrackName));
        }

        /// <summary>
        /// ChordModeがtrueの場合に使用します。
        /// wavファイルに名前を付けます
        /// [ InputFileNamePrefix, InputFileNameSuffix, OriginalIndex, Filename1, Filename2, ... ]
        /// </summary>
        /// <param name="namingway">予約領域</param>
        /// <param name="ib">入力</param>
        /// <param name="ia">入力</param>
        /// <param name="ob">出力</param>
        /// <param name="oa">出力</param>
        /// <param name="bb">BMS</param>
        /// <param name="ba">BMS</param>
        /// <param name="ntantmC">MidiInterpreter3.GetNta()</param>
        /// <returns></returns>
        public String AllNoteToName(
            int namingway, String ib, String ia, String ob, String oa, String bb, String ba,
            out String OutputInArrayFormat, bool isRedMode, bool isPurpleMode, List<List<MNote>> ntantmC)
        {
            if (isPurpleMode) throw new Exception("purplemodeの場合にchord modeを選択することは出来ないよ");

            int i;
            //String s2 = "";
            String w;
            StringBuilder s2 = new StringBuilder();

            wavnms = new List<string>();

            // input file name prefix
            s2.Append("" + (ib != "" ? ib : "(No line can be empty, because such lines will be ignored. This is not an error message.)") + "\r\n");

            // input file name suffix
            s2.Append("" + (ia != "" ? ia : "(No line can be empty, because such lines will be ignored. This is not an error message.)") + "\r\n");

            s2.Append("1" + "\r\n");  // original index

            for (i = 0; i < ntantmC.Count; i++)
            {
                KeySoundMode mode = isRedMode ? KeySoundMode.Red : KeySoundMode.Blue;
                wavnms.Add(Name(namingway, ib, mode, i, true, false, ntantmC[i], bb, ba));
                w = Name(namingway, ib, mode, i, true, false, ntantmC[i], ob, oa);
                s2.Append(w + "\r\n");
            }

            s2.Append("//\r\n");

            OutputInArrayFormat = s2.ToString();
            return "";
        }


        /// <summary>
        /// wavファイルに名前を付けます
        /// [ InputFileNamePrefix, InputFileNameSuffix, OriginalIndex, Filename1, Filename2, ... ]
        /// </summary>
        /// <param name="namingway">予約領域</param>
        /// <param name="ib">入力</param>
        /// <param name="ia">入力</param>
        /// <param name="ob">出力</param>
        /// <param name="oa">出力</param>
        /// <param name="bb">BMS</param>
        /// <param name="ba">BMS</param>
        /// <param name="ntantm">MidiInterpreter3.GetNta()</param>
        /// <returns></returns>
        public String AllNoteToName(
            int namingway, String ib, String ia, String ob, String oa, String bb, String ba,
            out String OutputInArrayFormat, bool isRedMode, bool isPurpleMode, List<MNote> ntantm, bool isOneShot)
        {
            int i;
            //String s2 = "";
            String w;
            StringBuilder s2 = new StringBuilder();

            wavnms = new List<string>();

            // input file name prefix
            s2.Append("" + (ib != "" ? ib : "(No line can be empty, because such lines will be ignored. This is not an error message.)") + "\r\n");

            // input file name suffix
            s2.Append("" + (ia != "" ? ia : "(No line can be empty, because such lines will be ignored. This is not an error message.)") + "\r\n");

            s2.Append("1" + "\r\n");  // original index

            if (!isRedMode && !isPurpleMode)  // blue mode
            {
                for (i = 0; i < ntantm.Count; i++)
                {
                    var note = new[] { ntantm[i] };
                    wavnms.Add(Name(namingway, ib, KeySoundMode.Blue, i, false, isOneShot, note, bb, ba));
                    w = Name(namingway, ib, KeySoundMode.Blue, i, false, isOneShot, note, ob, oa);
                    s2.Append(w + "\r\n");
                }
            }
            else if (isPurpleMode)  // purple mode
            {
                for (i = 0; i < ntantm.Count; i++)
                {
                    var note = new[] { ntantm[i] };
                    wavnms.Add(Name(namingway, ib, KeySoundMode.Purple, i, false, isOneShot, note, bb, ba));
                    w = Name(namingway, ib, KeySoundMode.Purple, i, false, isOneShot, note, ob, oa);

                    // previous note
                    s2.Append("____dummy_" + w + "\r\n");

                    // current note
                    s2.Append(w + "\r\n");
                }
            }
            else  // red mode
            {
                for (i = 0; i < ntantm.Count; i++)
                {
                    var note = new[] { ntantm[i] };
                    wavnms.Add(Name(namingway, ib, KeySoundMode.Red, i, false, isOneShot, note, bb, ba));
                    w = Name(namingway, ib, KeySoundMode.Red, i, false, isOneShot, note, ob, oa);
                    s2.Append(w + "\r\n");
                }
            }

            s2.Append("//\r\n");

            if (ntantm.Count == 0)
            {
                OutputInArrayFormat = "";
            }
            else
            {
                OutputInArrayFormat = s2.ToString();
            }
            return "";
        }
    }
}
