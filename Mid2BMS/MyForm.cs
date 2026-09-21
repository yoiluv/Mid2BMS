using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Mid2BMS
{
    class MyForm
    {
        public String PathBase = ":";  // 円記号で終わらなければならない
        public String WavePathBase = ":";
        public String RenamedPathBase = ":";
        public String FileName_MidiFile = "";  //@"stdread.mid";
        public String FileName_WaveFile = "";  //@"";
        public String FileName_BMSFile = "";  //@"";
        public IKeySoundNamingStrategy NamingStrategy { get; set; } = new LegacyKeySoundNamingStrategy();

        //***********************************************************************************
        //*** 既存出力の上書き確認
        //***********************************************************************************

        private bool Mid2BMS_CheckHash()
        {
            return new GeneratedFileHashGuard(PathBase).CheckMidiOutputs(ConfirmOverwrite);
        }

        private bool WaveSplit_CheckHash()
        {
            return new GeneratedFileHashGuard(PathBase).CheckWaveOutputs(ConfirmOverwrite);
        }

        private static bool ConfirmOverwrite(string message)
        {
            return MessageBox.Show(message, "確認", MessageBoxButtons.YesNoCancel) == DialogResult.Yes;
        }

        //***********************************************************************************
        //*** Mid2BMS に関する記述
        //***********************************************************************************
        /// <summary>
        /// midiファイルからbmsファイル等の音切りに必要なファイルを生成します。
        /// [1] Mid2BMS タブの Process ボタンに相当します。
        /// </summary>
        /// <param name="isRedMode">red modeであるかどうかを表すbool値です。</param>
        /// <param name="isPurpleMode">purple modeであるかどうかを表すbool値です。</param>
        /// <param name="createExFiles">テンポチェンジ定義BMS等の追加のファイルを生成するかどうかを示すbool値です。</param>
        /// <param name="VacantWavid">定義を開始するWAV定義番号です。</param>
        /// <param name="DefaultVacantBMSChannelIdx">配置を開始するレーンを表す、static string[] Mid2BMS.BMSPlacement.ChannelTemplate の添字です。</param>
        /// <param name="LookAtInstrumentName">キー音ファイル名に Instrument Name を用いるか、Track Name を用いるかを表すフラグの、各トラックに対する値の配列です。</param>
        /// <param name="margintime_beats">キー音とキー音の間に設けられる無音時間を、拍で表した長さです。</param>
        /// <param name="WavidSpacing">トラックとトラックの間に設けられる、キー音が定義されないWAV定義の数です。通常は0です。</param>
        /// <param name="trackCsv">midiファイルを解析した結果を格納するcsvを表す文字列です。</param>
        /// <param name="MidiTrackNames">[nullable] midiファイルを解析した結果得られたTrack Nameを格納する配列です。ただしnull以外が与えられた場合は、それに従ってキー音にファイル名を与えます。</param>
        /// <param name="MidiInstrumentNames">[nullable] midiファイルを解析した結果得られたInstrument Nameを格納する配列です。</param>
        /// <param name="isDrumsList">[nullable] 各音程に対し1つのBMSレーンを割り当てるかどうかを示すフラグの配列です。フラグのデフォルト値はfalseです。</param>
        /// <param name="ignoreList">[nullable] トラックを無視するかどうかを示すフラグの配列です。フラグのデフォルト値はfalseです。</param>
        /// <param name="isChordList">[nullable] 同時に発音された音を1つのキー音にまとめるかどうかを示すフラグの配列です。フラグのデフォルト値はfalseです。</param>
        /// <param name="isXChainList">[nullable] RedModeとシーケンスレイヤーの両方が選択されている場合に、サイドチェイントリガノーツとして扱うかどうかを示すフラグの配列です。フラグのデフォルト値はfalseです。</param>
        /// <param name="isOneShotList">[nullable] RedModeとシーケンスレイヤーの両方が選択されている場合に、オートメーションをグローバルとして扱うかどうかを示すフラグの配列です。フラグのデフォルト値はtrueです。</param>
        /// <param name="sequenceLayer">それぞれのトラックが重ならないように単音midiを時間的にずらすかどうかを示すフラグです。デフォルト値はfalseです。</param>
        /// <param name="ProgressBarValue">プログレスバーに対して値を反映させるための変数です。</param>
        /// <param name="ProgressBarFinished">プログレスバーの増加が終了したかどうかを示すフラグです。</param>
        public void Mid2BMS_Process(
            bool isRedMode, bool isPurpleMode, bool createExFiles, ref int VacantWavid, ref int DefaultVacantBMSChannelIdx,
            bool LookAtInstrumentName, String margintime_beats, int WavidSpacing,
            out String trackCsv, ref List<String> MidiTrackNames, out List<String> MidiInstrumentNames,
            List<bool> isDrumsList, List<bool> ignoreList, List<bool> isChordList, List<bool> isXChainList, List<bool> isOneShotList, bool sequenceLayer,
            int newTimebase, int velocityStep,
            ref double ProgressBarValue, ref bool ProgressBarFinished)
        {
            #region ファイルの更新チェック
            if (!Mid2BMS_CheckHash())  // TODO: 不要なコードの削除or修正
            {
                // 操作をキャンセルする
                ProgressBarValue = 1.00;
                ProgressBarFinished = true;
                trackCsv = null;
                MidiTrackNames = null;
                MidiInstrumentNames = null;
                return;
            }
            #endregion

            new Mid2BmsConverter(PathBase, FileName_MidiFile, NamingStrategy).Run(
                isRedMode, isPurpleMode, createExFiles, ref VacantWavid, ref DefaultVacantBMSChannelIdx,
                LookAtInstrumentName, margintime_beats, WavidSpacing,
                out trackCsv, ref MidiTrackNames, out MidiInstrumentNames,
                isDrumsList, ignoreList, isChordList, isXChainList, isOneShotList, sequenceLayer,
                newTimebase, velocityStep,
                ref ProgressBarValue, ref ProgressBarFinished);
        }


        //***********************************************************************************
        //*** WaveSplit に関する記述
        //***********************************************************************************

        public void WaveSplit_Process(
            double tailcut_threshold, int fadeintime, int fadeouttime, double silence_time, bool inputFileIndicated,
            bool renamingEnabled, String renamingFilename, float[] SilenceLevelsSquare,
            ref double ProgressBarValue, ref bool ProgressBarFinished)
        {
            if (!WaveSplit_CheckHash())
            {
                ProgressBarValue = 1.00;
                ProgressBarFinished = true;
                return;
            }


            int RenameRequiredFilesCount;
            int createdWavCount = new WaveSplitService(PathBase, WavePathBase, FileName_WaveFile).Split(
                tailcut_threshold, fadeintime, fadeouttime, silence_time, inputFileIndicated,
                renamingEnabled, renamingFilename, SilenceLevelsSquare,
                ref ProgressBarValue, out RenameRequiredFilesCount);

            ProgressBarValue = 1.00;
            ProgressBarFinished = true;

            if (RenameRequiredFilesCount == -1)
            {
                MessageBox.Show(
                    createdWavCount + " 個のwavを書き出しました。" + 
                    "もし、音切りに失敗していた場合は、「無音検出時間 (Silence Time)」の値を大きくしてみて下さい。",
                    "Split Result");
            }
            else
            {
                if (RenameRequiredFilesCount > createdWavCount)
                {
                    MessageBox.Show(
                        RenameRequiredFilesCount + " 個のwavを書き出さなければならないのに対し、" + createdWavCount + " 個のwavを書き出しました。" + 
                        "つまり、正しく音切り出来ていない可能性があります。" +
                        "その場合は、「無音検出時間 (Silence Time)」の値を大きくしてみて下さい。",
                        "Split Failed?");
                }
                else
                {
                    MessageBox.Show(
                        RenameRequiredFilesCount + " 個のwavを書き出さなければならないのに対し、" + createdWavCount + " 個のwavを書き出しました。" +
                        "もし、音切りに失敗していた場合は、「無音検出時間 (Silence Time)」の値を大きくしてみて下さい。",
                        "Renaming Result");
                }
            }
        }

        //***********************************************************************************
        //*** DupeDef に関する記述
        //***********************************************************************************

        // 誰かBMS界隈に詳しい英語の読める人、重複定義の英訳教えて
        public void DupeDef_Process(double intervaltime, int maxLayerCount, ref double ProgressBarValue, ref bool ProgressBarFinished)
        {
            String[] text;
            var service = new DuplicateDefinitionService(RenamedPathBase, FileName_BMSFile);
            String bms = service.ReadInput();

            try
            {
                service.Generate(bms, intervaltime, maxLayerCount, out text, ref ProgressBarValue);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
                return;
            }

            String newBmsPath = service.WriteOutput(text);

            ProgressBarValue = 1.00;
            ProgressBarFinished = true;

            MessageBox.Show("\"" + newBmsPath + "\" にBMSを書き込みました。");
        }

    }
}
