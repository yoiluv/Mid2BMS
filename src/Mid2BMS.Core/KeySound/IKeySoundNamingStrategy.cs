using System;
using System.Collections.Generic;

namespace Mid2BMS
{
    internal sealed record KeySoundContext(
        int NamingWay,
        string TrackName,
        TrackMode Mode,
        int IndexWithinTrack,
        bool IsChord,
        bool IsOneShot,
        IReadOnlyList<KeySoundNoteIdentity> Notes,
        string Prefix,
        string Suffix,
        int TrackId = 0,
        int GlobalIndex = 1,
        bool DisambiguateTrackName = false);

    internal interface IKeySoundNamingStrategy
    {
        string GetFileName(KeySoundContext context);
    }

    // The formulas are kept byte-for-byte compatible with the former NameWaves methods.
    internal sealed class LegacyKeySoundNamingStrategy : IKeySoundNamingStrategy
    {
        public string GetFileName(KeySoundContext context)
        {
            string stem;
            if (context.IsChord)
            {
                stem = String.Format("{0:D5}_", context.IndexWithinTrack + 1);
                foreach (KeySoundNoteIdentity note in context.Notes)
                    stem += GetNoteHeight(note.NoteNumber);
            }
            else
            {
                KeySoundNoteIdentity note = context.Notes[0];
                if (context.NamingWay == 0)
                {
                    stem = context.Mode == TrackMode.Red
                        ? String.Format("{0:D5}_", context.IndexWithinTrack + 1) : "";
                    stem += "v" + note.Velocity
                        + (context.IsOneShot ? "" : ("l" + note.LengthDenominator
                            + (note.LengthNumerator == 1 ? "" : ("-" + note.LengthNumerator))))
                        + "o" + (note.NoteNumber / 12) + GetNoteHeight(note.NoteNumber);
                    if (context.Mode == TrackMode.Purple)
                    {
                        int previousNote = note.PreviousNoteNumber.Value;
                        stem += "-o" + (previousNote / 12) + GetNoteHeight(previousNote);
                    }
                }
                else if (context.NamingWay == 1)
                {
                    stem = "o" + (note.NoteNumber / 12) + GetNoteHeight(note.NoteNumber);
                }
                else
                {
                    stem = "NULL";
                }
            }
            return context.Prefix + stem + context.Suffix;
        }

        private static string GetNoteHeight(int noteNumber)
        {
            switch (noteNumber % 12)
            {
                case 0: return "c";
                case 1: return "cp";
                case 2: return "d";
                case 3: return "dp";
                case 4: return "e";
                case 5: return "f";
                case 6: return "fp";
                case 7: return "g";
                case 8: return "gp";
                case 9: return "a";
                case 10: return "ap";
                case 11: return "b";
            }
            return "";
        }
    }
}
