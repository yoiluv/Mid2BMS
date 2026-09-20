using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace Mid2BMS.Core.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                AssertCoreHasNoWinFormsReference();
                AssertEncodingAndResourcesWorkWithoutDesktopRuntime();
                AssertHostInteractionReceivesCoreMessages();
                AssertLegacyHashGuard();
                AssertMidiQuantization();
                AssertWaveSplitService();
                AssertDuplicateDefinitionService();
                Console.WriteLine("Core isolation tests passed.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static void AssertCoreHasNoWinFormsReference()
        {
            Assembly core = typeof(CoreInteraction).Assembly;
            string[] references = core.GetReferencedAssemblies().Select(x => x.Name).ToArray();
            Assert(!references.Contains("System.Windows.Forms"), "Core references WinForms.");
            Assert(!references.Contains("Mid2BMS"), "Core references the UI assembly.");

            string framework = core.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName;
            Assert(framework == ".NETCoreApp,Version=v10.0", "Unexpected Core target framework: " + framework);
        }

        private static void AssertEncodingAndResourcesWorkWithoutDesktopRuntime()
        {
            string previousDirectory = Environment.CurrentDirectory;
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Mid2BMS.Core.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            try
            {
                Environment.CurrentDirectory = temporaryDirectory;
                File.WriteAllText("encoding.ini", "Shift_JIS", Encoding.ASCII);

                byte[] encoded = HatoEnc.Encode("あ");
                Assert(encoded.SequenceEqual(new byte[] { 0x82, 0xA0 }), "Shift_JIS encoding changed.");

                var downsampler = new AdaptiveDownsampler(-42);
                var destination = new float[8];
                Assert(downsampler.DownSample(new float[16], destination), "Core filter resources failed to load.");
            }
            finally
            {
                Environment.CurrentDirectory = previousDirectory;
                Directory.Delete(temporaryDirectory, true);
            }
        }

        private static void AssertHostInteractionReceivesCoreMessages()
        {
            ICoreInteraction previous = CoreInteraction.Current;
            var host = new RecordingInteraction();
            CoreInteraction.Current = host;
            try
            {
                // A format-0, zero-track MIDI file exercises the parser's legacy notification.
                byte[] midi = { 0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 0, 0, 0, 1, 0xE0 };
                using (var stream = new MemoryStream(midi))
                {
                    var parsed = new MidiStruct(stream);
                    Assert(parsed.tracks.Count == 0, "Unexpected MIDI tracks.");
                }
                Assert(host.Messages.Count == 1 && host.Messages[0].Contains("midi format"), "Parser notification did not reach the host.");

                host.Abort = true;
                Assert(CoreInteraction.ConfirmAbort("abort?", "confirm"), "Host confirmation result was not returned.");
            }
            finally
            {
                CoreInteraction.Current = previous;
            }
        }

        private static void AssertLegacyHashGuard()
        {
            string directory = NewTemporaryDirectory();
            try
            {
                string pathBase = directory + Path.DirectorySeparatorChar;
                var guard = new GeneratedFileHashGuard(pathBase);
                File.WriteAllText(Path.Combine(directory, "text0_stdout_part1.mml"), "abc", Encoding.ASCII);
                File.WriteAllText(Path.Combine(directory, "hashvalues1.txt"),
                    "900150983cd24fb0d6963f7d28e17f72" + Environment.NewLine, Encoding.ASCII);

                Assert(guard.CheckMidiOutputs(_ => throw new InvalidOperationException("Unexpected hash prompt.")),
                    "Matching MD5 was rejected.");
                File.WriteAllText(Path.Combine(directory, "text0_stdout_part1.mml"), "changed", Encoding.ASCII);
                string warning = null;
                Assert(!guard.CheckMidiOutputs(message => { warning = message; return false; }),
                    "Overwrite rejection was ignored.");
                Assert(warning != null && warning.Contains("text0_stdout_part1.mml"),
                    "Hash warning omitted the file name.");
                Assert(guard.CheckMidiOutputs(_ => true), "Overwrite acceptance was ignored.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void AssertMidiQuantization()
        {
            byte[] midi = {
                0x4D, 0x54, 0x68, 0x64, 0, 0, 0, 6, 0, 1, 0, 1, 1, 0xE0,
                0x4D, 0x54, 0x72, 0x6B, 0, 0, 0, 13,
                0, 0x90, 60, 67,
                0x83, 0x60, 0x80, 60, 0,
                0, 0xFF, 0x2F, 0
            };

            using (var output = new MemoryStream())
            {
                MidiQuantizer.ChangeMidiTimebase(new MemoryStream(midi), output, 240);
                // MidiStruct.Export closes its destination stream.
                var parsed = new MidiStruct(new MemoryStream(output.ToArray()), true);
                Assert(parsed.resolution == 240, "Timebase quantization changed.");
                Assert(parsed.tracks[0].OfType<MidiEventNote>().Any(x => x.tick == 0 && x.q == 240),
                    "Quantized note timing changed.");
            }

            using (var output = new MemoryStream())
            {
                MidiQuantizer.QuantizeVelocity(new MemoryStream(midi), output, 10);
                var parsed = new MidiStruct(new MemoryStream(output.ToArray()), true);
                Assert(parsed.tracks[0].OfType<MidiEventNote>().Any(x => x.v == 70),
                    "Velocity quantization changed.");
            }
        }

        private static void AssertWaveSplitService()
        {
            string directory = NewTemporaryDirectory();
            try
            {
                string pathBase = directory + Path.DirectorySeparatorChar;
                float[] samples = new float[80];
                for (int i = 20; i < 40; i++) samples[i] = 0.5f;
                for (int i = 60; i < 80; i++) samples[i] = 0.5f;
                WaveFileWriter.WriteAllSamples(Path.Combine(directory, "input.wav"),
                    new[] { samples, samples }, 2, 44100, 16);

                var service = new WaveSplitService(pathBase, pathBase, "input.wav");
                double progress = 0;
                int requiredCount;
                int createdCount = service.Split(-60, 0, 0, 0, true, false,
                    "tone_{0}.wav", new[] { 0.0001f }, ref progress, out requiredCount);

                Assert(requiredCount == -1, "Non-renaming split count changed.");
                Assert(createdCount >= 1, "Wave splitter created no key sounds.");
                Assert(File.Exists(Path.Combine(directory, "renamed", "tone_1.wav")),
                    "Wave split output is missing.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void AssertDuplicateDefinitionService()
        {
            string directory = NewTemporaryDirectory();
            try
            {
                string pathBase = directory + Path.DirectorySeparatorChar;
                var service = new DuplicateDefinitionService(pathBase, "input.bms");
                FileIO.WriteAllText(Path.Combine(directory, "input.bms"), "#BPM 120\r\n#PLAYER 1\r\n");
                string bms = service.ReadInput();
                double progress = 0;
                string[] content;
                service.Generate(bms, 0.1, 4, out content, ref progress);
                string output = service.WriteOutput(content);
                Assert(File.Exists(output), "Duplicate-definition output is missing.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static string NewTemporaryDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Mid2BMS.Core.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class RecordingInteraction : ICoreInteraction
        {
            public readonly List<string> Messages = new List<string>();
            public bool Abort;

            public void ShowMessage(string message) { Messages.Add(message); }
            public void ShowMessage(string message, string caption) { Messages.Add(message); }
            public bool ConfirmAbort(string message, string caption) { return Abort; }
        }
    }
}
