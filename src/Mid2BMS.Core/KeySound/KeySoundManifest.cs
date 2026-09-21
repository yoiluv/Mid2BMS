using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mid2BMS
{
    internal enum KeySoundMode { Unknown, Blue, Purple, Red }

    // A snapshot of the MIDI-derived information that identifies one note in a key sound.
    internal sealed record KeySoundNoteIdentity(int NoteNumber, int Velocity, long LengthNumerator,
        long LengthDenominator, long? StartNumerator, long? StartDenominator,
        int? PreviousNoteNumber)
    {
        public static KeySoundNoteIdentity FromMNote(MNote note)
        {
            return new KeySoundNoteIdentity(note.n, note.v, note.l.n, note.l.d,
                note.t == null ? null : (long?)note.t.n,
                note.t == null ? null : (long?)note.t.d,
                note.prev == null ? null : (int?)note.prev.n);
        }
    }

    internal sealed class KeySound
    {
        public int Id { get; }
        public int TrackId { get; }
        public KeySoundMode Mode { get; }
        public bool IsChord { get; }
        public bool IsOneShot { get; }
        public int? WavId { get; }
        public string FileName { get; }
        public string BmsFileName { get; }
        public IReadOnlyList<KeySoundNoteIdentity> Identity { get; }

        public KeySound(int id, int trackId, KeySoundMode mode, bool isChord, bool isOneShot,
            int? wavId, string fileName, string bmsFileName, IEnumerable<KeySoundNoteIdentity> identity)
        {
            Id = id;
            TrackId = trackId;
            Mode = mode;
            IsChord = isChord;
            IsOneShot = isOneShot;
            WavId = wavId;
            FileName = fileName;
            BmsFileName = bmsFileName;
            Identity = Array.AsReadOnly(identity.ToArray());
        }
    }

    internal sealed class KeySoundTrack
    {
        private readonly string[] waveSplitNames;

        public int TrackId { get; }
        public string InputPrefix { get; }
        public string InputSuffix { get; }
        public string OriginalIndex { get; }
        public bool HasLegacyRow { get; }
        public IReadOnlyList<KeySound> KeySounds { get; }

        public KeySoundTrack(int trackId, string inputPrefix, string inputSuffix, string originalIndex,
            IEnumerable<string> waveSplitNames, IEnumerable<KeySound> keySounds, bool hasLegacyRow)
        {
            TrackId = trackId;
            InputPrefix = inputPrefix;
            InputSuffix = inputSuffix;
            OriginalIndex = originalIndex;
            this.waveSplitNames = waveSplitNames.ToArray();
            KeySounds = Array.AsReadOnly(keySounds.ToArray());
            HasLegacyRow = hasLegacyRow;

            string[] actualNames = this.waveSplitNames.Where(name => !IsDummy(name)).ToArray();
            if (actualNames.Length != KeySounds.Count ||
                actualNames.Where((name, id) => name != KeySounds[id].FileName).Any())
                throw new InvalidOperationException("Manifest key sounds do not match WaveSplitter slots.");
        }

        public string[] ToLegacyRow()
        {
            return new[] { InputPrefix, InputSuffix, OriginalIndex }.Concat(waveSplitNames).ToArray();
        }

        public int RequiredWaveFileCount => waveSplitNames.Count(name => !IsDummy(name));

        internal static bool IsDummy(string fileName)
        {
            return fileName.Length >= 10 && fileName.Substring(0, 10) == "____dummy_";
        }
    }

    // One explicit mapping is projected to both BMS #WAV definitions and the legacy WaveSplitter rows.
    internal sealed class KeySoundManifest
    {
        private readonly List<KeySoundTrack> tracks = new List<KeySoundTrack>();
        public IReadOnlyList<KeySoundTrack> Tracks => tracks.AsReadOnly();
        public int KeySoundCount => tracks.Sum(track => track.KeySounds.Count);

        public void AssertUniqueOutputFileNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeySound keySound in tracks.SelectMany(track => track.KeySounds))
            {
                if (!names.Add(keySound.FileName))
                    throw new InvalidOperationException("Key sound filename is used more than once: " + keySound.FileName);
            }
        }

        public KeySoundTrack AddGeneratedTrack(int trackId, KeySoundMode mode, bool isChord, bool isOneShot,
            int firstWavId, IReadOnlyList<string> bmsFileNames, string renamerText,
            IReadOnlyList<IReadOnlyList<MNote>> identities)
        {
            if (bmsFileNames.Count != identities.Count)
                throw new InvalidOperationException("Key sound identity count differs from BMS name count.");

            string[] row = ParseRows(renamerText).SingleOrDefault();
            string[] waveNames = row == null ? Array.Empty<string>() : row.Skip(3).ToArray();
            string[] outputNames = waveNames.Where(name => !KeySoundTrack.IsDummy(name)).ToArray();
            if (outputNames.Length != bmsFileNames.Count)
                throw new InvalidOperationException("WaveSplitter and BMS key sound counts differ.");

            var keySounds = new List<KeySound>();
            for (int i = 0; i < outputNames.Length; i++)
            {
                if (!String.Equals(outputNames[i], bmsFileNames[i], StringComparison.Ordinal))
                    throw new InvalidOperationException("WaveSplitter and BMS key sound filenames differ.");
                KeySoundNoteIdentity[] notes = identities[i].Select(KeySoundNoteIdentity.FromMNote).ToArray();
                keySounds.Add(new KeySound(i, trackId, mode, isChord, isOneShot,
                    firstWavId + i, outputNames[i], bmsFileNames[i], notes));
            }

            var track = new KeySoundTrack(trackId, row == null ? "" : row[0],
                row == null ? "" : row[1], row == null ? "1" : row[2],
                waveNames, keySounds, row != null);
            tracks.Add(track);
            return track;
        }

        public static KeySoundManifest FromLegacyRenamerText(string text)
        {
            var manifest = new KeySoundManifest();
            foreach (string[] row in ParseRows(text))
            {
                string[] waveNames = row.Skip(3).ToArray();
                var keySounds = waveNames.Where(name => !KeySoundTrack.IsDummy(name))
                    .Select((name, id) => new KeySound(id, -1, KeySoundMode.Unknown, false, false,
                        null, name, name, Array.Empty<KeySoundNoteIdentity>())).ToArray();
                manifest.tracks.Add(new KeySoundTrack(-1, row[0], row[1], row[2],
                    waveNames, keySounds, true));
            }
            return manifest;
        }

        public string ToLegacyRenamerText()
        {
            var result = new StringBuilder();
            foreach (KeySoundTrack track in tracks.Where(track => track.HasLegacyRow))
            {
                foreach (string value in track.ToLegacyRow()) result.Append(value).Append("\r\n");
                result.Append("//\r\n");
            }
            return result.ToString();
        }

        public string[][] ToLegacyWaveRenamerRows()
        {
            return tracks.Where(track => track.HasLegacyRow).Select(track => track.ToLegacyRow()).ToArray();
        }

        private static string[][] ParseRows(string text)
        {
            if (text.Length == 0) return Array.Empty<string[]>();
            string[][] rows = TextTransaction.SplitString(text, "\r\n", "//", StringSplitOptions.RemoveEmptyEntries);
            if (rows.Any(row => row.Length < 3))
                throw new InvalidOperationException("Invalid legacy WaveSplitter row.");
            return rows;
        }
    }
}
