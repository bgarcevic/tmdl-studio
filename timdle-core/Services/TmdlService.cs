using System;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.Tabular.Tmdl;
using TmdlStudio.Models;

namespace TmdlStudio.Services
{
    public static class TmdlService
    {
        public static Database LoadModel(string path)
        {
            var database = TmdlSerializer.DeserializeDatabaseFromFolder(path);
            return database;
        }

        public static Model LoadModelObject(string path)
        {
            return LoadModel(path).Model;
        }

        public static ModelStructure ToModelStructure(Database database, string path)
        {
            var model = database.Model;

            return new ModelStructure
            {
                Name = database.Name,
                Path = path,
                Database = new DatabaseInfo
                {
                    Name = database.Name,
                    File = "database.tmdl"
                },
                Model = new ModelInfo
                {
                    Name = model.Name,
                    File = "model.tmdl"
                },
                Tables = model.Tables.Select(ToTableInfo).ToArray(),
                Relationships = model.Relationships.Select(ToRelationshipInfo).ToArray(),
                Expressions = model.Expressions.Select(e => new ExpressionInfo
                {
                    Name = e.Name,
                    File = "expressions.tmdl",
                    Kind = e.Kind.ToString()
                }).ToArray(),
                Cultures = model.Cultures.Select(ToCultureInfo).ToArray()
            };
        }

        private static TableInfo ToTableInfo(Table table)
        {
            return new TableInfo
            {
                Name = table.Name,
                File = $"tables/{table.Name}.tmdl",
                Columns = table.Columns.Select(c => new ColumnInfo
                {
                    Name = c.Name,
                    DataType = c.DataType.ToString(),
                    IsHidden = c.IsHidden
                }).ToArray(),
                Measures = table.Measures.Select(m => new MeasureInfo
                {
                    Name = m.Name,
                    FormatString = m.FormatString
                }).ToArray(),
                Partitions = table.Partitions.Select(p => new PartitionInfo
                {
                    Name = p.Name,
                    Mode = p.Mode.ToString()
                }).ToArray()
            };
        }

        private static RelationshipInfo ToRelationshipInfo(Relationship relationship)
        {
            return new RelationshipInfo
            {
                Id = relationship.Name ?? string.Empty,
                Name = GenerateRelationshipName(relationship),
                File = "relationships.tmdl",
                FromColumn = relationship.FromTable?.Name,
                ToColumn = relationship.ToTable?.Name
            };
        }

        private static string GenerateRelationshipName(Relationship relationship)
        {
            string fromTable = relationship.FromTable?.Name ?? "Unknown";
            string toTable = relationship.ToTable?.Name ?? "Unknown";
            return $"{fromTable} → {toTable}";
        }

        private static CultureInfo ToCultureInfo(Culture culture)
        {
            return new CultureInfo
            {
                Name = culture.Name,
                File = $"cultures/{culture.Name}.tmdl"
            };
        }

        public static string[] ListTables(string path)
        {
            var model = LoadModelObject(path);
            return model.Tables.Select(t => t.Name).ToArray();
        }

        public static TmdlStudio.Models.ValidationResult Validate(string path)
        {
            try
            {
                var model = LoadModelObject(path);
                return TmdlStudio.Models.ValidationResult.Success($"Model '{model.Name}' loaded. Tables: {model.Tables.Count}");
            }
            catch (Exception ex)
            {
                return TmdlStudio.Models.ValidationResult.Error(ex.Message);
            }
        }
    }
}
