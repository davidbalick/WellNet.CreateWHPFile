using System.Windows;

namespace WellNet.CreateWHPFile
{
    public partial class MainWindow : Window
    {
        private ViewModel _viewModel;
        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new ViewModel();
            DataContext = _viewModel;
            TbExcelFilename.Drop += TbExcelFilename_Drop;
            TbTextFilename.Drop += TbTextFilename_Drop;
            TbScriptFilename.Drop += TbScriptFilename_Drop;
        }

        private void TbTextFilename_Drop(object sender, DragEventArgs e)
        {
            _viewModel.TextFilename = InterpretDragDropData(e.Data);
        }
        private void TbExcelFilename_Drop(object sender, DragEventArgs e)
        {
            _viewModel.ExcelFilename = InterpretDragDropData(e.Data);
        }
        private void TbScriptFilename_Drop(object sender, DragEventArgs e)
        {
            _viewModel.ScriptFilename = InterpretDragDropData(e.Data);
        }

        private static string InterpretDragDropData(IDataObject data)
        {
            var dataFormat = DataFormats.Text;
            if (data.GetDataPresent(dataFormat))
                return data.GetData(DataFormats.Text, true) as string;
            dataFormat = DataFormats.FileDrop;
            if (data.GetDataPresent(dataFormat))
                return (data.GetData(dataFormat, true) as string[])[0];
            return null;
        }
    }
}
