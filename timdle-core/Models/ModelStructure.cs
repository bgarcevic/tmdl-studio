using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TmdlStudio.Models
{
    public class ModelStructure
    {
        public string Name { get; set; }

        public string Path { get; set; }

        public DatabaseInfo Database { get; set; }

        public ModelInfo Model { get; set; }

        public TableInfo[] Tables { get; set; }

        public RelationshipInfo[] Relationships { get; set; }

        public ExpressionInfo[] Expressions { get; set; }

        public CultureInfo[] Cultures { get; set; }
    }

    public class DatabaseInfo
    {
        public string Name { get; set; }
        public string File { get; set; }
    }

    public class ModelInfo
    {
        public string Name { get; set; }
        public string File { get; set; }
    }

    public class TableInfo
    {
        public string Name { get; set; }
        public string File { get; set; }
        public ColumnInfo[] Columns { get; set; }
        public MeasureInfo[] Measures { get; set; }
        public PartitionInfo[] Partitions { get; set; }
    }

    public class ColumnInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool IsHidden { get; set; }
    }

    public class MeasureInfo
    {
        public string Name { get; set; }
        public string FormatString { get; set; }
    }

    public class PartitionInfo
    {
        public string Name { get; set; }
        public string Mode { get; set; }
    }

    public class RelationshipInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
        public string FromColumn { get; set; }
        public string ToColumn { get; set; }
    }

    public class ExpressionInfo
    {
        public string Name { get; set; }
        public string File { get; set; }
        public string Kind { get; set; }
    }

    public class CultureInfo
    {
        public string Name { get; set; }
        public string File { get; set; }
    }
}
