using System;
using System.Collections.Generic;
using System.Linq;

namespace Mid2BMS
{
    /// <summary>
    /// Combines the synthesized Blue/Purple key-sound tracks with Red tracks
    /// reconstructed from the source MIDI and its automation events.
    /// </summary>
    internal sealed class MixedModeMidiBuilder
    {
        public MidiStruct Build(MidiStruct sourceMidi, MidiStruct generatedMidi,
            IReadOnlyList<MidiTrack> generatedTracksBySourceIndex,
            IReadOnlyList<TrackSettings> trackSettings, bool sequenceLayer,
            string marginTimeBeats)
        {
            if (sourceMidi == null) throw new ArgumentNullException(nameof(sourceMidi));
            if (generatedMidi == null) throw new ArgumentNullException(nameof(generatedMidi));
            if (generatedTracksBySourceIndex == null)
                throw new ArgumentNullException(nameof(generatedTracksBySourceIndex));
            if (trackSettings == null) throw new ArgumentNullException(nameof(trackSettings));
            if (sourceMidi.tracks.Count != trackSettings.Count ||
                generatedTracksBySourceIndex.Count != trackSettings.Count)
                throw new ArgumentException("MIDI tracks and TrackSettings must have the same count.");

            int sourceResolution = sourceMidi.resolution ?? 480;
            int outputResolution = generatedMidi.resolution ?? 3840;
            int marginBeats = (int)Math.Ceiling(Convert.ToDouble(marginTimeBeats));
            var splitOptions = new RedSplitOptions(marginBeats + 4, 2, marginBeats);

            MidiStruct redSource = CreateRedSource(sourceMidi, trackSettings);
            List<MidiTrack> splitRedTracks = sequenceLayer
                ? SplitRedSequence(redSource, trackSettings, splitOptions)
                : SplitRedTracks(redSource, trackSettings, splitOptions);

            while (splitRedTracks.Count < trackSettings.Count)
                splitRedTracks.Add(new MidiTrack());

            var result = new MidiStruct(outputResolution);
            for (int i = 0; i < trackSettings.Count; i++)
            {
                MidiTrack track;
                if (trackSettings[i].Mode == TrackMode.Red &&
                    !(sequenceLayer && trackSettings[i].IsXChain))
                {
                    track = ScaleTrack(splitRedTracks[i], sourceResolution, outputResolution);
                }
                else if (generatedTracksBySourceIndex[i] != null)
                {
                    track = CloneTrack(generatedTracksBySourceIndex[i]);
                }
                else
                {
                    track = ScaleTrack(MetadataOnly(sourceMidi.tracks[i]), sourceResolution, outputResolution);
                }
                result.tracks.Add(track);
            }

            if (sequenceLayer)
                ArrangeSequenceLayer(result, splitRedTracks, sourceResolution, outputResolution,
                    trackSettings, marginBeats);

            for (int i = 0; i < result.tracks.Count; i++)
                result.tracks[i] = new MidiTrack(result.tracks[i].OrderBy(x => x.tick));
            return result;
        }

        private static MidiStruct CreateRedSource(MidiStruct source,
            IReadOnlyList<TrackSettings> settings)
        {
            var result = new MidiStruct(source.resolution ?? 480);
            for (int trackIndex = 0; trackIndex < source.tracks.Count; trackIndex++)
            {
                TrackSettings trackSettings = settings[trackIndex];
                IEnumerable<MidiEvent> events = source.tracks[trackIndex];
                if (trackSettings.Mode != TrackMode.Red)
                {
                    // Tempo and structural metadata may be needed while Red automation is clipped.
                    events = events.Where(IsStructuralMetadata);
                }
                else if (trackSettings.Ignore)
                {
                    events = events.Where(x => !(x is MidiEventNote));
                }
                result.tracks.Add(new MidiTrack(events.Select(x => x.Clone())));
            }
            return result;
        }

        private static List<MidiTrack> SplitRedTracks(MidiStruct source,
            IReadOnlyList<TrackSettings> settings, RedSplitOptions options)
        {
            var result = source.tracks
                .Select(track => new MidiTrack(track.Where(IsStructuralMetadata).Select(x => x.Clone())))
                .ToList();
            for (int i = 0; i < settings.Count; i++)
            {
                if (settings[i].Mode == TrackMode.Red)
                    result[i] = source.tracks[i].SplitNotes(source, settings[i].IsChord, options);
            }
            return result;
        }

        private static List<MidiTrack> SplitRedSequence(MidiStruct source,
            IReadOnlyList<TrackSettings> settings, RedSplitOptions options)
        {
            List<bool> chords = settings.Select(x => x.Mode == TrackMode.Red && x.IsChord).ToList();
            List<bool> xchains = settings.Select(x => x.Mode == TrackMode.Red && x.IsXChain).ToList();
            IEnumerable<MultiTrackMidiEvent> split = MidiTrack.SplitNotes(
                MidiTrack.DirectSum(source.tracks), source, chords, xchains, options);
            return MidiTrack.DirectDifference(split);
        }

