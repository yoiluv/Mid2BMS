using System.IO;

namespace Mid2BMS
{
    internal sealed class DuplicateDefinitionService
    {
        private readonly string renamedPathBase;
        private readonly string bmsFileName;

        public DuplicateDefinitionService(string renamedPathBase, string bmsFileName)
        {
            this.renamedPathBase = renamedPathBase;
            this.bmsFileName = bmsFileName;
        }

        public string ReadInput()
        {
            return FileIO.ReadAllText(renamedPathBase + bmsFileName);
        }

        public void Generate(string bms, double intervaltime, int maxLayerCount,
            out string[] text, ref double progressBarValue)
        {
            var processor = new DupeDefinition();
            string[] textNames;
            processor.Process(bms, intervaltime, renamedPathBase, maxLayerCount,
                out textNames, out text, ref progressBarValue, 0.0, 1.0);
        }

        public string WriteOutput(string[] text)
        {
            string newBmsPath = renamedPathBase + Path.GetFileNameWithoutExtension(bmsFileName) + "_multidefined.bms";
            FileIO.WriteAllText(newBmsPath, text[4]);
            return newBmsPath;
        }
    }
}
