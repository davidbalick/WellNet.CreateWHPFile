using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using WellNet.Excel;
using WellNet.Sql;
using WellNet.Utils;

namespace WellNet.CreateWHPFile
{
    public class ViewModel : ViewModelBase
    {
        private const string WHP_CONN_NAME = "HSNavanex";
        private const string WHP_SPROC = "enroll.WHP_ElgibilityFile_V3";
        private const string UI_CONN_NAME = "HSNavanex_WorkingDB";
        private const string GROUPLIST_SPROC = "WellnetUi.selectGroupList";

        private BackgroundWorker _bgWorker;

        #region Properties
        private Dictionary<string, string> _connMgr;
        public Dictionary<string, string> Connections
        {
            get { return _connMgr; }
            set
            {
                _connMgr = value;
                OnPropertyChanged("Connections");
            }
        }
        private string _conn;
        public string Connection
        {
            get { return _conn; }
            set
            {
                _conn = value;
                OnPropertyChanged("Connection");
            }
        }
        private string _table;
        public string Table
        {
            get { return _table; }
            set
            {
                _table = value;
                OnPropertyChanged("Table");
            }
        }

        public RelayCommand CreateFilesCommand { get; set; }
        private List<Group> _groups = null;
        public List<Group> Groups
        {
            get { return _groups ?? (_groups = GetGroups()); }
            set
            {
                _groups = value;
                OnPropertyChanged("Groups");
            }
        }
        private Group _selectedGroup;
        public Group SelectedGroup
        {
            get { return _selectedGroup; }
            set
            {
                _selectedGroup = value;
                OnPropertyChanged("SelectedGroup");
            }
        }
        private string _excelFilename = null;
        public string ExcelFilename
        {
            get { return _excelFilename; }
            set
            {
                _excelFilename = Path.ChangeExtension(value, "xlsx");
                OnPropertyChanged("ExcelFilename");
            }
        }
        private string _textFilename = null;
        public string TextFilename
        {
            get { return _textFilename; }
            set
            {
                _textFilename = Path.ChangeExtension(value, "txt");
                OnPropertyChanged("TextFilename");
            }
        }
        private string _scriptFilename = null;
        public string ScriptFilename
        {
            get { return _scriptFilename; }
            set
            {
                _scriptFilename = Path.ChangeExtension(value, "sql");
                OnPropertyChanged("ScriptFilename");
            }
        }
        private DateTime? _dateOfInterest;
        public DateTime? DateOfInterest
        {
            get { return _dateOfInterest; }
            set
            {
                _dateOfInterest = value;
                OnPropertyChanged("DateOfInterest");
            }
        }
        private DateTime? _historyDate;
        public DateTime? HistoryDate
        {
            get { return _historyDate; }
            set
            {
                _historyDate = value;
                OnPropertyChanged("HistoryDate");
            }
        }
        private int? _effDateLookAhead;
        public int? EffectiveDateLookAheadDays
        {
            get { return _effDateLookAhead; }
            set
            {
                _effDateLookAhead = value;
                OnPropertyChanged("EffectiveDateLookAheadDays");
            }
        }
        private int? _termDateLookBack;
        public int? TermDateLookBackDays
        {
            get { return _termDateLookBack; }
            set
            {
                _termDateLookBack = value;
                OnPropertyChanged("TermdateLookBackDays");
            }
        }
        #endregion Properties

        public ViewModel()
        {
            _connMgr = ConnectionManager.Create();
            DateOfInterest = DateTime.Today;
            HistoryDate = DateTime.Today.AddDays(1);
            EffectiveDateLookAheadDays = 30;
            TermDateLookBackDays = 0;
            CreateFilesCommand = new RelayCommand(CreateFilesExec, CreateFilesCanExec);
            _bgWorker = new BackgroundWorker { WorkerReportsProgress = true };
            _bgWorker.DoWork += _bgWorker_DoWork;
            _bgWorker.RunWorkerCompleted += _bgWorker_RunWorkerCompleted;
            _bgWorker.ProgressChanged += _bgWorker_ProgressChanged;
        }

        private void _bgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Status = e.UserState.ToString();
        }

        private void _bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
                Status = e.Error.Message;
            else
                Status = "Completed";
        }

        private void _bgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _bgWorker.ReportProgress(0, "Getting Data...");
            var sqlHelper = SqlHelper.Create(_connMgr[WHP_CONN_NAME]);
            var cmd = sqlHelper.CreateStoredProcCommand(WHP_SPROC, true);
            cmd.Parameters["@ClientPrincipalID"].Value = SelectedGroup.Id;
            cmd.Parameters["@DateOfInterest"].Value = DateOfInterest.Value;
            cmd.Parameters["@HistoryDate"].Value = HistoryDate.Value;
            cmd.Parameters["@EffDateLookAhead"].Value = EffectiveDateLookAheadDays.Value;
            cmd.Parameters["@TermDateLookBackDays"].Value = TermDateLookBackDays.Value;
            var dataTable = sqlHelper.PopulateDataTable(cmd);
            _bgWorker.ReportProgress(0, "Creating Output...");
            if (!string.IsNullOrEmpty(TextFilename))
                DataToFile.TabDelimited(dataTable, TextFilename);
            if (!string.IsNullOrEmpty(ExcelFilename))
            {
                var dataSet = new DataSet();
                dataSet.Tables.Add(dataTable);
                var dataToExcel = new DataToExcel();
                dataToExcel.DataSetToExcel(dataSet, ExcelFilename, false);
            }
            if (!string.IsNullOrEmpty(ScriptFilename))
                DataToFile.SqlScript(dataTable, ScriptFilename, Table);
            if (!string.IsNullOrEmpty(Connection) && !string.IsNullOrEmpty(Table))
            {
                _bgWorker.ReportProgress(0, "Creating table...");
                string sql;
                if (!string.IsNullOrEmpty(ScriptFilename))
                    sql = File.ReadAllText(ScriptFilename);
                else
                {
                    var tempFile = Path.GetTempFileName();
                    DataToFile.SqlScript(dataTable, tempFile, Table);
                    sql = File.ReadAllText(tempFile);
                    File.Delete(tempFile);
                    sqlHelper = SqlHelper.Create(Connection);
                    cmd = sqlHelper.CreateTextCommand(sql);
                    sqlHelper.Execute(cmd);
                }
            }
        }

        private List<Group> GetGroups()
        {
            var sqlHelper = SqlHelper.Create(_connMgr[UI_CONN_NAME]);
            var cmd = sqlHelper.CreateStoredProcCommand(GROUPLIST_SPROC, false);
            var dataTable = sqlHelper.PopulateDataTable(cmd);
            return dataTable.Rows.Cast<DataRow>().Select(dr =>
                new Group { Id = new Guid(dr["ClientPrincipalID"].ToString()), Name = dr["GroupName"].ToString() }).ToList();
        }

        private bool CreateFilesCanExec(object arg)
        {
            return SelectedGroup != null && DateOfInterest.HasValue && HistoryDate.HasValue && EffectiveDateLookAheadDays.HasValue
                && TermDateLookBackDays.HasValue && !_bgWorker.IsBusy &&
                (
                    (!string.IsNullOrEmpty(ExcelFilename) || !string.IsNullOrEmpty(TextFilename) || !string.IsNullOrEmpty(ScriptFilename))
                    ||
                    (!string.IsNullOrEmpty(Connection) && !string.IsNullOrEmpty(Table))
                );
        }

        private void CreateFilesExec(object obj)
        {
            _bgWorker.RunWorkerAsync();
        }
    }
    public class Group
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
