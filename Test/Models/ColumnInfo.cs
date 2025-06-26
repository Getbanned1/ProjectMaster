using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectMaster
{
    public class ColumnInfo
    {
        public string Name { get; set; }
        public bool IsNullable { get; set; }
        public string DataType { get; set; }
        public string DefaultValue { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}
