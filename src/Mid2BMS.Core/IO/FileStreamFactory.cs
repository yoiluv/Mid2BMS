using System;
using System.Collections.Generic;
using System.IO;

namespace Mid2BMS
{
    public static class FileIO
    {
        static Dictionary<string, Stream> readfiles = new Dictionary<string, Stream>();
        static Dictionary<string, MemoryStream> wrotefiles = new Dictionary<string, MemoryStream>();

        public static List<String> SavedFileList
        {
            get
            {
                throw new PlatformNotSupportedException();
            }
        }

        public static void ShowOpenFileDialog(String filename)
        {
            throw new PlatformNotSupportedException();
        }
        public static void ShowSaveFileDialog(String filename)
        {
            throw new PlatformNotSupportedException();
        }

        public static void WriteAllText(String filename, String text)
        {
            File.WriteAllText(filename, text, HatoEnc.Encoding);
        }

        public static String ReadAllText(string filename)
        {
            return File.ReadAllText(filename, HatoEnc.Encoding);
        }
    }

    public static partial class neu
    {
        public static Stream IFileStream(String filename, FileMode mode, FileAccess access)
        {
            return new FileStream(filename, mode, access);
        }
    }
}
