using System;
using System.IO;
using TmdlStudio.Services;
using Xunit;

namespace TmdlStudio.Tests.Services
{
    public class LineNumberFinderTests
    {
        [Fact]
        public void FindLineNumber_ColumnWithSimpleName_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    column SimpleColumn
        dataType: int64

    measure SimpleMeasure = SUM(1)
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "SimpleColumn", "column");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_ColumnWithSpaces_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    column 'Column With Spaces'
        dataType: int64
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "Column With Spaces", "column");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_ColumnWithSpecialChars_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    column 'My.Column'
        dataType: int64

    column 'Column:Name'
        dataType: string
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result1 = LineNumberFinder.FindLineNumber(tempFile, "My.Column", "column");
                var result2 = LineNumberFinder.FindLineNumber(tempFile, "Column:Name", "column");
                
                Assert.Equal(3, result1);
                Assert.Equal(6, result2);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_MeasureWithSimpleName_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    measure SimpleMeasure = SUM(1)
        formatString: $ #,##0
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "SimpleMeasure", "measure");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_MeasureWithSpecialChars_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    measure 'Measure Name' = SUM(1)
        formatString: $ #,##0
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "Measure Name", "measure");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_MeasureWithMultiLineExpression_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    measure ComplexMeasure =
        var result = SUMX(
            'TestTable',
            'TestTable'[Value]
        )
        return result
        formatString: $ #,##0
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "ComplexMeasure", "measure");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_Partition_ReturnsCorrectLineNumber()
        {
            var content = @"
table TestTable

    partition MyPartition = m
        mode: import
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "MyPartition", "partition");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_Relationship_ReturnsCorrectLineNumber()
        {
            var content = @"
relationship cdb6e6a9-c9d1-42b9-b9e0-484a1bc7e123
    fromColumn: Table1.Column1
    toColumn: Table2.Column2
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "cdb6e6a9-c9d1-42b9-b9e0-484a1bc7e123", "relationship");
                
                Assert.Equal(1, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_Expression_ReturnsCorrectLineNumber()
        {
            var content = @"
expression Server = ""localhost"" meta [IsParameterQuery=true]
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "Server", "expression");
                
                Assert.Equal(1, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_Culture_ReturnsCorrectLineNumber()
        {
            var content = @"
culture en-US
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "en-US", "culture");
                
                Assert.Equal(1, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_ItemNotFound_ReturnsNull()
        {
            var content = @"
table TestTable

    column ExistingColumn
        dataType: int64
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "NonExistentColumn", "column");
                
                Assert.Null(result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_NonExistentFile_ReturnsNull()
        {
            var result = LineNumberFinder.FindLineNumber("/non/existent/path.tmdl", "AnyColumn", "column");
            
            Assert.Null(result);
        }

        [Fact]
        public void FindLineNumber_WithWindowsLineEndings_WorksCorrectly()
        {
            var content = "table Test\r\n\r\n    column TestColumn\r\n        dataType: int64";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "TestColumn", "column");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void FindLineNumber_FirstMatch_ReturnsFirstOccurrence()
        {
            var content = @"
table TestTable

    column TestColumn
        dataType: int64

    measure TestColumn = SUM(1)
";
            var tempFile = CreateTempTmdlFile(content);
            
            try
            {
                var result = LineNumberFinder.FindLineNumber(tempFile, "TestColumn", "column");
                
                Assert.Equal(3, result);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private string CreateTempTmdlFile(string content)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tmdl");
            File.WriteAllText(tempFile, content.Trim());
            return tempFile;
        }
    }
}
