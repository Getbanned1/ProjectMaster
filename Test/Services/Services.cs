using Microsoft.Win32;
using Newtonsoft.Json;
using Npgsql;
using ProjectMaster;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMaster
{
    // DatabaseService.cs
    public class DatabaseService : IDatabaseService, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _connectionStatus = "Не подключено";
        private string _connectionString = "Host=localhost;Port=5432;Database=ProjectMaster;Username=postgres;Password=1472";

        public string ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Table> GetTables()
        {
            var tables = new ObservableCollection<Table>();

            using (var conn = new NpgsqlConnection(_connectionString))
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

            return tables;
        }

        public DataTable GetTableData(string tableName)
        {
            var data = new DataTable();

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand($"SELECT * FROM {tableName}", conn);
                    var da = new NpgsqlDataAdapter(cmd);
                    da.Fill(data);
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка загрузки: {ex.Message}";
                }
            }

            return data;
        }

        public bool ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                    var cmd = new NpgsqlCommand(sql, conn);

                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception ex)
                {
                    ConnectionStatus = $"Ошибка: {ex.Message}";
                    return false;
                }
            }
        }

        public NpgsqlDataAdapter GetDataAdapter(string tableName, NpgsqlConnection connection)
        {
            return new NpgsqlDataAdapter($"SELECT * FROM {tableName}", connection);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // TableService.cs
    public class TableService : ITableService
    {
        private readonly IDatabaseService _databaseService;

        public TableService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public ObservableCollection<Table> LoadTables()
        {
            return _databaseService.GetTables();
        }

        public DataTable LoadTableData(string tableName)
        {
            return _databaseService.GetTableData(tableName);
        }
    }

    // RecordService.cs
    public class RecordService : IRecordService
    {
        private readonly IDatabaseService _databaseService;

        public RecordService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void AddRecord(string tableName)
        {
            var columnsInfo = GetColumnInfo(tableName);
            var columns = new List<string>();
            var parameters = new Dictionary<string, object>();
            int paramCounter = 0;

            foreach (var col in columnsInfo)
            {
                // Пропускаем автоинкрементные столбцы и столбцы с DEFAULT
                if (col.IsIdentity || !string.IsNullOrEmpty(col.DefaultValue))
                    continue;

                // Добавляем столбец в список
                columns.Add($"\"{col.Name}\"");

                // Определяем значение для вставки
                object value = DBNull.Value;

                if (!col.IsNullable && string.IsNullOrEmpty(col.DefaultValue) && !col.IsIdentity)
                {
                    // Для NOT NULL колонок без значения по умолчанию и не автоинкрементных
                    value = GetDefaultValueByType(col.DataType);
                }

                parameters.Add($"@p{paramCounter}", value);
                paramCounter++;
            }

            if (columns.Count > 0)
            {
                var columnsStr = string.Join(", ", columns);
                var valuesStr = string.Join(", ", parameters.Select(p => p.Key));
                var sql = $"INSERT INTO \"{tableName}\" ({columnsStr}) VALUES ({valuesStr})";
                _databaseService.ExecuteNonQuery(sql, parameters);
            }
            else
            {
                // Если все колонки имеют значения по умолчанию или автоинкрементные
                var sql = $"INSERT INTO \"{tableName}\" DEFAULT VALUES";
                _databaseService.ExecuteNonQuery(sql);
            }
        }

        public void SaveRecord(string tableName, DataTable data)
        {
            using (var conn = new NpgsqlConnection(_databaseService.ConnectionString))
            {
                conn.Open();
                var da = _databaseService.GetDataAdapter(tableName, conn);
                var cb = new NpgsqlCommandBuilder(da);
                da.Update(data);
                data.AcceptChanges();
            } // Соединение автоматически закрывается здесь
        }

        public void DeleteRecord(string tableName, DataRowView selectedRow)
        {
            if (selectedRow == null) return;

            // Получаем информацию о таблице для определения PK
            var columnsInfo = GetColumnInfo(tableName);
            var primaryKey = columnsInfo.FirstOrDefault(c => c.IsPrimaryKey)?.Name ?? columnsInfo[0].Name;
            var id = selectedRow.Row[primaryKey];

            var sql = $"DELETE FROM \"{tableName}\" WHERE \"{primaryKey}\" = @id";
            var parameters = new Dictionary<string, object> { { "@id", id } };
            _databaseService.ExecuteNonQuery(sql, parameters);
        }

            private List<ColumnInfo> GetColumnInfo(string tableName)
            {
                var columnsInfo = new List<ColumnInfo>();

                using (var conn = new NpgsqlConnection(_databaseService.ConnectionString))
                {
                    conn.Open();
                    // Исправленный SQL с параметром
                    var cmd = new NpgsqlCommand(
                        @"SELECT 
                    column_name, 
                    is_nullable, 
                    data_type,
                    column_default,
                    is_identity,
                    EXISTS (
                        SELECT 1 
                        FROM information_schema.key_column_usage 
                        WHERE table_name = @tableName 
                          AND column_name = c.column_name
                    ) as is_primary_key
                   FROM information_schema.columns c
                   WHERE table_name = @tableName 
                     AND table_schema = 'public'",
                        conn);

                    // Добавляем параметр
                    cmd.Parameters.AddWithValue("tableName", tableName);

                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        columnsInfo.Add(new ColumnInfo
                        {
                            Name = reader["column_name"].ToString(),
                            IsNullable = reader["is_nullable"].ToString() == "YES",
                            DataType = reader["data_type"].ToString(),
                            DefaultValue = reader["column_default"]?.ToString(),
                            IsIdentity = reader["is_identity"].ToString() == "YES",
                            IsPrimaryKey = (bool)reader["is_primary_key"] 
                        });
                    }
                }

                return columnsInfo;
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
    }

    // ExportService.cs
    public class ExportService : IExportService
    {
        private readonly IDatabaseService _databaseService;

        public ExportService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void ExportJson(DataTable data, string tableName)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    FileName = $"{tableName}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    System.IO.File.WriteAllText(saveDialog.FileName, json);
                    _databaseService.ConnectionStatus = "Таблица экспортирована в JSON";
                }
            }
            catch (Exception ex)
            {
                _databaseService.ConnectionStatus = $"Ошибка экспорта: {ex.Message}";
            }
        }

        public DataTable ImportJson(string tableName)
        {
            var data = new DataTable(tableName);

            try
            {
                var openDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };

                if (openDialog.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(openDialog.FileName);

                    // Получаем структуру таблицы из БД
                    using (var conn = new NpgsqlConnection(_databaseService.ConnectionString))
                    {
                        conn.Open();
                        var cmd = new NpgsqlCommand($"SELECT * FROM {tableName} LIMIT 1", conn);
                        var da = new NpgsqlDataAdapter(cmd);
                        da.Fill(data);
                    }

                    // Очищаем данные, оставляя структуру
                    data.Rows.Clear();

                    var rows = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                    foreach (var row in rows)
                    {
                        // Проверяем, есть ли уже такая запись (например, по первичному ключу)
                        // Предположим, что у нас есть столбец "id" как первичный ключ
                        if (row.ContainsKey("id"))
                        {
                            var id = row["id"];
                            DataRow existingRow = null;

                            // Если у вас уже есть данные в DataTable, ищем совпадение
                            if (data.Rows.Count > 0)
                            {
                                existingRow = data.Rows.Cast<DataRow>()
                                    .FirstOrDefault(r => r["id"].Equals(id));
                            }

                            if (existingRow != null)
                            {
                                // Обновляем существующую запись
                                foreach (var item in row)
                                {
                                    if (data.Columns.Contains(item.Key))
                                    {
                                        existingRow[item.Key] = item.Value ?? DBNull.Value;
                                    }
                                }
                            }
                            else
                            {
                                // Добавляем новую запись
                                var newRow = data.NewRow();
                                foreach (var item in row)
                                {
                                    if (data.Columns.Contains(item.Key))
                                    {
                                        newRow[item.Key] = item.Value ?? DBNull.Value;
                                    }
                                }
                                data.Rows.Add(newRow);
                            }
                        }
                        else
                        {
                            // Если нет первичного ключа, просто добавляем как новую запись
                            var newRow = data.NewRow();
                            foreach (var item in row)
                            {
                                if (data.Columns.Contains(item.Key))
                                {
                                    newRow[item.Key] = item.Value ?? DBNull.Value;
                                }
                            }
                            data.Rows.Add(newRow);
                        }
                    }

                    _databaseService.ConnectionStatus = "Данные импортированы";
                }
            }
            catch (Exception ex)
            {
                _databaseService.ConnectionStatus = $"Ошибка импорта: {ex.Message}";
            }

            return data;
        }
    }
}
