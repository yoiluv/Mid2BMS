using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mid2BMS
{
    /// <summary>
    /// Selects the appropriate per-track algorithm and returns one single-note MIDI
    /// for Blue, Purple, Red, or any supported mixture of those modes.
    /// </summary>
    internal sealed class SingleNoteMidiBuilder
    {
        public MidiStruct Build(Func<Stream> sourceMidiStreamGenerator,
            MelodyWalker melodyWalker, IReadOnlyList<TrackSettings> trackSettings,
            bool sequenceLayer, string marginTimeBeats)
        {
            if (sourceMidiStreamGenerator == null)
                throw new ArgumentNullException(nameof(sourceMidiStreamGenerator));
            if (melodyWalker == null) throw new ArgumentNullException(nameof(melodyWalker));
            if (trackSettings == null) throw new ArgumentNullException(nameof(trackSettings));

            bool hasRed = trackSettings.Any(x => x.Mode == TrackMode.Red);
            if (!hasRed) return melodyWalker.GeneratedSingleNoteMidi;

            var sourceMidi = new MidiStruct(sourceMidiStreamGenerator(), true);
            if (trackSettings.Any(x => x.Mode != TrackMode.Red))
            {
                return new MixedModeMidiBuilder().Build(sourceMidi,
                    melodyWalker.GeneratedSingleNoteMidi,
                    melodyWalker.GeneratedTracksBySourceIndex,
                    trackSettings, sequenceLayer, marginTimeBeats);
            }

            return BuildLegacyRed(sourceMidi, trackSettings, sequenceLayer, marginTimeBeats);
        }

        private static MidiStruct BuildLegacyRed(MidiStruct midi,
            IReadOnlyList<TrackSettings> trackSettings, bool sequenceLayer,
            string marginTimeBeats)
        {
            int marginBeats = (int)Math.Ceiling(Convert.ToDouble(marginTimeBeats));
            MidiTrack.SPLIT_BEATS_INTERVAL = marginBeats + 4;
            MidiTrack.SPLIT_BEATS_AUTOMATIONLEFT = 2;
            MidiTrack.SPLIT_BEATS_AUTOMATIONRIGHT = marginBeats;

            for (int trackIndex = 0; trackIndex < trackSettings.Count; trackIndex++)
            {
                if (trackSettings[trackIndex].Ignore)
                {
                    midi.tracks[trackIndex] = new MidiTrack(
                        midi.tracks[trackIndex].Where(x => !(x is MidiEventNote)));
                }
            }

            if (!sequenceLayer)
            {
                for (int trackIndex = 1; trackIndex < midi.tracks.Count; trackIndex++)
                {
                    midi.tracks[trackIndex] = midi.tracks[trackIndex]
                        .SplitNotes(midi, trackSettings[trackIndex].IsChord);
                }
                return midi;
            }

            try
            {
                IEnumerable<MultiTrackMidiEvent> directSum = MidiTrack.DirectSum(midi.tracks);
                List<bool> chords = TrackSettings.SelectFlags(trackSettings, x => x.IsChord);
                List<bool> xchains = TrackSettings.SelectFlags(trackSettings, x => x.IsXChain);
                IEnumerable<MultiTrackMidiEvent> split = MidiTrack.SplitNotes(
                    directSum, midi, chords, xchains);
                midi.tracks = MidiTrack.DirectDifference(split);
            }
            catch (Exception exception)
            {
                // Preserve the legacy behavior: report the error and export the MIDI state
                // available at that point instead of changing cancellation semantics.
                CoreInteraction.ShowMessage(exception.ToString());
            }
            return midi;
        }
    }
}
