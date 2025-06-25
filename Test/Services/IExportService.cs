using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMaster
{
    public interface IExportService
    {
        void ExportJson(DataTable data, string tableName);
        DataTable ImportJson(string tableName);
    }
}
