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

        private static void RunFixture(string fixtureName, string fixtureDirectory, string temporaryRoot, bool accept)
        {
            IDictionary<string, string> settings = ReadSettings(Path.Combine(fixtureDirectory, SettingsFileName));
            string sourceInputPath = Path.Combine(fixtureDirectory, InputFileName);
            string workDirectory = Path.Combine(temporaryRoot, fixtureName);
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

            int vacantWavid = ParseInt(settings, "vacantWavid");
            int vacantBmsChannelIndex = ParseInt(settings, "vacantBmsChannelIndex");
            string trackCsv;
            List<string> midiTrackNames = null;
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
                ParseInt(settings, "wavidSpacing"),
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

            if (accept)
            {
                return;
            }

            CompareWithExpected(actualFiles, expectedDirectory);
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
