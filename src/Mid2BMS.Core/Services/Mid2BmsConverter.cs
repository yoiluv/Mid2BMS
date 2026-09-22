using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mid2BMS
{
    // Coordinates the existing MIDI-to-BMS pipeline without depending on a form.
    internal sealed class Mid2BmsConverter
    {
        private readonly string PathBase;
        private readonly string FileName_MidiFile;
        private readonly IKeySoundNamingStrategy namingStrategy;

        public Mid2BmsConverter(string pathBase, string midiFileName, IKeySoundNamingStrategy namingStrategy = null)
        {
            PathBase = pathBase;
            FileName_MidiFile = midiFileName;
            this.namingStrategy = namingStrategy ?? new LegacyKeySoundNamingStrategy();
        }

        public void Run(
            bool isRedMode, bool isPurpleMode, bool createExFiles, ref int VacantWavid, ref int DefaultVacantBMSChannelIdx,
            bool LookAtInstrumentName, String margintime_beats, int WavidSpacing,
            out String trackCsv, ref List<String> MidiTrackNames, out List<String> MidiInstrumentNames,
            IReadOnlyList<TrackSettings> trackSettings, bool sequenceLayer,
            int newTimebase, int velocityStep,
            ref double ProgressBarValue, ref bool ProgressBarFinished)
        {
            Func<Stream> quantizedMidiStreamGenerator;

            #region Midiのクオンタイズ
            if (newTimebase > 0)
            {
                var quantizedMidiWriteStream = new MemoryStream();

                MidiQuantizer.ChangeMidiTimebase(
                    neu.IFileStream(this.PathBase + this.FileName_MidiFile, FileMode.Open, FileAccess.Read),
                    quantizedMidiWriteStream,
                    newTimebase);

                quantizedMidiWriteStream.Close();

                var mbuf = quantizedMidiWriteStream.GetBuffer();

                quantizedMidiStreamGenerator = () => new MemoryStream(mbuf);

                if (createExFiles)
                {
                    File.WriteAllBytes(this.PathBase + "midiinput_timequantizedstream.mid", quantizedMidiWriteStream.GetBuffer());
                }
            }
            else
            {
                quantizedMidiStreamGenerator = () => neu.IFileStream(this.PathBase + this.FileName_MidiFile, FileMode.Open, FileAccess.Read);
            }
            #endregion

            #region ベロシティの量子化
            if (velocityStep >= 2)
            {
                var quantizedMidiWriteStream2 = new MemoryStream();

                MidiQuantizer.QuantizeVelocity(
                    quantizedMidiStreamGenerator(),
                    quantizedMidiWriteStream2,
                    velocityStep);

                quantizedMidiWriteStream2.Close();

                var mbuf = quantizedMidiWriteStream2.GetBuffer();

                quantizedMidiStreamGenerator = () => new MemoryStream(mbuf);  // quantizedMidiStreamGenerator に上書き

                if (createExFiles)
                {
                    File.WriteAllBytes(this.PathBase + "midiinput_velocityquantizedstream.mid", quantizedMidiWriteStream2.GetBuffer());
                }
            }
            else
            {
                // 何もしない
            }
            #endregion

            #region timebase及びmidi_bpmの取得、及び重複ノーツのチェック、テンポチェンジBMSの作成
            int timebase;
            decimal midi_bpm;
            {
                Stream rf = quantizedMidiStreamGenerator();
                MidiStruct ms = new MidiStruct(rf);
                if (ms.resolution == null) throw new Exception("resolutionがnull #とは");
                timebase = ms.resolution ?? 480;
                double uspb = ms.InitalUSecondPerBeat ?? (60.0 * 1.0e6 / 120.0);
                midi_bpm = 0.001m * (decimal)Math.Round(1000.0 * 60.0 * 1.0e6 / uspb);  // そこまで0.001単位にこだわる必要があったのかどうか

                bool messageShown = false;

                //double newResolution = 4 * 24;  // BMS分解能、マジックナンバー感ある。16分音符を24個に分解出来る。
                // ↑これは恐らく、テンポチェンジがMIDIファイルに多すぎた場合の対処だと思います。
                // めんどくさいのでとりあえずこのままで（要修正）

                double newResolution = timebase;

                for (int trackindex = 0; trackindex < ms.tracks.Count; trackindex++ )
                {
                    var dict = new HashSet<Tuple<int, long>>();  // Tuple<ノート番号, 発音時間>

                    foreach (var _me in ms.tracks[trackindex])
                    {
                        if (_me is MidiEventNote)
                        {
                            MidiEventNote me = _me as MidiEventNote;
                            if (me.n > 127)
                            {
                                throw new Exception("いや、逆にそれはおかしい");
                            }
                            Tuple<int, long> serialized = Tuple.Create(
                                me.n,
                                (long)Math.Round((double)me.tick * newResolution / (double)ms.resolution));  // このnewResolutionって何ですか・・・

                            if (!messageShown && dict.Contains(serialized))
                            {
                                // todo: 表記が分かりづらいので修正

                                if (CoreInteraction.ConfirmAbort(
                                    "トラック番号" + trackindex + ", " 
                                    + (1 + me.tick / (4 * (int)ms.resolution))  + "小節, "
                                    + "ノート番号" + me.n + "に重複した音符が存在します。\n"
                                    + "このまま続行すると、変換結果が正常とならない可能性があります。(特にisDrumsを選択した場合)\n"
                                    + "処理を中断しますか。(Click \"Yes\" to Abort)\n"
                                    + "(注：Mid2BMSは、Midiチャンネルには対応していません)",
                                     "Confirm to continue"))
                                {
                                    throw new Exception("ユーザーの指示により処理を中断しました。");
                                }
                                messageShown = true;
                            }
                            dict.Add(serialized);
                        }
                    }

                    // トラック00(AAAAA), 00000小節, 位置0/0, ノート番号00 に重複したノートが存在します。
                    // 処理を中断しますか。(Click \"Yes\" to Abort)
                    // (注：Mid2BMSは、Midiチャンネルには対応していません)
                }
                
                if (createExFiles) // テンポチェンジBMSの作成
                {
                    var tempochanges = ms.tracks[0].Where(x => x is MidiEventMeta && ((MidiEventMeta)x).id == 0x51);
                    StringSuruyatu tempobms = "";
                    int bpmid = 1;  // 0じゃダメだよ
                    List<ArrTuple<Frac, int>> tempos = new List<ArrTuple<Frac, int>>();
                    int lasttick = -99999999;
                    int TEMPOCHANGE_RESOLUTION = 32;  // 拍
                    int PRECISION = 1000;  // BPMの精度
                    foreach (var tempoev in tempochanges)
                    {
                        if (bpmid >= 36 * 36) break;
                        if (tempoev.tick - lasttick < (ms.resolution ?? 480) * 4 / TEMPOCHANGE_RESOLUTION) continue;
                        lasttick = tempoev.tick;
                        // += としてはダメ！
                        tempobms = tempobms +
                            "#BPM" + BMSParser.IntToHex36Upper(bpmid) + " "
                            + Math.Round((60000000.0 / ((MidiEventMeta)tempoev).val) * PRECISION) / (double)PRECISION + "\r\n";
                        tempos.Add(Arr.ay(new Frac((long)(tempoev.tick * 192.0 * 0.25 / (ms.resolution ?? 480) + 192.0), 192), bpmid)); // 面倒なので分解能は192

                        bpmid++;
                    }
                    tempobms += "\r\n";
                    tempobms += (new BMSPlacement()).haichiTuplesAsBMS("08", 0, 200, 192, tempos);
                    FileIO.WriteAllText(this.PathBase + "text6_tempochangebms.txt", tempobms);
                }
            }
            #endregion

            #region midiファイルからmmlへの変換、Track Name、Instrument Nameの取得
            Mid2mml m2m = new Mid2mml();

            List<String> MMLs = new List<String>();
            List<String> MidiTrackIdentifier = MidiTrackNames;  // null以外が与えられた場合はそれに従う

            MidiInstrumentNames = new List<String>();   // 無駄な初期化感
            MidiTrackNames = new List<String>();

            List<bool> isEmptyList;

            m2m.Process(quantizedMidiStreamGenerator(), PathBase + @"text0_stdout_part1.txt", out MMLs,
                out MidiTrackNames, out MidiInstrumentNames, createExFiles, ref ProgressBarValue, 0.00, 0.10);

            TrackMode globalMode = TrackSettings.FromLegacyGlobalMode(isRedMode, isPurpleMode);
            IReadOnlyList<TrackSettings> normalizedTrackSettings =
                TrackSettings.NormalizeForConversion(MMLs.Count, globalMode, trackSettings, sequenceLayer);

            if (MidiTrackIdentifier == null)
            {
                // nullが与えられた場合はmidiファイルから読み込んだデータを用いる
                MidiTrackIdentifier = new List<String>(LookAtInstrumentName ? MidiInstrumentNames : MidiTrackNames);  // ちゃんとCloneする
                for (int i = 0; i < MidiTrackIdentifier.Count; i++)
                {
                    if (MidiTrackIdentifier[i] == "") MidiTrackIdentifier[i] = "untitled " + i;  // コンダクタートラックに関する処理をどうするか
                }
            }
            else
            {
                // null以外が与えられた場合はそれに従う
            }
            #endregion

            #region 何か書き出しっぽいの
            String[][] TextFormatOut = new String[MMLs.Count][];
            for (int i = 0; i < MMLs.Count; i++)
            {
                TextFormatOut[i] = new String[2];
                TextFormatOut[i][0] = MidiTrackIdentifier[i];
                TextFormatOut[i][1] = MMLs[i];
            }
            String TextFormatOutStr = TextTransaction.JoinString(TextFormatOut,
                "\r\n========================================\r\n",
                "\r\n########################################\r\n");

            if (createExFiles)
            {
                FileIO.WriteAllText(PathBase + @"text0_stdout_part1_array.txt", TextFormatOutStr);
            }
            #endregion

            #region MMLから各出力ファイルへの変換（多分）
            MelodyWalker mw = new MelodyWalker();
            mw.VacantBMSChannelIdx = DefaultVacantBMSChannelIdx;
            mw.WavidSpacing = WavidSpacing;
            mw.NamingStrategy = namingStrategy;
            mw.MultiProcess(MMLs, MidiTrackIdentifier, normalizedTrackSettings, sequenceLayer, PathBase,
                createExFiles, ref VacantWavid, timebase, margintime_beats, out trackCsv, out isEmptyList, midi_bpm,
                ref ProgressBarValue, 0.10, 1.00);
            #endregion

            #region トラックリストファイルの作成。WaveSplitterで使用する（？？？？？？？？？？？？）
            {
                String Text1;
                SoundRunner sr = new SoundRunner();
                List<String> validTrackIds = new List<String>();
                for (int ii = 0; ii < MidiTrackIdentifier.Count; ii++)
                {
                    if (!isEmptyList[ii])
                    {
                        validTrackIds.Add(MidiTrackIdentifier[ii]);
                    }
                }

                if (createExFiles)
                {
                    sr.CreateText(validTrackIds.ToArray(), out Text1);
                    FileIO.WriteAllText(PathBase + @"wavesplitter_input.txt", Text1);
                }

                //FileStreamFactory.WriteAllText(
                //    PathBase + @"wavesplitter_tracklist.txt",
                //    validTrackIds.Join("\r\n"));
            }
            #endregion

            #region RedModeの場合のMidi書き出し処理

            // RedModeの場合はmidiをSplitしたものを提出する
            // ignoreListをちゃんと見て！

            if (normalizedTrackSettings.Any(x => x.Mode == TrackMode.Red))
            {
                MidiStruct ms2 = new MidiStruct(quantizedMidiStreamGenerator(), true);

                int margintime_beats_int = (int)Math.Ceiling(Convert.ToDouble(margintime_beats));
                MidiTrack.SPLIT_BEATS_INTERVAL = margintime_beats_int + 4;
                MidiTrack.SPLIT_BEATS_AUTOMATIONLEFT = 2;
                MidiTrack.SPLIT_BEATS_AUTOMATIONRIGHT = margintime_beats_int + 0;

                for (int trid = 0; trid < normalizedTrackSettings.Count; trid++)
                {
                    if (normalizedTrackSettings[trid].Ignore)
                    {
                        ms2.tracks[trid] = new MidiTrack(ms2.tracks[trid].Where(x => !(x is MidiEventNote)));
                    }
                }

                if (!sequenceLayer)  // シーケンスレイヤーとして書き出す（テンポチェンジを含む場合はチェックしてください）
                {
                    for (int i = 1; i < ms2.tracks.Count; i++)  // 1から処理
                    {
                        bool isChordMode = normalizedTrackSettings[i].IsChord;
                        ms2.tracks[i] = ms2.tracks[i].SplitNotes(ms2, isChordMode);  // コンダクタートラックはそのままにする(主にテンポ保持のため)
                    }
                }
                else
                {
                    // ノート数が5000を超える場合は中断しても良いと思う(でもノート数よりオートメーションが極端に多いと問題の解決にならない)
                    // 小節数が9999を超える場合はさすがに中断しよう
                    try
                    {
                        var directsum = MidiTrack.DirectSum(ms2.tracks);
                        List<bool> isChordList = TrackSettings.SelectFlags(normalizedTrackSettings, x => x.IsChord);
                        List<bool> isXChainList = TrackSettings.SelectFlags(normalizedTrackSettings, x => x.IsXChain);
                        var splitted = MidiTrack.SplitNotes(directsum, ms2, isChordList, isXChainList);
                        ms2.tracks = MidiTrack.DirectDifference(splitted);
                    }
                    catch (Exception e)
                    {
                        CoreInteraction.ShowMessage(e.ToString());
                    }
                }

                ms2.Export(neu.IFileStream(PathBase + @"text3_tanon_smf_red.mid", FileMode.Create, FileAccess.Write), true);
            }
            #endregion

            ProgressBarValue = 1.00;
            ProgressBarFinished = true;
        }
    }
}
