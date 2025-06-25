using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMaster
{
    public interface IDatabaseService
    {
        string ConnectionString { get; set; }
        string ConnectionStatus { get; set; }
        ObservableCollection<Table> GetTables();
        DataTable GetTableData(string tableName);
        bool ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null);
        NpgsqlDataAdapter GetDataAdapter(string tableName);
    }
}
