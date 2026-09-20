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
