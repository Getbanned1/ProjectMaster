using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMaster
{
    public interface ITableService
    {
        ObservableCollection<Table> LoadTables();
        DataTable LoadTableData(string tableName);
    }
}
