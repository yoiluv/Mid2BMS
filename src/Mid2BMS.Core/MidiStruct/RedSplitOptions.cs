using System;

namespace Mid2BMS
{
    internal sealed class RedSplitOptions
    {
        public RedSplitOptions(float intervalBeats, float automationLeftBeats,
            float automationRightBeats)
        {
            if (intervalBeats < 0) throw new ArgumentOutOfRangeException(nameof(intervalBeats));
            if (automationLeftBeats < 0) throw new ArgumentOutOfRangeException(nameof(automationLeftBeats));
            if (automationRightBeats < 0) throw new ArgumentOutOfRangeException(nameof(automationRightBeats));
            IntervalBeats = intervalBeats;
            AutomationLeftBeats = automationLeftBeats;
            AutomationRightBeats = automationRightBeats;
        }

        public float IntervalBeats { get; }
        public float AutomationLeftBeats { get; }
        public float AutomationRightBeats { get; }

        public static RedSplitOptions FromLegacyStatics()
        {
            return new RedSplitOptions(MidiTrack.SPLIT_BEATS_INTERVAL,
                MidiTrack.SPLIT_BEATS_AUTOMATIONLEFT,
                MidiTrack.SPLIT_BEATS_AUTOMATIONRIGHT);
        }
    }
}
