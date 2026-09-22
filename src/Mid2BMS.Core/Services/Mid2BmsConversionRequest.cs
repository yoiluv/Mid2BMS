using System;
using System.Collections.Generic;
using System.Linq;

namespace Mid2BMS
{
    public sealed record Mid2BmsConversionRequest
    {
        public TrackMode DefaultTrackMode { get; init; } = TrackMode.Blue;
        public IReadOnlyList<TrackSettings> TrackSettings { get; init; }
        public IReadOnlyList<string> TrackNames { get; init; }
        public bool CreateExtraFiles { get; init; }
        public bool LookAtInstrumentName { get; init; }
        public string MarginTimeBeats { get; init; } = "12.0";
        public int WavIdSpacing { get; init; }
        public bool SequenceLayer { get; init; }
        public int NewTimebase { get; init; }
        public int VelocityStep { get; init; } = 1;
        public int StartingWavId { get; init; } = 1;
        public int StartingBmsChannelIndex { get; init; }

        internal IReadOnlyList<TrackSettings> ResolveTrackSettings(int trackCount)
        {
            Validate();
            return global::Mid2BMS.TrackSettings.NormalizeForConversion(
                trackCount, DefaultTrackMode, TrackSettings, SequenceLayer);
        }

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(TrackMode), DefaultTrackMode))
                throw new ArgumentException("DefaultTrackMode is invalid.", nameof(DefaultTrackMode));
            if (MarginTimeBeats == null)
                throw new ArgumentNullException(nameof(MarginTimeBeats));
            Convert.ToDouble(MarginTimeBeats);
            if (TrackNames != null && TrackNames.Any(x => x == null))
                throw new ArgumentException("TrackNames cannot contain null.", nameof(TrackNames));
        }
    }

    public sealed class Mid2BmsConversionResult
    {
        internal Mid2BmsConversionResult(bool cancelled, string trackCsv,
            IEnumerable<string> trackNames, IEnumerable<string> instrumentNames,
            int nextWavId, int nextBmsChannelIndex,
            IEnumerable<TrackSettings> trackSettings, string modeSuffix,
            string singleNoteMidiPath, string bmsPath, string keySoundManifestPath)
        {
            Cancelled = cancelled;
            TrackCsv = trackCsv;
            TrackNames = Array.AsReadOnly((trackNames ?? Array.Empty<string>()).ToArray());
            InstrumentNames = Array.AsReadOnly((instrumentNames ?? Array.Empty<string>()).ToArray());
            NextWavId = nextWavId;
            NextBmsChannelIndex = nextBmsChannelIndex;
            TrackSettings = Array.AsReadOnly((trackSettings ?? Array.Empty<TrackSettings>()).ToArray());
            ModeSuffix = modeSuffix;
            SingleNoteMidiPath = singleNoteMidiPath;
            BmsPath = bmsPath;
            KeySoundManifestPath = keySoundManifestPath;
        }

        public bool Cancelled { get; }
        public string TrackCsv { get; }
        public IReadOnlyList<string> TrackNames { get; }
        public IReadOnlyList<string> InstrumentNames { get; }
        public int NextWavId { get; }
        public int NextBmsChannelIndex { get; }
        public IReadOnlyList<TrackSettings> TrackSettings { get; }
        public string ModeSuffix { get; }
        public string SingleNoteMidiPath { get; }
        public string BmsPath { get; }
        public string KeySoundManifestPath { get; }

        internal static Mid2BmsConversionResult CancelledResult(Mid2BmsConversionRequest request)
        {
            return new Mid2BmsConversionResult(true, null, null, null,
                request.StartingWavId, request.StartingBmsChannelIndex,
                null, null, null, null, null);
        }
    }
}
