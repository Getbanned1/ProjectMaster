using Npgsql.Replication.PgOutput.Messages;
using ProjectMaster;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Windows.Input;

namespace ProjectMaster
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Table> _tables;
        private Table _selectedTable;
        private DataTable _tableData;
        private string _connectionStatus = "Не подключено";
        private readonly IDatabaseService _databaseService;
        private readonly ITableService _tableService;
        private readonly IRecordService _recordService;
        private readonly IExportService _exportService;

        public ObservableCollection<Table> Tables
        {
            get => _tables;
            set { _tables = value; OnPropertyChanged(); }
        }

        public Table SelectedTable
        {
            get => _selectedTable;
            set
            {
                _selectedTable = value;
                OnPropertyChanged();
                LoadTableData();
            }
        }

        public DataTable TableData
        {
            get => _tableData;
            set { _tableData = value; OnPropertyChanged(); }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        public ICommand AddRecordCommand { get; }
        public ICommand SaveRecordCommand { get; }
        public ICommand DeleteRecordCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand ImportJsonCommand { get; }

        public MainViewModel()
        {
            // Внедрение зависимостей
            _databaseService = new DatabaseService();
            _tableService = new TableService(_databaseService);
            _recordService = new RecordService(_databaseService);
            _exportService = new ExportService(_databaseService);

            // Инициализация команд
            AddRecordCommand = new RelayCommand(AddRecord);
            SaveRecordCommand = new RelayCommand(SaveRecord);
            DeleteRecordCommand = new RelayCommand(DeleteRecord);
            ExportJsonCommand = new RelayCommand(ExportJson);
            ImportJsonCommand = new RelayCommand(ImportJson);

            // Загрузка таблиц
            Tables = _tableService.LoadTables();
            ConnectionStatus = _databaseService.ConnectionStatus;
        }

        private void LoadTableData()
        {
            if (SelectedTable == null) return;
            TableData = _tableService.LoadTableData(SelectedTable.TableName);
        }

        private void AddRecord(object obj)
        {
            if (SelectedTable == null) return;
            _recordService.AddRecord(SelectedTable.TableName);
            LoadTableData(); // Обновляем данные
        }

        private void SaveRecord(object obj)
        {
            if (SelectedTable == null || TableData == null) return;
            _recordService.SaveRecord(SelectedTable.TableName, TableData);
            ConnectionStatus = "Изменения сохранены";
        }

        private void DeleteRecord(object obj)
        {
            if (SelectedRow == null || SelectedTable == null) return;
            _recordService.DeleteRecord(SelectedTable.TableName, SelectedRow);
            LoadTableData(); // Обновляем данные
        }

        private void ExportJson(object obj)
        {
            if (SelectedTable == null || TableData == null)
            {
                ConnectionStatus = "Ошибка: Таблица не выбрана";
                return;
            }
            _exportService.ExportJson(TableData, SelectedTable.TableName);
        }

        private void ImportJson(object obj)
        {
            if (SelectedTable == null)
            {
                ConnectionStatus = "Ошибка: Таблица не выбрана";
                return;
            }
            TableData = _exportService.ImportJson(SelectedTable.TableName);
        }

        // Остальные свойства и методы из исходного MainViewModel
        private DataRowView _selectedRow;

        public DataRowView SelectedRow
        {
            get => _selectedRow;
            set { _selectedRow = value; OnPropertyChanged(); }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
