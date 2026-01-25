using System;
using System.IO;
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
                Tables = model.Tables.Select(t => ToTableInfo(t, path)).ToArray(),
                Relationships = model.Relationships.Select(r => ToRelationshipInfo(r, path)).ToArray(),
                Expressions = model.Expressions.Select(e => ToExpressionInfo(e, path)).ToArray(),
                Cultures = model.Cultures.Select(c => ToCultureInfo(c, path)).ToArray()
            };
        }

        private static TableInfo ToTableInfo(Table table, string basePath)
        {
            var tableFilePath = Path.Combine(basePath, $"tables/{table.Name}.tmdl");

            return new TableInfo
            {
                Name = table.Name,
                File = $"tables/{table.Name}.tmdl",
                Columns = table.Columns.Select(c => new ColumnInfo
                {
                    Name = c.Name,
                    DataType = c.DataType.ToString(),
                    IsHidden = c.IsHidden,
                    LineNumber = LineNumberFinder.FindLineNumber(tableFilePath, c.Name, "column")
                }).ToArray(),
                Measures = table.Measures.Select(m => new MeasureInfo
                {
                    Name = m.Name,
                    FormatString = m.FormatString,
                    LineNumber = LineNumberFinder.FindLineNumber(tableFilePath, m.Name, "measure")
                }).ToArray(),
                Partitions = table.Partitions.Select(p => new PartitionInfo
                {
                    Name = p.Name,
                    Mode = p.Mode.ToString(),
                    LineNumber = LineNumberFinder.FindLineNumber(tableFilePath, p.Name, "partition")
                }).ToArray()
            };
        }

        private static RelationshipInfo ToRelationshipInfo(Relationship relationship, string basePath)
        {
            var relationshipsFilePath = Path.Combine(basePath, "relationships.tmdl");
            
            return new RelationshipInfo
            {
                Id = relationship.Name ?? string.Empty,
                Name = GenerateRelationshipName(relationship),
                File = "relationships.tmdl",
                FromColumn = relationship.FromTable?.Name,
                ToColumn = relationship.ToTable?.Name,
                LineNumber = LineNumberFinder.FindLineNumber(relationshipsFilePath, relationship.Name, "relationship")
            };
        }

        private static string GenerateRelationshipName(Relationship relationship)
        {
            string fromTable = relationship.FromTable?.Name ?? "Unknown";
            string toTable = relationship.ToTable?.Name ?? "Unknown";
            return $"{fromTable} → {toTable}";
        }

        private static ExpressionInfo ToExpressionInfo(NamedExpression expression, string basePath)
        {
            var expressionsFilePath = Path.Combine(basePath, "expressions.tmdl");
            
            return new ExpressionInfo
            {
                Name = expression.Name,
                File = "expressions.tmdl",
                Kind = expression.Kind.ToString(),
                LineNumber = LineNumberFinder.FindLineNumber(expressionsFilePath, expression.Name, "expression")
            };
        }

        private static CultureInfo ToCultureInfo(Culture culture, string basePath)
        {
            var cultureFilePath = Path.Combine(basePath, $"cultures/{culture.Name}.tmdl");

            return new CultureInfo
            {
                Name = culture.Name,
                File = $"cultures/{culture.Name}.tmdl",
                LineNumber = LineNumberFinder.FindLineNumber(cultureFilePath, culture.Name, "cultureInfo")
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
