using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private Dictionary<string, string> _connMgr;

        #region Properties
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
        #endregion Properties

        public ViewModel()
        {
            _connMgr = ConnectionManager.Create();
        }

        private List<Group> GetGroups()
        {
            var sqlHelper = SqlHelper.Create(_connMgr[UI_CONN_NAME]);
            var cmd = sqlHelper.CreateStoredProcCommand(GROUPLIST_SPROC, false);
            var dataTable = sqlHelper.PopulateDataTable(cmd);
            return dataTable.Rows.Cast<DataRow>().Select(dr =>
                new Group { Id = new Guid(dr["ClientPrincipalID"].ToString()), Name = dr["GroupName"].ToString() }).ToList();
        }
    }
    public class Group
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
