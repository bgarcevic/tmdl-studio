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
        /// <summary>
        /// Deploys the TMDL model to the specified workspace.
        /// Uses Microsoft Fabric REST API for cross-platform compatibility (Windows, macOS, Linux).
        /// </summary>
        /// <param name="path">Path to the TMDL folder.</param>
        /// <param name="authConfig">Authentication configuration.</param>
        /// <returns>Deploy result with success status and change summary.</returns>
        public static DeployResult Deploy(string path, AuthConfig authConfig)
        {
            // Use Fabric REST API for deployment
            // This works cross-platform without requiring Windows-only components
            return FabricApiService.DeployAsync(path, authConfig).GetAwaiter().GetResult();
        }

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
                if (!Directory.Exists(path))
                {
                    return TmdlStudio.Models.ValidationResult.Error($"Path does not exist: {path}");
                }

                var tmdlFiles = Directory.GetFiles(path, "*.tmdl", SearchOption.AllDirectories);
                if (tmdlFiles.Length == 0)
                {
                    return TmdlStudio.Models.ValidationResult.Error($"No TMDL files found in: {path}");
                }

                var model = LoadModelObject(path);

                if (model.Tables.Count == 0)
                {
                    return TmdlStudio.Models.ValidationResult.Warning($"Model '{model.Name}' loaded but contains no tables");
                }

                return TmdlStudio.Models.ValidationResult.Success($"Model '{model.Name}' validated. Tables: {model.Tables.Count}, Measures: {model.Tables.Sum(t => t.Measures.Count)}");
            }
            catch (Exception ex)
            {
                return TmdlStudio.Models.ValidationResult.Error($"Validation failed: {ex.Message}");
            }
        }
    }
}
