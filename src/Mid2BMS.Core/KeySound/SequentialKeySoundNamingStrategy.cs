using System;
using System.Globalization;
using System.Text;

namespace Mid2BMS
{
    // Numbers key sounds across all processed tracks; unlike WAV IDs, gaps between tracks are not included.
    internal sealed class SequentialKeySoundNamingStrategy : IKeySoundNamingStrategy
    {
        public string GetFileName(KeySoundContext context)
        {
            if (context.GlobalIndex < 1)
                throw new ArgumentOutOfRangeException(nameof(context.GlobalIndex));
            return context.GlobalIndex.ToString("D4", CultureInfo.InvariantCulture) + context.Suffix;
        }
    }

    // Numbers key sounds within a track and uses a filesystem-safe version of the track name.
    internal sealed class TrackSequentialKeySoundNamingStrategy : IKeySoundNamingStrategy
    {
        public string GetFileName(KeySoundContext context)
        {
            if (context.IndexWithinTrack < 0)
                throw new ArgumentOutOfRangeException(nameof(context.IndexWithinTrack));

            string trackName = GetSafeTrackName(context.TrackName, context.TrackId);
            if (context.DisambiguateTrackName)
                trackName += "_t" + (context.TrackId + 1).ToString("D2", CultureInfo.InvariantCulture);
            return trackName + "_" + (context.IndexWithinTrack + 1).ToString("D3", CultureInfo.InvariantCulture)
                + context.Suffix;
        }

        internal static string GetSafeTrackName(string trackName, int trackId)
        {
            var result = new StringBuilder();
            foreach (char ch in trackName ?? "")
            {
                result.Append(ch < 32 || "<>:\"/\\|?*".IndexOf(ch) >= 0 ? '_' : ch);
            }
            string safeName = result.ToString().Trim().TrimEnd('.');
            if (safeName.Length == 0)
                return "Track" + (trackId + 1).ToString(CultureInfo.InvariantCulture);
            // WaveSplitter reserves this prefix for a silent placeholder, not an output file.
            return safeName.StartsWith("____dummy_", StringComparison.Ordinal)
                ? "Track_" + safeName : safeName;
        }
    }
}