        private static void ArrangeSequenceLayer(MidiStruct result,
            IReadOnlyList<MidiTrack> unscaledSplitRedTracks, int sourceResolution,
            int outputResolution, IReadOnlyList<TrackSettings> settings, int marginBeats)
        {
            int cursor = 16 * outputResolution;
            int bluePurpleTrackGap = 16 * outputResolution;
            int bluePurpleMargin = marginBeats * outputResolution;
            int redInterval = (marginBeats + 4) * outputResolution;
            var redAnchors = new List<TimeAnchor>();

            for (int trackIndex = 0; trackIndex < settings.Count; trackIndex++)
            {
                TrackSettings trackSettings = settings[trackIndex];
                if (trackSettings.Ignore ||
                    (trackSettings.Mode == TrackMode.Red && trackSettings.IsXChain))
                    continue;

                MidiTrack track = result.tracks[trackIndex];
                List<MidiEventNote> notes = track.OfType<MidiEventNote>()
                    .OrderBy(x => x.tick).ToList();
                if (notes.Count == 0) continue;

                int sourceFirstTick = notes[0].tick;
                int sourceLastEnd = notes.Max(x => x.tick + x.q);
                int[] sourceNoteTicks = notes.Select(x => x.tick).Distinct().ToArray();
                int offset = cursor - sourceFirstTick;
                ShiftMusicalEvents(track, offset);

                int lastEnd = sourceLastEnd + offset;
                if (trackSettings.Mode == TrackMode.Red)
                {
                    foreach (int noteTick in sourceNoteTicks)
                        redAnchors.Add(new TimeAnchor(noteTick, noteTick + offset));
                    cursor = lastEnd + redInterval;
                }
                else
                {
                    cursor = lastEnd + bluePurpleMargin + bluePurpleTrackGap;
                }
            }

            if (redAnchors.Count == 0) return;

            // Sequence-layer XChain events and clipped tempo events retain their original
            // track IDs. Move each event with the Red note segment nearest to it.
            for (int trackIndex = 0; trackIndex < settings.Count; trackIndex++)
            {
                if (settings[trackIndex].Mode == TrackMode.Red && !settings[trackIndex].IsXChain)
                    continue;

                MidiTrack supportTrack = trackIndex < unscaledSplitRedTracks.Count
                    ? ScaleTrack(unscaledSplitRedTracks[trackIndex], sourceResolution, outputResolution)
                    : new MidiTrack();
                foreach (MidiEvent sourceEvent in supportTrack)
                {
                    if (IsStationaryMetadata(sourceEvent)) continue;
                    MidiEvent moved = sourceEvent.Clone();
                    TimeAnchor anchor = redAnchors
                        .OrderBy(x => Math.Abs((long)x.SourceTick - moved.tick)).First();
                    moved.tick += anchor.TargetTick - anchor.SourceTick;
                    result.tracks[trackIndex].Add(moved);
                }
            }
        }

        private static void ShiftMusicalEvents(IEnumerable<MidiEvent> events, int offset)
        {
            foreach (MidiEvent midiEvent in events)
            {
                if (!IsStationaryMetadata(midiEvent)) midiEvent.tick += offset;
            }
        }

        private static bool IsStructuralMetadata(MidiEvent midiEvent)
        {
            MidiEventMeta meta = midiEvent as MidiEventMeta;
            if (meta == null) return false;
            return meta.id == 0x03 || meta.id == 0x04 || meta.id == 0x05 ||
                meta.id == 0x21 || meta.id == 0x51 || meta.id == 0x58 || meta.id == 0x59;
        }

        private static bool IsStationaryMetadata(MidiEvent midiEvent)
        {
            MidiEventMeta meta = midiEvent as MidiEventMeta;
            return meta != null && meta.id != 0x51;
        }

        private static MidiTrack MetadataOnly(IEnumerable<MidiEvent> track)
        {
            return new MidiTrack(track.Where(IsStructuralMetadata).Select(x => x.Clone()));
        }

        private static MidiTrack CloneTrack(IEnumerable<MidiEvent> track)
        {
            return new MidiTrack(track.Select(x => x.Clone()));
        }

        private static MidiTrack ScaleTrack(IEnumerable<MidiEvent> track,
            int sourceResolution, int outputResolution)
        {
            var result = new MidiTrack();
            foreach (MidiEvent sourceEvent in track)
            {
                MidiEvent copy = sourceEvent.Clone();
                copy.tick = Scale(copy.tick, sourceResolution, outputResolution);
                MidiEventNote note = copy as MidiEventNote;
                if (note != null)
                    note.q = Math.Max(1, Scale(note.q, sourceResolution, outputResolution));
                result.Add(copy);
            }
            return result;
        }

        private static int Scale(int value, int sourceResolution, int outputResolution)
        {
            return (int)Math.Round((double)value * outputResolution / sourceResolution,
                MidpointRounding.AwayFromZero);
        }

        private sealed class TimeAnchor
        {
            public TimeAnchor(int sourceTick, int targetTick)
            {
                SourceTick = sourceTick;
                TargetTick = targetTick;
            }

            public int SourceTick { get; }
            public int TargetTick { get; }
        }
    }
}
