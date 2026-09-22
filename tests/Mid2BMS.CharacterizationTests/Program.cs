using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Mid2BMS.CharacterizationTests
{
    internal static class Program
    {
        private const string SettingsFileName = "fixture.properties";
        private const string InputFileName = "input.mid";

        [STAThread]
        private static int Main(string[] args)
        {
            CoreInteraction.Current = new CharacterizationInteraction();
            AssertPhase14UiContracts();
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
                    RunMixedModeScenario(fixturesRoot, temporaryRoot, failures);
                    RunRedMixedModeScenario(fixturesRoot, temporaryRoot, false, failures);
                    RunRedMixedModeScenario(fixturesRoot, temporaryRoot, true, failures);
                    RunThreeModeSequenceLayerScenario(fixturesRoot, temporaryRoot, failures);
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

        private static void AssertPhase14UiContracts()
        {
            if (!(Form1.CreateNamingStrategy(0) is LegacyKeySoundNamingStrategy)
                || !(Form1.CreateNamingStrategy(1) is SequentialKeySoundNamingStrategy)
                || !(Form1.CreateNamingStrategy(2) is TrackSequentialKeySoundNamingStrategy))
                throw new InvalidOperationException("The File Naming selector is not mapped to the expected strategies.");

            if (Form2.ValidateTrackSettings(new TrackSettings
            {
                Mode = TrackMode.Red,
                IsOneShot = true,
            }, true) != null)
                throw new InvalidOperationException("Red OneShot should be available in the track settings UI.");

            if (Form2.ValidateTrackSettings(new TrackSettings
            {
                Mode = TrackMode.Purple,
                IsChord = true,
            }, false) == null)
                throw new InvalidOperationException("Purple Chord should be rejected by the track settings UI.");

            if (Form2.ValidateTrackSettings(new TrackSettings
            {
                Mode = TrackMode.Red,
                IsXChain = true,
            }, true) != null)
                throw new InvalidOperationException("Red XChain should be available when SequenceLayer is enabled.");

            if (Form2.ValidateTrackSettings(new TrackSettings
            {
                Mode = TrackMode.Blue,
                IsXChain = true,
            }, true) == null)
                throw new InvalidOperationException("XChain should be rejected for non-Red tracks.");

            using (var mainForm = new Form1())
            {
                if (!mainForm.Text.Contains(Application.ProductVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException("The application title does not show the product version.");
                ComboBox namingSelector = mainForm.Controls.Find("comboBox_fileNaming", true)
                    .OfType<ComboBox>().Single();
                if (namingSelector.Items.Count != 3 || namingSelector.SelectedIndex != 0)
                    throw new InvalidOperationException("The File Naming selector has unexpected items or default.");
            }

            using (var trackForm = new Form2())
            {
                trackForm.TrackName_csv = "Tr\tnta\tntm\tTrackName\r\n\t(waves)\t(notes)\t\r\n0\t1\t1\tKick\r\n";
                trackForm.TrackNames = new List<string> { "Kick" };
                trackForm.InstrumentNames = new List<string> { "Drums" };
                trackForm.SetMode(true, false, false);

                typeof(Form2).GetField("changeEnabled", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(trackForm, true);
                typeof(Form2).GetMethod("SetTable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(trackForm, new object[] { true });

                DataGridView grid = trackForm.Controls.Find("dataGridView1", true)
                    .OfType<DataGridView>().Single();
                if (!(grid.Columns[6] is DataGridViewComboBoxColumn))
                    throw new InvalidOperationException("The track Mode column is not a ComboBox.");

                grid.Rows[0].Cells[6].Value = TrackMode.Red.ToString();
                if (grid.Rows[0].Cells[11].ReadOnly)
                    throw new InvalidOperationException("XChain was not enabled for a Red track with SequenceLayer.");

                grid.Rows[0].Cells[6].Value = TrackMode.Purple.ToString();
                if (!grid.Rows[0].Cells[9].ReadOnly)
                    throw new InvalidOperationException("Chord was not disabled for a Purple track.");
            }
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
            int? startingWavId = null, int? wavidSpacing = null, bool duplicateTrackNames = false,
            bool useTrackSettings = false, IReadOnlyList<TrackMode> trackModes = null,
            bool? sequenceLayerOverride = null, IReadOnlyCollection<int> xChainTrackOverrides = null)
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
            if (xChainTrackOverrides != null)
            {
                isXChainList = Enumerable.Range(0, trackCount)
                    .Select(xChainTrackOverrides.Contains).ToList();
            }
            bool sequenceLayer = sequenceLayerOverride ?? ParseBool(settings, "sequenceLayer");

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

            IReadOnlyList<TrackSettings> trackSettings = TrackSettings.FromLegacyFlags(trackCount,
                TrackSettings.FromLegacyGlobalMode(isRedMode, isPurpleMode),
                isDrumsList, null, isChordList, isXChainList, isOneShotList);
            if (trackModes != null)
            {
                if (trackModes.Count != trackCount)
                    throw new InvalidDataException("Track mode count differs from the MIDI track count.");
                trackSettings = trackSettings.Select((value, index) => value with
                {
                    Mode = trackModes[index],
                }).ToArray();
            }
            if (useTrackSettings)
            {
                var request = new Mid2BmsConversionRequest
                {
                    DefaultTrackMode = TrackSettings.FromLegacyGlobalMode(isRedMode, isPurpleMode),
                    TrackSettings = trackSettings,
                    TrackNames = midiTrackNames,
                    CreateExtraFiles = ParseBool(settings, "createExtraFiles"),
                    LookAtInstrumentName = ParseBool(settings, "lookAtInstrumentName"),
                    MarginTimeBeats = GetRequiredSetting(settings, "marginTimeBeats"),
                    WavIdSpacing = wavidSpacing ?? ParseInt(settings, "wavidSpacing"),
                    SequenceLayer = sequenceLayer,
                    NewTimebase = ParseInt(settings, "newTimebase"),
                    VelocityStep = ParseInt(settings, "velocityStep"),
                    StartingWavId = vacantWavid,
                    StartingBmsChannelIndex = vacantBmsChannelIndex,
                };
                Mid2BmsConversionResult result = target.Mid2BMS_Process(
                    request, ref progressValue, ref progressFinished);
                if (result.Cancelled)
                    throw new InvalidOperationException("The unified conversion workflow was cancelled.");
                if (result.TrackSettings.Count != trackCount ||
                    !File.Exists(result.SingleNoteMidiPath) ||
                    !File.Exists(result.BmsPath) ||
                    !File.Exists(result.KeySoundManifestPath))
                    throw new InvalidOperationException(
                        "The unified conversion result did not expose its generated artifacts.");
                vacantWavid = result.NextWavId;
                vacantBmsChannelIndex = result.NextBmsChannelIndex;
                trackCsv = result.TrackCsv;
                midiTrackNames = new List<string>(result.TrackNames);
                midiInstrumentNames = new List<string>(result.InstrumentNames);
            }
            else
            {
                target.Mid2BMS_Process(isRedMode, isPurpleMode, ParseBool(settings, "createExtraFiles"),
                    ref vacantWavid, ref vacantBmsChannelIndex, ParseBool(settings, "lookAtInstrumentName"),
                    GetRequiredSetting(settings, "marginTimeBeats"),
                    wavidSpacing ?? ParseInt(settings, "wavidSpacing"), out trackCsv,
                    ref midiTrackNames, out midiInstrumentNames, isDrumsList, null, isChordList,
                    isXChainList, isOneShotList, sequenceLayer,
                    ParseInt(settings, "newTimebase"), ParseInt(settings, "velocityStep"),
                    ref progressValue, ref progressFinished);
            }

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
                    duplicateTrackNames, useTrackSettings: true);
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

        private static void RunMixedModeScenario(string fixturesRoot, string temporaryRoot,
            List<string> failures)
        {
            const string scenarioName = "mixed_blue_purple";
            try
            {
                TrackMode[] modes =
                {
                    TrackMode.Blue,
                    TrackMode.Blue,
                    TrackMode.Purple,
                    TrackMode.Blue,
                    TrackMode.Purple,
                };
                string workDirectory = RunFixture("blue_basic", Path.Combine(fixturesRoot, "blue_basic"),
                    temporaryRoot, false, workName: scenarioName, compareGolden: false,
                    useTrackSettings: true, trackModes: modes);

                string bmsPath = Path.Combine(workDirectory, "text6_bms_blue_purple.txt");
                string midiPath = Path.Combine(workDirectory, "text3_tanon_smf_blue_purple.mid");
                if (!File.Exists(bmsPath) || !File.Exists(midiPath))
                    throw new InvalidDataException("Mixed-mode BMS or single-note MIDI output is missing.");
                if (Directory.GetFiles(workDirectory, "text6_bms_blue.txt").Any() ||
                    Directory.GetFiles(workDirectory, "text6_bms_purple.txt").Any())
                    throw new InvalidDataException("Mixed-mode conversion used a legacy single-mode output name.");

                KeySoundManifest manifest = KeySoundManifest.FromLegacyRenamerText(
                    FileIO.ReadAllText(Path.Combine(workDirectory, "text5_renamer_array.txt")));
                KeySoundTrack[] tracks = manifest.Tracks.ToArray();
                if (tracks.Length != 4)
                    throw new InvalidDataException("Mixed-mode scenario did not produce four musical tracks.");

                using (var stream = new FileStream(midiPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var midi = new MidiStruct(stream, true);
                    if (midi.tracks.Count != modes.Length)
                        throw new InvalidDataException("Mixed-mode MIDI track count changed.");

                    for (int index = 0; index < tracks.Length; index++)
                    {
                        TrackMode expectedMode = modes[index + 1];
                        KeySoundTrack track = tracks[index];
                        string[] slots = track.ToLegacyRow().Skip(3).ToArray();
                        int expectedSlotCount = track.KeySounds.Count *
                            (expectedMode == TrackMode.Purple ? 2 : 1);
                        if (slots.Length != expectedSlotCount)
                            throw new InvalidDataException("Mixed-mode WaveSplitter slots do not match track mode.");
                        if (track.KeySounds.Any(key => !key.FileName.StartsWith(
                            expectedMode == TrackMode.Purple ? "p_" : "b_", StringComparison.Ordinal)))
                            throw new InvalidDataException("Mixed-mode key sound prefix does not match track mode.");
                        if (midi.tracks[index + 1].OfType<MidiEventNote>().Count() != expectedSlotCount)
                            throw new InvalidDataException("Mixed-mode MIDI notes do not match WaveSplitter slots.");
                    }
                }

                string bms = FileIO.ReadAllText(bmsPath);
                if (!bms.Contains("#WAV") || !bms.Contains(" b_") || !bms.Contains(" p_"))
                    throw new InvalidDataException("Mixed-mode BMS does not contain both Blue and Purple WAV definitions.");

                Console.WriteLine("PASSED    " + scenarioName);
            }
            catch (Exception exception)
            {
                failures.Add(scenarioName + ": " + exception.Message);
                Console.Error.WriteLine("FAILED   " + scenarioName);
                Console.Error.WriteLine(exception.ToString());
            }
        }

        private static void RunRedMixedModeScenario(string fixturesRoot, string temporaryRoot,
            bool sequenceLayer, List<string> failures)
        {
            string scenarioName = sequenceLayer
                ? "mixed_blue_red_sequence_layer"
                : "mixed_blue_red";
            try
            {
                TrackMode[] modes =
                {
                    TrackMode.Blue,
                    TrackMode.Blue,
                    TrackMode.Blue,
                    TrackMode.Red,
                    TrackMode.Red,
                    TrackMode.Blue,
                };
                string fixtureName = sequenceLayer ? "sequence_layer" : "red_automation";
                string workDirectory = RunFixture(fixtureName,
                    Path.Combine(fixturesRoot, fixtureName), temporaryRoot, false,
                    workName: scenarioName, compareGolden: false,
                    useTrackSettings: true, trackModes: modes);

                string bmsPath = Path.Combine(workDirectory, "text6_bms_blue_red.txt");
                string midiPath = Path.Combine(workDirectory, "text3_tanon_smf_blue_red.mid");
                if (!File.Exists(bmsPath) || !File.Exists(midiPath))
                    throw new InvalidDataException("Blue/Red BMS or single-note MIDI output is missing.");

                string bms = FileIO.ReadAllText(bmsPath);
                if (!bms.Contains(" b_") || !bms.Contains(" r_"))
                    throw new InvalidDataException("Blue/Red BMS does not contain both mode prefixes.");

                KeySoundManifest manifest = KeySoundManifest.FromLegacyRenamerText(
                    FileIO.ReadAllText(Path.Combine(workDirectory, "text5_renamer_array.txt")));
                string[] waveNames = manifest.Tracks.SelectMany(x => x.KeySounds)
                    .Select(x => x.FileName).ToArray();
                if (!waveNames.Any(x => x.StartsWith("b_", StringComparison.Ordinal)) ||
                    !waveNames.Any(x => x.StartsWith("r_", StringComparison.Ordinal)))
                    throw new InvalidDataException("Blue/Red manifest does not contain both modes.");

                using (var stream = new FileStream(midiPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var midi = new MidiStruct(stream, true);
                    if (midi.tracks.Count != modes.Length)
                        throw new InvalidDataException("Blue/Red MIDI track count changed.");
                    if (!midi.tracks.SelectMany(x => x).Any(x => x is MidiEventCC || x is MidiEventPB))
                        throw new InvalidDataException("Red automation was not retained in mixed-mode MIDI.");

                    if (sequenceLayer)
                    {
                        MidiEventNote[] notes = midi.tracks.SelectMany(x => x.OfType<MidiEventNote>())
                            .OrderBy(x => x.tick).ToArray();
                        for (int i = 1; i < notes.Length; i++)
                        {
                            if (notes[i].tick < notes[i - 1].tick + notes[i - 1].q)
                                throw new InvalidDataException("SequenceLayer contains overlapping key sounds.");
                        }
                    }
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

        private static void RunThreeModeSequenceLayerScenario(string fixturesRoot,
            string temporaryRoot, List<string> failures)
        {
            const string scenarioName = "mixed_blue_purple_red_sequence_xchain";
            try
            {
                TrackMode[] modes =
                {
                    TrackMode.Blue,
                    TrackMode.Blue,
                    TrackMode.Red,
                    TrackMode.Red,
                    TrackMode.Purple,
                };
                const int xChainTrack = 2;
                string workDirectory = RunFixture("blue_basic",
                    Path.Combine(fixturesRoot, "blue_basic"), temporaryRoot, false,
                    workName: scenarioName, compareGolden: false, useTrackSettings: true,
                    trackModes: modes, sequenceLayerOverride: true,
                    xChainTrackOverrides: new[] { xChainTrack });

                string bmsPath = Path.Combine(workDirectory, "text6_bms_blue_purple_red.txt");
                string midiPath = Path.Combine(workDirectory, "text3_tanon_smf_blue_purple_red.mid");
                if (!File.Exists(bmsPath) || !File.Exists(midiPath))
                    throw new InvalidDataException("Three-mode SequenceLayer output is missing.");

                string bms = FileIO.ReadAllText(bmsPath);
                if (!bms.Contains(" b_") || !bms.Contains(" p_") || !bms.Contains(" r_"))
                    throw new InvalidDataException("Three-mode BMS does not contain every mode prefix.");

                using (var stream = new FileStream(midiPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var midi = new MidiStruct(stream, true);
                    if (midi.tracks.Count != modes.Length ||
                        !midi.tracks[xChainTrack].OfType<MidiEventNote>().Any())
                        throw new InvalidDataException("XChain triggers were not retained in mixed MIDI.");

                    MidiEventNote[] audibleNotes = midi.tracks
                        .Where((track, index) => index != xChainTrack)
                        .SelectMany(x => x.OfType<MidiEventNote>())
                        .OrderBy(x => x.tick).ToArray();
                    for (int i = 1; i < audibleNotes.Length; i++)
                    {
                        if (audibleNotes[i].tick < audibleNotes[i - 1].tick + audibleNotes[i - 1].q)
                            throw new InvalidDataException("Three-mode SequenceLayer contains overlapping key sounds.");
                    }
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
