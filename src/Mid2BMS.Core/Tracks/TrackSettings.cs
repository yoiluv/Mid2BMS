using System;
using System.Collections.Generic;
using System.Linq;

namespace Mid2BMS
{
    public enum TrackMode
    {
        Blue,
        Purple,
        Red,
    }

    public sealed record TrackSettings
    {
        public TrackMode Mode { get; init; }
        public bool IsDrums { get; init; }
        public bool IsChord { get; init; }
        public bool IsOneShot { get; init; }
        public bool IsXChain { get; init; }
        public bool Ignore { get; init; }

        public static TrackMode FromLegacyGlobalMode(bool isRedMode, bool isPurpleMode)
        {
            if (isRedMode && isPurpleMode)
                throw new ArgumentException("Red mode and Purple mode cannot both be enabled.");
            return isRedMode ? TrackMode.Red : isPurpleMode ? TrackMode.Purple : TrackMode.Blue;
        }

        internal static IReadOnlyList<TrackSettings> FromLegacyFlags(int trackCount, TrackMode mode,
            IReadOnlyList<bool> isDrums, IReadOnlyList<bool> ignore, IReadOnlyList<bool> isChord,
            IReadOnlyList<bool> isXChain, IReadOnlyList<bool> isOneShot)
        {
            ValidateCount(trackCount, isDrums, nameof(isDrums));
            ValidateCount(trackCount, ignore, nameof(ignore));
            ValidateCount(trackCount, isChord, nameof(isChord));
            ValidateCount(trackCount, isXChain, nameof(isXChain));
            ValidateCount(trackCount, isOneShot, nameof(isOneShot));

            return Enumerable.Range(0, trackCount).Select(index => new TrackSettings
            {
                Mode = mode,
                IsDrums = ValueAt(isDrums, index),
                Ignore = ValueAt(ignore, index),
                IsChord = ValueAt(isChord, index),
                IsXChain = ValueAt(isXChain, index),
                IsOneShot = ValueAt(isOneShot, index),
            }).ToArray();
        }

        internal static IReadOnlyList<TrackSettings> NormalizeForConversion(int trackCount,
            TrackMode defaultMode, IReadOnlyList<TrackSettings> settings, bool sequenceLayer)
        {
            if (settings == null)
                return FromLegacyFlags(trackCount, defaultMode, null, null, null, null, null);
            if (settings.Count != trackCount)
                throw new ArgumentException("TrackSettings count must match the MIDI track count.", nameof(settings));

            TrackSettings[] result = settings.ToArray();
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == null)
                    throw new ArgumentException("TrackSettings contains null at track " + i + ".", nameof(settings));
                if (!Enum.IsDefined(typeof(TrackMode), result[i].Mode))
                    throw new ArgumentException("TrackSettings contains an invalid mode at track " + i + ".", nameof(settings));
            }

            if (result.Any(x => x.Mode == TrackMode.Purple && x.IsChord))
                throw new ArgumentException("Purple tracks cannot use Chord mode.", nameof(settings));
            if (result.Any(x => x.IsXChain && (x.Mode != TrackMode.Red || !sequenceLayer)))
                throw new ArgumentException("XChain requires a Red track with SequenceLayer enabled.", nameof(settings));

            return Array.AsReadOnly(result);
        }

        internal static List<bool> SelectFlags(IReadOnlyList<TrackSettings> settings,
            Func<TrackSettings, bool> selector)
        {
            return settings.Select(selector).ToList();
        }

        internal static string GetModeSuffix(IReadOnlyList<TrackSettings> settings)
        {
            var modes = new List<string>();
            if (settings.Any(x => x.Mode == TrackMode.Blue)) modes.Add("blue");
            if (settings.Any(x => x.Mode == TrackMode.Purple)) modes.Add("purple");
            if (settings.Any(x => x.Mode == TrackMode.Red)) modes.Add("red");
            return string.Join("_", modes);
        }

        private static bool ValueAt(IReadOnlyList<bool> values, int index)
        {
            return values != null && values[index];
        }

        private static void ValidateCount(int trackCount, IReadOnlyList<bool> values, string parameterName)
        {
            if (values != null && values.Count != trackCount)
                throw new ArgumentException(parameterName + " count must match the MIDI track count.", parameterName);
        }
    }
}
