using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Mid2BMS
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            RedoRequired = false;
            InitializeComponent();
            dataGridView1.CurrentCellDirtyStateChanged += dataGridView1_CurrentCellDirtyStateChanged;
            dataGridView1.CellValueChanged += dataGridView1_CellValueChanged;
        }

        DataSet data_set;
        DataTable data_table;

        //**************************************************
        //********** 無効なチェックの組み合わせ ************
        // Chord + Drums は不可 (意味がないため)
        // Ignore + その他 は不可 (Ignoreが優先されるため)
        // XChain + その他 は不可 (サイドチェイン以外は無視されるため)
        // Purple + Chord は不可 (ポルタメントを適用する順序が一意でないため)
        // XChain は RedMode かつシーケンスレイヤーの場合のみ可
        //**************************************************

        //################ 入出力パラメータ ################
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public String TrackName_csv { get; set; }  // 親フォーム(Form1)から値を受け取る
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<String> TrackNames { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<String> InstrumentNames { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<TrackSettings> TrackSettings { get; private set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<bool> IsDrumsList { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<bool> IgnoreList { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<bool> IsChordList { get; set; }
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<bool> IsXChainList { get; set; }  // RedModeとシーケンスレイヤーの両方が（？）選択されている場合に、サイドチェイントリガノーツとして扱う
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<bool> IsOneShotList { get; set; }  // Midiノーツの長さを無視して処理する

        //################# 出力パラメータ #################
        public bool RedoRequired { get; private set; }  // 初期値はfalseかな？

        //################# 入力パラメータ #################
        private bool IsSequenceLayer;
        private bool IsRedMode;
        private bool IsPurpleMode;

        //##################################################

        bool changeEnabled = false;
        bool updatingModeAvailability = false;

        const int COLUMN_MODE = 6;
        const int COLUMN_DRUMS = 7;
        const int COLUMN_ONESHOT = 8;
        const int COLUMN_CHORD = 9;
        const int COLUMN_IGNORE = 10;
        const int COLUMN_XCHAIN = 11;
        
        public void SetMode(bool isSequenceLayer, bool isRedMode, bool isPurpleMode)
        {
            // private get; set; は許されるのか！？ いや、許されない気がする・・・
            IsSequenceLayer = isSequenceLayer;
            IsRedMode = isRedMode;
            IsPurpleMode = isPurpleMode;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (changeEnabled == false)
            {
                // 1回目のクリック「終了」
                this.Close();
            }
            else
            {
                //######## パラメータの妥当性のチェック ########
                for (int i = 0; i < data_table.Rows.Count; i++)
                {
                    TrackMode mode;
                    if (!Enum.TryParse(data_table.Rows[i][COLUMN_MODE].ToString(), out mode))
                    {
                        MessageBox.Show(this, "Modeを選択してください。", "Invalid Parameter");
                        return;
                    }
                    bool isDrums = (bool)data_table.Rows[i][COLUMN_DRUMS];
                    bool isOneShot = (bool)data_table.Rows[i][COLUMN_ONESHOT];
                    bool isChord = (bool)data_table.Rows[i][COLUMN_CHORD];
                    bool ignore = (bool)data_table.Rows[i][COLUMN_IGNORE];
                    bool isXChain = (bool)data_table.Rows[i][COLUMN_XCHAIN];
                    string validationError = ValidateTrackSettings(new TrackSettings
                    {
                        Mode = mode,
                        IsDrums = isDrums,
                        IsOneShot = isOneShot,
                        IsChord = isChord,
                        Ignore = ignore,
                        IsXChain = isXChain,
                    }, IsSequenceLayer);
                    if (validationError != null)
                    {
                        MessageBox.Show(this, validationError, "Invalid Parameter");
                        return;
                    }
                }

                //######## 呼び出し元(Form1)への戻り値の設定 ########
                RedoRequired = true;

                TrackNames = new List<String>(TrackNames);

                // http://stackoverflow.com/questions/3363940/fill-listint-with-default-values
                // Enumerable.Repeatよりもこの方が僅かに速いらしい

                IsDrumsList = new List<bool>(new bool[TrackNames.Count]);
                IgnoreList = new List<bool>(new bool[TrackNames.Count]);
                IsChordList = new List<bool>(new bool[TrackNames.Count]);
                IsXChainList = new List<bool>(new bool[TrackNames.Count]);
                IsOneShotList = new List<bool>(new bool[TrackNames.Count]);
                var settings = Enumerable.Range(0, TrackNames.Count)
                    .Select(_ => new TrackSettings { Mode = GetGlobalTrackMode() }).ToArray();

                for (int i = 0; i < data_table.Rows.Count; i++)
                {
                    int tracknumber = (System.Int32)(data_table.Rows[i][0]);
                    TrackNames[tracknumber] = data_table.Rows[i][5].ToString();
                    TrackMode mode = (TrackMode)Enum.Parse(typeof(TrackMode), data_table.Rows[i][COLUMN_MODE].ToString());
                    IsDrumsList[tracknumber] = (bool)data_table.Rows[i][COLUMN_DRUMS];
                    IsOneShotList[tracknumber] = (bool)data_table.Rows[i][COLUMN_ONESHOT];
                    IsChordList[tracknumber] = (bool)data_table.Rows[i][COLUMN_CHORD];
                    IgnoreList[tracknumber] = (bool)data_table.Rows[i][COLUMN_IGNORE];
                    IsXChainList[tracknumber] = (bool)data_table.Rows[i][COLUMN_XCHAIN];
                    settings[tracknumber] = new TrackSettings
                    {
                        Mode = mode,
                        IsDrums = IsDrumsList[tracknumber],
                        IsOneShot = IsOneShotList[tracknumber],
                        IsChord = IsChordList[tracknumber],
                        Ignore = IgnoreList[tracknumber],
                        IsXChain = IsXChainList[tracknumber],
                    };
                }
                TrackSettings = Array.AsReadOnly(settings);

                //######## フォームを閉じる ########
                this.Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (changeEnabled == false)
            {
                // 1回目のクリック「変更する」
                button1.Text = "Apply Changes (適用)";
                button2.Text = "Cancel (キャンセル)";
                changeEnabled = true;
                SetTable(true);
            }
            else
            {
                // 2回目のクリック「キャンセル」
                if (MessageBox.Show(this, "変更を取り消して操作を完了しますか？", "Cancel and Close", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                {
                    this.Close();
                }
            }

            return;
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            SetTable(false);
        }

        private TrackMode GetGlobalTrackMode()
        {
            return Mid2BMS.TrackSettings.FromLegacyGlobalMode(IsRedMode, IsPurpleMode);
        }

        internal static string ValidateTrackSettings(TrackSettings settings, bool isSequenceLayer)
        {
            if (settings.IsChord && settings.IsDrums)
                return "Chord? と Drums? を同時にチェックすることはできません。設定を確認してください。";
            if (settings.Ignore && (settings.IsOneShot || settings.IsChord || settings.IsDrums || settings.IsXChain))
                return "Ignore? がチェックされる場合、これは単独でチェックされなければなりません。設定を確認してください。";
            if (settings.IsXChain && (settings.IsOneShot || settings.IsChord || settings.IsDrums))
                return "XChain? がチェックされる場合、これは単独でチェックされなければなりません。設定を確認してください。";
            if (settings.Mode == TrackMode.Purple && settings.IsChord)
                return "PurpleのトラックではChord?を使用できません。設定を確認してください。";
            if (settings.IsXChain && (settings.Mode != TrackMode.Red || !isSequenceLayer))
                return "XChain? はRedのトラックかつSequenceLayer有効時のみ使用できます。設定を確認してください。";
            return null;
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.IsCurrentCellDirty)
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (!changeEnabled || updatingModeAvailability || e.RowIndex < 0 || e.ColumnIndex != COLUMN_MODE)
                return;
            UpdateRowModeAvailability(dataGridView1.Rows[e.RowIndex]);
        }

        private void UpdateAllRowModeAvailability()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
                UpdateRowModeAvailability(row);
        }

        private void UpdateRowModeAvailability(DataGridViewRow row)
        {
            TrackMode mode;
            if (row.IsNewRow || !Enum.TryParse(Convert.ToString(row.Cells[COLUMN_MODE].Value), out mode))
                return;

            updatingModeAvailability = true;
            try
            {
                SetCellAvailability(row.Cells[COLUMN_CHORD], mode != TrackMode.Purple);
                SetCellAvailability(row.Cells[COLUMN_XCHAIN], mode == TrackMode.Red && IsSequenceLayer);
            }
            finally
            {
                updatingModeAvailability = false;
            }
        }

        private static void SetCellAvailability(DataGridViewCell cell, bool enabled)
        {
            if (!enabled && cell.Value is bool && (bool)cell.Value)
                cell.Value = false;
            cell.ReadOnly = !enabled;
            cell.Style.BackColor = enabled ? SystemColors.Window : SystemColors.Control;
            cell.Style.ForeColor = enabled ? SystemColors.WindowText : SystemColors.GrayText;
        }

        private void ConfigureModeColumn()
        {
            DataGridViewColumn generatedColumn = dataGridView1.Columns[COLUMN_MODE];
            dataGridView1.Columns.Remove(generatedColumn);

            var modeColumn = new DataGridViewComboBoxColumn
            {
                Name = "Mode",
                HeaderText = "Mode",
                DataPropertyName = "Mode",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Flat,
                ValueType = typeof(string),
            };
            modeColumn.Items.AddRange(Enum.GetNames(typeof(TrackMode)));
            dataGridView1.Columns.Insert(COLUMN_MODE, modeColumn);
        }

        private void SetTable(bool showDetail) {
            // ん、datagridとdatagridviewって違うのか
            // http://msdn.microsoft.com/ja-jp/library/ms171628(v=vs.110).aspx


            //ヘッダーとすべてのセルの内容に合わせて、列の幅を自動調整する
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            //ヘッダーとすべてのセルの内容に合わせて、行の高さを自動調整する
            //dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;


            // データセットの作成
            data_set = new DataSet("default_set");

            // データテーブルの作成
            data_table = new DataTable("default_table");

            // データテーブルをデータセットに登録
            data_set.Tables.Add(data_table);

            // データテーブルにカラムを作成・登録
            data_table.Columns.Add("Tr", Type.GetType("System.Int32"));  // 数字の順にソート出来るようにする
            data_table.Columns.Add("wavs", Type.GetType("System.Int32"));
            data_table.Columns.Add("notes", Type.GetType("System.Int32"));
            if (showDetail)
            {
                data_table.Columns.Add("TrackName", Type.GetType("System.String"));
                data_table.Columns.Add("InstName", Type.GetType("System.String"));
                data_table.Columns.Add("New TrackName (Edit This Col)", Type.GetType("System.String"));
                data_table.Columns.Add("Mode", Type.GetType("System.String"));
                data_table.Columns.Add("Drums?", Type.GetType("System.Boolean"));
                data_table.Columns.Add("OneShot?", Type.GetType("System.Boolean"));
                data_table.Columns.Add("Chord?", Type.GetType("System.Boolean"));
                data_table.Columns.Add("Ignore?", Type.GetType("System.Boolean"));
                data_table.Columns.Add("XChain?", Type.GetType("System.Boolean"));
            }
            else
            {
                data_table.Columns.Add("TrackName", Type.GetType("System.String"));
            }

            // データテーブルのプライマリーキー（主キー）を設定
            data_table.PrimaryKey = new DataColumn[] { data_table.Columns[0] };

            String[] myrows = (TrackName_csv ?? "").Split(new String[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            DataRow data_row;

            for (int rowi = 0; rowi < myrows.Length - 2; rowi++)
            {
                String[] mycells = myrows[rowi + 2].Split(new String[] { "\t" }, StringSplitOptions.None);

                if (Convert.ToInt32(mycells[2]) != 0)  // ノート数が0なら無視する。従ってコンダクタートラックも無視され、曲名の変更は出来ません
                {
                    // データ行の作成とテーブルへの登録　その１
                    data_row = data_table.NewRow();
                    data_row[0] = Convert.ToInt32(mycells[0]);  // Convert.ToInt32は無くてもおｋ？
                    data_row[1] = Convert.ToInt32(mycells[1]);
                    data_row[2] = Convert.ToInt32(mycells[2]);
                    if (showDetail)
                    {
                        data_row[3] = TrackNames[rowi];
                        data_row[4] = InstrumentNames[rowi];
                        data_row[5] = mycells[3];
                        data_row[COLUMN_MODE] = GetGlobalTrackMode().ToString();
                        data_row[COLUMN_DRUMS] = false;
                        data_row[COLUMN_ONESHOT] = false;
                        data_row[COLUMN_CHORD] = false;
                        data_row[COLUMN_IGNORE] = false;
                        data_row[COLUMN_XCHAIN] = false;
                    }
                    else
                    {
                        data_row[3] = mycells[3];
                    }
                    data_table.Rows.Add(data_row);
                }
            }

            // DataGridViewにデータセットを設定
            dataGridView1.DataMember = data_set.Tables[0].TableName;
            dataGridView1.DataSource = data_set;

            // ファイル名だけ編集可能にする
            dataGridView1.Columns[0].ReadOnly = true;
            dataGridView1.Columns[1].ReadOnly = true;
            dataGridView1.Columns[2].ReadOnly = true;

            if (showDetail)
            {
                ConfigureModeColumn();
                dataGridView1.Columns[3].ReadOnly = true;
                dataGridView1.Columns[4].ReadOnly = true;
                dataGridView1.Columns[5].ReadOnly = false;
                dataGridView1.Columns[COLUMN_MODE].ReadOnly = false;
                dataGridView1.Columns[COLUMN_DRUMS].ReadOnly = false;
                dataGridView1.Columns[COLUMN_ONESHOT].ReadOnly = false;
                dataGridView1.Columns[COLUMN_CHORD].ReadOnly = false;
                dataGridView1.Columns[COLUMN_IGNORE].ReadOnly = false;
                dataGridView1.Columns[COLUMN_XCHAIN].ReadOnly = false;

                dataGridView1.Columns[COLUMN_XCHAIN].Visible = IsSequenceLayer;
                UpdateAllRowModeAvailability();
            }
            else
            {
                dataGridView1.Columns[3].ReadOnly = true;
            }
            
            
        }
    }
}
