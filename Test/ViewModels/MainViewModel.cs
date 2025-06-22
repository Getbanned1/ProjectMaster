using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Data;
using Npgsql;
using System;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System.Text.Json.Serialization;
using System.Xml;
using Newtonsoft.Json;

namespace ProjectMaster
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Table> _tables;
        private Table _selectedTable;
        private DataTable _tableData;
        private string _connectionStatus = "Не подключено";
        string connStr = "Host=localhost;Port=5432;Database=ProjectMaster;Username=postgres;Password=1472";
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
        private void LoadTables()
        {
            // Пример подключения - укажите свою connection string

            var tables = new ObservableCollection<Table>();

            using (var conn = new NpgsqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand(
                        "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'",
                        conn);
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        tables.Add(new Table { TableName = reader["table_name"].ToString() });
                    }
                    ConnectionStatus = "Подключено";
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка: {ex.Message}";
                }
            }

            Tables = tables;
        }

        private void LoadTableData()
        {
            if (SelectedTable == null) return;

            // Используем ту же connection string
            var data = new DataTable();

            using (var conn = new NpgsqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand($"SELECT * FROM {SelectedTable.TableName}", conn);
                    var da = new NpgsqlDataAdapter(cmd);
                    da.Fill(data);
                    TableData = data;
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка загрузки: {ex.Message}";
                }
            }
        }

        // Модель данных для таблицы
        public class Table
        {
            public string TableName { get; set; }
        }

        // Реализация INotifyPropertyChanged
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private DataRowView _selectedRow;

        public DataRowView SelectedRow
        {
            get => _selectedRow;
            set { _selectedRow = value; OnPropertyChanged(); }
        }

        // Команды CRUD
        public ICommand AddRecordCommand { get; }
        public ICommand SaveRecordCommand { get; }
        public ICommand DeleteRecordCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand ImportJsonCommand { get; }

        public MainViewModel()
        {
            // Инициализация команд
            AddRecordCommand = new RelayCommand(AddRecord);
            SaveRecordCommand = new RelayCommand(SaveRecord);
            DeleteRecordCommand = new RelayCommand(DeleteRecord);
            ExportJsonCommand = new RelayCommand(ExportJson);
            ImportJsonCommand = new RelayCommand(ImportJson);

            LoadTables();
        }

        private void AddRecord(object obj)
        {
            if (SelectedTable == null) return;

            string connStr = "Host=localhost;Port=5432;Database=ProjectMaster;Username=postgres;Password=1472";

            using (var conn = new NpgsqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // 1. Получаем информацию о колонках таблицы
                    var columnsInfo = new List<ColumnInfo>();
                    var cmd = new NpgsqlCommand(
                        $@"SELECT 
                    column_name, 
                    is_nullable, 
                    data_type,
                    column_default,
                    is_identity
                   FROM information_schema.columns 
                   WHERE table_name = @tableName 
                     AND table_schema = 'public'",
                        conn);
                    cmd.Parameters.AddWithValue("tableName", SelectedTable.TableName);
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var colInfo = new ColumnInfo
                        {
                            Name = reader["column_name"].ToString(),
                            IsNullable = reader["is_nullable"].ToString() == "YES",
                            DataType = reader["data_type"].ToString(),
                            DefaultValue = reader["column_default"]?.ToString(),
                            IsIdentity = reader["is_identity"].ToString() == "YES"
                        };
                        columnsInfo.Add(colInfo);
                    }
                    reader.Close();

                    // 2. Формируем SQL для вставки
                    var columns = new List<string>();
                    var parameters = new List<NpgsqlParameter>();
                    int paramCounter = 0;

                    foreach (var col in columnsInfo)
                    {
                        // Пропускаем автоинкрементные столбцы и столбцы с DEFAULT
                        if (col.IsIdentity || !string.IsNullOrEmpty(col.DefaultValue))
                        {
                            continue; // Пропускаем столбец, он будет заполнен автоматически
                        }

                        // Добавляем столбец в список
                        columns.Add($"\"{col.Name}\"");

                        // Определяем значение для вставки
                        object value = DBNull.Value;

                        if (!col.IsNullable && string.IsNullOrEmpty(col.DefaultValue) && !col.IsIdentity)
                        {
                            // Для NOT NULL колонок без значения по умолчанию и не автоинкрементных
                            value = GetDefaultValueByType(col.DataType);
                        }

                        parameters.Add(new NpgsqlParameter($"@p{paramCounter}", value ?? DBNull.Value));
                        paramCounter++;
                    }

                    // 3. Формируем запрос
                    if (columns.Count > 0)
                    {
                        var columnsStr = string.Join(", ", columns);
                        var valuesStr = string.Join(", ", parameters.Select(p => p.ParameterName));

                        var insertCmd = new NpgsqlCommand(
                            $"INSERT INTO \"{SelectedTable.TableName}\" ({columnsStr}) VALUES ({valuesStr})",
                            conn);

                        foreach (var param in parameters)
                        {
                            insertCmd.Parameters.Add(param);
                        }

                        insertCmd.ExecuteNonQuery();
                        LoadTableData(); // Обновляем данные
                    }
                    else
                    {
                        // Если все колонки имеют значения по умолчанию или автоинкрементные
                        cmd = new NpgsqlCommand(
                            $"INSERT INTO \"{SelectedTable.TableName}\" DEFAULT VALUES",
                            conn);
                        cmd.ExecuteNonQuery();
                        LoadTableData();
                    }
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка: {ex.Message}";
                }
            }
        }
        private object GetDefaultValueByType(string dataType)
        {
            switch (dataType.ToLower())
            {
                case "integer":
                case "int4":
                case "int":
                case "smallint":
                case "int2":
                case "bigint":
                case "int8":
                    return 0; // Числовые типы - 0

                case "numeric":
                case "decimal":
                    return 0.0m;

                case "real":
                case "float4":
                case "double precision":
                case "float8":
                    return 0.0;

                case "boolean":
                case "bool":
                    return false;

                case "date":
                case "timestamp":
                case "timestamptz":
                    return DateTime.Now; // Текущая дата/время

                case "text":
                case "character varying":
                case "varchar":
                case "character":
                case "char":
                    return "Новая запись"; // Текст по умолчанию

                // Добавьте другие типы по необходимости
                default:
                    return DBNull.Value; // Для неизвестных типов - NULL
            }
        }

        private void SaveRecord(object obj)
        {
            if (SelectedTable == null || TableData == null) return;

            string connStr = "Host=localhost;Port=5432;Database=ProjectMaster;Username=postgres;Password=1472";

            using (var conn = new NpgsqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // Создаем DataAdapter
                    var da = new NpgsqlDataAdapter($"SELECT * FROM \"{SelectedTable.TableName}\"", conn);
                    var cb = new NpgsqlCommandBuilder(da);
                    da.Update(TableData);

                    TableData.AcceptChanges();
                    ConnectionStatus = "Изменения сохранены";
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка сохранения: {ex.Message}";
                    TableData.RejectChanges();
                }
            }
        }
        private void DeleteRecord(object obj)
        {
            if (SelectedRow == null || SelectedTable == null) return;

            try
            {
                var primaryKey = TableData.Columns[0];
                var id = SelectedRow.Row[primaryKey];

                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand(
                        $"DELETE FROM \"{SelectedTable.TableName}\" WHERE \"{primaryKey}\" = @id",
                        conn);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.ExecuteNonQuery();
                    LoadTableData(); // Обновляем данные
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Ошибка удаления: {ex.Message}";
            }
        }

        private void ExportJson(object obj)
        {
            if (SelectedTable == null || TableData == null)
            {
                ConnectionStatus = "Ошибка: Таблица не выбрана";
                return;
            }

            try
            {
                // 1. Создаем диалог сохранения файла
                var saveDialog = new SaveFileDialog();
                saveDialog.Filter = "JSON files (*.json)|*.json";
                saveDialog.FileName = $"{SelectedTable.TableName}.json";

                if (saveDialog.ShowDialog() == true)
                {
                    // 2. Преобразуем DataTable в JSON
                    string json = JsonConvert.SerializeObject(TableData,
                        Newtonsoft.Json.Formatting.Indented);

                    // 3. Сохраняем в файл
                    System.IO.File.WriteAllText(saveDialog.FileName, json);

                    ConnectionStatus = "Таблица экспортирована в JSON";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Ошибка экспорта: {ex.Message}";
            }
        }

        private void ImportJson(object obj)
        {
            if (SelectedTable == null)
            {
                ConnectionStatus = "Ошибка: Таблица не выбрана";
                return;
            }

            try
            {
                var openDialog = new OpenFileDialog();
                openDialog.Filter = "JSON files (*.json)|*.json";

                if (openDialog.ShowDialog() == true)
                {
                    string json = System.IO.File.ReadAllText(openDialog.FileName);

                    // 1. Создаем DataTable с правильной структурой
                    var importedData = new DataTable(SelectedTable.TableName);

                    // 2. Копируем структуру из текущей таблицы
                    foreach (DataColumn col in TableData.Columns)
                    {
                        importedData.Columns.Add(col.ColumnName, col.DataType);
                    }

                    // 3. Заполняем данными из JSON
                    var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                    foreach (var row in rows)
                    {
                        var newRow = importedData.NewRow();
                        foreach (var item in row)
                        {
                            if (importedData.Columns.Contains(item.Key))
                            {
                                newRow[item.Key] = item.Value ?? DBNull.Value;
                            }
                        }
                        importedData.Rows.Add(newRow);
                    }

                    // 4. Обновляем данные
                    TableData.Clear();
                    foreach (DataRow row in importedData.Rows)
                    {
                        var newRow = TableData.NewRow();
                        newRow.ItemArray = row.ItemArray;
                        TableData.Rows.Add(newRow);
                    }
                    TableData.AcceptChanges();

                    ConnectionStatus = "Данные импортированы";
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Ошибка импорта: {ex.Message}";
            }
        }

        private bool CheckTableStructure(DataTable importedTable)
        {
            // Игнорируем имя таблицы, проверяем только столбцы
            if (importedTable.Columns.Count != TableData.Columns.Count)
                return false;

            foreach (DataColumn col in TableData.Columns)
            {
                if (!importedTable.Columns.Contains(col.ColumnName))
                    return false;
            }

            return true;
        }

    }

}
    // Реализация ICommand
    public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(object parameter) => _execute(parameter);
}
public class ColumnInfo
{
    public string Name { get; set; }
    public bool IsNullable { get; set; }
    public string DataType { get; set; }
    public string DefaultValue { get; set; }
    public bool IsIdentity { get; set; }
}