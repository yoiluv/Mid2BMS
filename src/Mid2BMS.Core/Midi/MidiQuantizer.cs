using System;
using System.Diagnostics;
using System.IO;

namespace Mid2BMS
{
    internal static class MidiQuantizer
    {
        public static void ChangeMidiTimebase(Stream inStream, Stream outStream, long newTimeBase)
        {
            MidiStruct ms = new MidiStruct(inStream, true);

            Debug.Assert(ms.resolution != null);
            long oldTimeBase = ms.resolution ?? 480;  // この480って必要？

            for (int i = 0; i < ms.tracks.Count; i++)
            {
                MidiTrack mt = ms.tracks[i];

                for (int j = 0; j < mt.Count; j++)
                {
                    // 切り捨てではなく四捨五入に修正
                    int tick_old = mt[j].tick;

                    mt[j].tick = (int)Math.Round((tick_old * newTimeBase) / (double)oldTimeBase);  // 切り捨て
                    if (mt[j] is MidiEventNote)
                    {
                        MidiEventNote me = (MidiEventNote)mt[j];
                        me.q = (int)Math.Round(((tick_old + me.q) * newTimeBase) / (double)oldTimeBase) - mt[j].tick;  // 切り捨て
                        me.q = Math.Max(1, me.q);  // ただし1以上
                    }
                }
            }

            ms.resolution = (int)newTimeBase;

            ms.Export(outStream, true);

            inStream.Close();
        }

        public static void QuantizeVelocity(Stream inStream, Stream outStream, int velocityStep)
        {
            if (velocityStep < 1) throw new Exception("Velocity Quantization Interval は 1以上である必要があります");

            Stream rf = inStream;

            MidiStruct ms = new MidiStruct(rf, true);

            foreach (MidiTrack mt in ms.tracks)
            {
                foreach (MidiEvent me_ in mt)
                {
                    MidiEventNote me = me_ as MidiEventNote;
                    if (me != null)
                    {
                        if (me.v >= 1)
                        {
                            me.v = ((int)Math.Round((double)me.v / (double)velocityStep)) * velocityStep;
                            if (me.v < 1) me.v = 1;
                            else if (me.v > 127) me.v = 127;
                        }
                    }
                }
            }

            ms.Export(outStream, true);

            rf.Close();
        }
    }
}
