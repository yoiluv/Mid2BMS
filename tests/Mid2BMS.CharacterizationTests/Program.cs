using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Mid2BMS.CharacterizationTests
{
    internal static class Program
    {
        private const string SettingsFileName = "fixture.properties";
        private const string InputFileName = "input.mid";

        private static int Main(string[] args)
        {
            CoreInteraction.Current = new CharacterizationInteraction();
            bool accept = args.Any(x => String.Equals(x, "--accept", StringComparison.OrdinalIgnoreCase));
            string repositoryRoot = GetArgumentValue(args, "--repository-root") ?? FindRepositoryRoot();
            string fixturesRoot = Path.Combine(repositoryRoot, "tests", "fixtures");
            string temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "Mid2BMS.CharacterizationTests",
                Guid.NewGuid().ToString("N"));
            string originalCurrentDirectory = Environment.CurrentDirectory;
            var failures = new List<string>();

            try
            {
                Directory.CreateDirectory(temporaryRoot);
                Environment.CurrentDirectory = temporaryRoot;
                File.WriteAllText(Path.Combine(temporaryRoot, "encoding.ini"), "Shift_JIS", Encoding.ASCII);
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");

                string[] fixtureDirectories = Directory.GetDirectories(fixturesRoot)
                    .Where(x => File.Exists(Path.Combine(x, SettingsFileName)))
                    .OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal)
                    .ToArray();

                if (fixtureDirectories.Length == 0)
                {
                    Console.Error.WriteLine("No fixtures were found under: " + fixturesRoot);
                    return 1;
                }

                foreach (string fixtureDirectory in fixtureDirectories)
                {
                    string fixtureName = Path.GetFileName(fixtureDirectory);
                    try
                    {
                        RunFixture(fixtureName, fixtureDirectory, temporaryRoot, accept);
                        Console.WriteLine((accept ? "GENERATED" : "PASSED   ") + " " + fixtureName);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(fixtureName + ": " + exception.Message);
                        Console.Error.WriteLine("FAILED   " + fixtureName);
                        Console.Error.WriteLine(exception.ToString());
                    }
                }

                if (!accept)
                {
                    RunNamingScenario(fixturesRoot, temporaryRoot, "blue_basic",
                        new SequentialKeySoundNamingStrategy(), "sequential_blue", true, false, failures,
                        startingWavId: 17, wavidSpacing: 3, verifyWaveOutput: true);
                    RunNamingScenario(fixturesRoot, temporaryRoot, "blue_basic",
                        new TrackSequentialKeySoundNamingStrategy(), "track_sequential_blue", false, false, failures);
                    RunNamingScenario(fixturesRoot, temporaryRoot, "purple_portamento",
                        new SequentialKeySoundNamingStrategy(), "sequential_purple", true, true, failures);
                    RunNamingScenario(fixturesRoot, temporaryRoot, "chord",
                        new TrackSequentialKeySoundNamingStrategy(), "track_sequential_chord", false, false, failures);
                    RunNamingScenario(fixturesRoot, temporaryRoot, "red_automation",
                        new SequentialKeySoundNamingStrategy(), "sequential_red", true, false, failures);
                    RunNamingScenario(fixturesRoot, temporaryRoot, "blue_basic",
                        new TrackSequentialKeySoundNamingStrategy(), "track_sequential_duplicate",
                        false, false, failures, duplicateTrackNames: true);
                }

                // Generate every fixture successfully before replacing any checked-in baseline.
                if (accept && failures.Count == 0)
                {
                    foreach (string fixtureDirectory in fixtureDirectories)
                    {
                        string fixtureName = Path.GetFileName(fixtureDirectory);
                        string workDirectory = Path.Combine(temporaryRoot, fixtureName);
                        string[] actualFiles = GetActualFiles(workDirectory);
                        AcceptActualFiles(actualFiles, Path.Combine(fixtureDirectory, "expected"));
                        Console.WriteLine("ACCEPTED  " + fixtureName);
                    }
                }
            }
            finally
            {
                Environment.CurrentDirectory = originalCurrentDirectory;
            }

            if (failures.Count == 0)
            {
                Directory.Delete(temporaryRoot, true);
                Console.WriteLine();
                Console.WriteLine("All characterization fixtures passed.");
                return 0;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(failures.Count + " fixture(s) failed. Actual outputs remain at:");
            Console.Error.WriteLine(temporaryRoot);
            foreach (string failure in failures)
            {
                Console.Error.WriteLine("- " + failure);
            }
            return 1;
        }

        private sealed class CharacterizationInteraction : ICoreInteraction
        {
            public void ShowMessage(string message)
            {
                Console.Error.WriteLine("Core notice: " + message);
            }

            public void ShowMessage(string message, string caption)
            {
                Console.Error.WriteLine("Core notice (" + caption + "): " + message);
            }

            public bool ConfirmAbort(string message, string caption)
            {
                Console.Error.WriteLine("Core confirmation (continuing): " + message);
                return false;
            }
        }

        private static string RunFixture(string fixtureName, string fixtureDirectory, string temporaryRoot, bool accept,
            IKeySoundNamingStrategy namingStrategy = null, string workName = null, bool compareGolden = true,
            int? startingWavId = null, int? wavidSpacing = null, bool duplicateTrackNames = false)
        {
            IDictionary<string, string> settings = ReadSettings(Path.Combine(fixtureDirectory, SettingsFileName));
            string sourceInputPath = Path.Combine(fixtureDirectory, InputFileName);
            string workDirectory = Path.Combine(temporaryRoot, workName ?? fixtureName);
            string actualInputPath = Path.Combine(workDirectory, InputFileName);

            if (!File.Exists(sourceInputPath))
            {
                throw new FileNotFoundException("Fixture input is missing.", sourceInputPath);
            }

            Directory.CreateDirectory(workDirectory);
            File.Copy(sourceInputPath, actualInputPath, true);

            int trackCount;
            using (var inputStream = new FileStream(actualInputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                trackCount = new MidiStruct(inputStream).tracks.Count;
            }

            string mode = GetRequiredSetting(settings, "mode").ToLowerInvariant();
            bool isRedMode = mode == "red";
            bool isPurpleMode = mode == "purple";
            if (!isRedMode && !isPurpleMode && mode != "blue")
            {
                throw new InvalidDataException("Unsupported mode: " + mode);
            }

            List<bool> isDrumsList = BuildTrackFlags(GetRequiredSetting(settings, "drumTracks"), trackCount);
            List<bool> isChordList = BuildTrackFlags(GetRequiredSetting(settings, "chordTracks"), trackCount);
            List<bool> isXChainList = BuildTrackFlags(GetRequiredSetting(settings, "xChainTracks"), trackCount);
            List<bool> isOneShotList = BuildTrackFlags(GetRequiredSetting(settings, "oneShotTracks"), trackCount);

            var target = new MyForm();
            target.PathBase = workDirectory + Path.DirectorySeparatorChar;
            target.FileName_MidiFile = InputFileName;
            target.NamingStrategy = namingStrategy ?? new LegacyKeySoundNamingStrategy();

            int vacantWavid = startingWavId ?? ParseInt(settings, "vacantWavid");
            int vacantBmsChannelIndex = ParseInt(settings, "vacantBmsChannelIndex");
            string trackCsv;
            List<string> midiTrackNames = duplicateTrackNames
                ? Enumerable.Range(0, trackCount).Select(i => i == 1 || i == 2 ? "Piano" : "Track" + i).ToList()
                : null;
            List<string> midiInstrumentNames;
            double progressValue = 0.0;
            bool progressFinished = false;

            target.Mid2BMS_Process(
                isRedMode,
                isPurpleMode,
                ParseBool(settings, "createExtraFiles"),
                ref vacantWavid,
                ref vacantBmsChannelIndex,
                ParseBool(settings, "lookAtInstrumentName"),
                GetRequiredSetting(settings, "marginTimeBeats"),
                wavidSpacing ?? ParseInt(settings, "wavidSpacing"),
                out trackCsv,
                ref midiTrackNames,
                out midiInstrumentNames,
                isDrumsList,
                null,
                isChordList,
                isXChainList,
                isOneShotList,
                ParseBool(settings, "sequenceLayer"),
                ParseInt(settings, "newTimebase"),
                ParseInt(settings, "velocityStep"),
                ref progressValue,
                ref progressFinished);

            if (!progressFinished || progressValue != 1.0)
            {
                throw new InvalidOperationException("The legacy pipeline did not report completion.");
            }
            if (trackCsv == null || midiTrackNames == null || midiInstrumentNames == null)
            {
                throw new InvalidOperationException("The legacy pipeline did not return its track metadata.");
            }

            string[] actualFiles = GetActualFiles(workDirectory);
            string expectedDirectory = Path.Combine(fixtureDirectory, "expected");

            if (accept || !compareGolden)
            {
                return workDirectory;
            }

            CompareWithExpected(actualFiles, expectedDirectory);
            return workDirectory;
        }

        private static void RunNamingScenario(string fixturesRoot, string temporaryRoot, string fixtureName,
            IKeySoundNamingStrategy namingStrategy, string scenarioName, bool sequential, bool purple,
            List<string> failures, int? startingWavId = null, int? wavidSpacing = null,
            bool duplicateTrackNames = false, bool verifyWaveOutput = false)
        {
            try
            {
                string workDirectory = RunFixture(fixtureName, Path.Combine(fixturesRoot, fixtureName),
                    temporaryRoot, false, namingStrategy, scenarioName, false, startingWavId, wavidSpacing,
                    duplicateTrackNames);
                KeySoundManifest manifest = KeySoundManifest.FromLegacyRenamerText(
                    FileIO.ReadAllText(Path.Combine(workDirectory, "text5_renamer_array.txt")));
                string[] waveNames = manifest.Tracks.SelectMany(track => track.KeySounds)
                    .Select(key => key.FileName).ToArray();
                if (waveNames.Length < 2 ||
                    waveNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != waveNames.Length)
                    throw new InvalidDataException("New naming generated too few key sounds or duplicate filenames.");

                string bmsFile = Directory.GetFiles(workDirectory, "text6_bms_*.txt").Single();
                string[] wavDefinitions = FileIO.ReadAllText(bmsFile)
                    .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => line.StartsWith("#WAV", StringComparison.Ordinal)).ToArray();
                string[] bmsNames = wavDefinitions.Select(line => line.Substring(line.IndexOf(' ') + 1)).ToArray();
                if (!waveNames.SequenceEqual(bmsNames, StringComparer.Ordinal))
                    throw new InvalidDataException("BMS #WAV names differ from WaveSplitter filenames.");

                int nextWavId = startingWavId ?? 1;
                int[] expectedIds = manifest.Tracks.SelectMany(track =>
                {
                    int[] ids = Enumerable.Range(nextWavId, track.KeySounds.Count).ToArray();
                    if (track.KeySounds.Count > 0) nextWavId += track.KeySounds.Count + (wavidSpacing ?? 0);
                    return ids;
                }).ToArray();
                int[] actualIds = wavDefinitions.Select(line => BMSParser.IntFromHex36(line.Substring(4, 2))).ToArray();
                if (!actualIds.SequenceEqual(expectedIds))
                    throw new InvalidDataException("BMS WAV IDs no longer match the manifest track order and spacing.");

                if (sequential)
                {
                    string[] expected = Enumerable.Range(1, waveNames.Length)
                        .Select(number => number.ToString("D4", CultureInfo.InvariantCulture) + ".wav").ToArray();
                    if (!waveNames.SequenceEqual(expected, StringComparer.Ordinal))
                        throw new InvalidDataException("Sequential filenames are not consecutive across tracks.");
                }
                else
                {
                    int disambiguatedTrackCount = 0;
                    foreach (KeySoundTrack track in manifest.Tracks)
                    {
                        for (int i = 0; i < track.KeySounds.Count; i++)
                        {
                            string sequenceSuffix = "_" + (i + 1).ToString("D3", CultureInfo.InvariantCulture) + ".wav";
                            string fileName = track.KeySounds[i].FileName;
                            if (duplicateTrackNames && track.InputPrefix == "Piano")
                            {
                                if (!fileName.StartsWith("Piano_t", StringComparison.Ordinal) ||
                                    !fileName.EndsWith(sequenceSuffix, StringComparison.Ordinal))
                                    throw new InvalidDataException("Duplicate track name was not disambiguated.");
                                if (i == 0) disambiguatedTrackCount++;
                            }
                            else if (fileName != track.InputPrefix + sequenceSuffix)
                                throw new InvalidDataException("Track-sequential filenames do not restart for each track.");
                        }
                    }
                    if (duplicateTrackNames && disambiguatedTrackCount < 2)
                        throw new InvalidDataException("Duplicate-name scenario did not exercise two tracks.");
                }

                if (purple)
                {
                    foreach (KeySoundTrack track in manifest.Tracks)
                    {
                        string[] slots = track.ToLegacyRow().Skip(3).ToArray();
                        if (slots.Length != track.KeySounds.Count * 2)
                            throw new InvalidDataException("Purple dummy slots were lost.");
                        for (int i = 0; i < track.KeySounds.Count; i++)
                        {
                            if (slots[i * 2] != "____dummy_" + track.KeySounds[i].FileName ||
                                slots[i * 2 + 1] != track.KeySounds[i].FileName)
                                throw new InvalidDataException("Purple dummy slots do not match the key sound name.");
                        }
                    }
                }

                if (verifyWaveOutput)
                {
                    float[] samples = new float[80];
                    for (int i = 20; i < 40; i++) samples[i] = 0.5f;
                    for (int i = 60; i < 80; i++) samples[i] = 0.5f;
                    WaveFileWriter.WriteAllSamples(Path.Combine(workDirectory, "wave_input.wav"),
                        new[] { samples, samples }, 2, 44100, 16);

                    string pathBase = workDirectory + Path.DirectorySeparatorChar;
                    var splitter = new WaveSplitService(pathBase, pathBase, "wave_input.wav");
                    double progress = 0;
                    int requiredCount;
                    int createdCount = splitter.Split(-60, 0, 0, 0, true, true,
                        "unused_{0}.wav", new[] { 0.0001f }, ref progress, out requiredCount);
                    if (requiredCount != waveNames.Length || createdCount < 1 ||
                        !File.Exists(Path.Combine(workDirectory, "renamed", waveNames[0])))
                        throw new InvalidDataException("WaveSplitter did not write the BMS-defined sequential filename.");
                }

                Console.WriteLine("PASSED    " + scenarioName);
            }
            catch (Exception exception)
            {
                failures.Add(scenarioName + ": " + exception.Message);
                Console.Error.WriteLine("FAILED   " + scenarioName);
                Console.Error.WriteLine(exception.ToString());
            }
        }

        private static string[] GetActualFiles(string workDirectory)
        {
            string[] files = Directory.GetFiles(workDirectory)
                .Where(x => !String.Equals(Path.GetFileName(x), InputFileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
            {
                throw new InvalidDataException("The legacy pipeline did not generate any files.");
            }
            return files;
        }

        private static void AcceptActualFiles(IEnumerable<string> actualFiles, string expectedDirectory)
        {
            if (Directory.Exists(expectedDirectory))
            {
                Directory.Delete(expectedDirectory, true);
            }
            Directory.CreateDirectory(expectedDirectory);

            foreach (string actualFile in actualFiles)
            {
                File.Copy(actualFile, Path.Combine(expectedDirectory, Path.GetFileName(actualFile)));
            }
        }

        private static void CompareWithExpected(IEnumerable<string> actualFiles, string expectedDirectory)
        {
            if (!Directory.Exists(expectedDirectory))
            {
                throw new DirectoryNotFoundException("Golden Master is missing: " + expectedDirectory);
            }

            var actualByName = actualFiles.ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var expectedByName = Directory.GetFiles(expectedDirectory)
                .ToDictionary(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            string[] missing = expectedByName.Keys.Except(actualByName.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            string[] unexpected = actualByName.Keys.Except(expectedByName.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();

            if (missing.Length != 0 || unexpected.Length != 0)
            {
                throw new InvalidDataException(
                    "Generated file set changed. Missing: [" + String.Join(", ", missing) +
                    "]; unexpected: [" + String.Join(", ", unexpected) + "].");
            }

            foreach (string fileName in expectedByName.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                byte[] expected = File.ReadAllBytes(expectedByName[fileName]);
                byte[] actual = File.ReadAllBytes(actualByName[fileName]);
                if (!expected.SequenceEqual(actual))
                {
                    throw new InvalidDataException(
                        fileName + " differs. Expected " + expected.Length + " bytes / " + Hash(expected) +
                        ", actual " + actual.Length + " bytes / " + Hash(actual) + ".");
                }
            }
        }

        private static List<bool> BuildTrackFlags(string selection, int trackCount)
        {
            if (String.Equals(selection, "none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            if (!String.Equals(selection, "all-non-conductor", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Unsupported track selection: " + selection);
            }

            var flags = Enumerable.Repeat(false, trackCount).ToList();
            for (int index = 1; index < flags.Count; index++)
            {
                flags[index] = true;
            }
            return flags;
        }

        private static IDictionary<string, string> ReadSettings(string path)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    throw new InvalidDataException("Invalid fixture setting: " + rawLine);
                }
                settings[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return settings;
        }

        private static string GetRequiredSetting(IDictionary<string, string> settings, string name)
        {
            string value;
            if (!settings.TryGetValue(name, out value))
            {
                throw new InvalidDataException("Fixture setting is missing: " + name);
            }
            return value;
        }

        private static int ParseInt(IDictionary<string, string> settings, string name)
        {
            return Int32.Parse(GetRequiredSetting(settings, name), CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(IDictionary<string, string> settings, string name)
        {
            return Boolean.Parse(GetRequiredSetting(settings, name));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return String.Concat(algorithm.ComputeHash(bytes).Select(x => x.ToString("x2")));
            }
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (String.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[index + 1]);
                }
            }
            return null;
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Mid2BMS.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Could not locate the repository root.");
        }
    }
}
