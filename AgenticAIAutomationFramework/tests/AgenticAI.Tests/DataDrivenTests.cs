using AgenticAI.Core.DataDriven;

namespace AgenticAI.Tests
{
    [TestFixture]
    public class DataDrivenTests
    {
        [Test]
        public void ParseCsv_ReturnsCorrectRowsAndColumns()
        {
            var csv = "col1,col2\nval1,val2\nval3,val4";
            var dataSet = DataSetReader.ParseCsv(csv);

            Assert.Multiple(() =>
            {
                Assert.That(dataSet.Columns.Count, Is.EqualTo(2));
                Assert.That(dataSet.Columns[0], Is.EqualTo("col1"));
                Assert.That(dataSet.Columns[1], Is.EqualTo("col2"));

                Assert.That(dataSet.RowCount, Is.EqualTo(2));
                Assert.That(dataSet.Rows[0]["col1"], Is.EqualTo("val1"));
                Assert.That(dataSet.Rows[0]["col2"], Is.EqualTo("val2"));
                Assert.That(dataSet.Rows[1]["col1"], Is.EqualTo("val3"));
                Assert.That(dataSet.Rows[1]["col2"], Is.EqualTo("val4"));
            });
        }

        [Test]
        public void ParseJson_ReturnsCorrectRowsAndColumns()
        {
            var json = @"[
                { ""name"": ""Alice"", ""age"": ""30"" },
                { ""name"": ""Bob"", ""age"": ""25"", ""city"": ""NY"" }
            ]";
            var dataSet = DataSetReader.ParseJson(json);

            Assert.Multiple(() =>
            {
                Assert.That(dataSet.Columns.Count, Is.EqualTo(3));
                Assert.That(dataSet.Columns.Contains("name"), Is.True);
                Assert.That(dataSet.Columns.Contains("age"), Is.True);
                Assert.That(dataSet.Columns.Contains("city"), Is.True);

                Assert.That(dataSet.RowCount, Is.EqualTo(2));
                Assert.That(dataSet.Rows[0]["name"], Is.EqualTo("Alice"));
                Assert.That(dataSet.Rows[0]["age"], Is.EqualTo("30"));
                Assert.That(dataSet.Rows[0]["city"], Is.Empty); // Missing key gets empty string

                Assert.That(dataSet.Rows[1]["name"], Is.EqualTo("Bob"));
                Assert.That(dataSet.Rows[1]["city"], Is.EqualTo("NY"));
            });
        }

        [Test]
        public void SubstitutePlaceholders_ReplacesAllValues()
        {
            var template = "Hello ${name}, your age is ${age}.";
            var row = new Dictionary<string, string>
            {
                { "name", "Alice" },
                { "age", "30" }
            };

            var result = DataSetReader.SubstitutePlaceholders(template, row);

            Assert.That(result, Is.EqualTo("Hello Alice, your age is 30."));
        }

        [Test]
        public void SubstitutePlaceholders_IgnoresMissingKeysAndCaseInsensitive()
        {
            var template = "Hello ${NAME}, your city is ${city}.";
            var row = new Dictionary<string, string>
            {
                { "name", "Bob" }
            };

            var result = DataSetReader.SubstitutePlaceholders(template, row);

            // Replaces ${NAME} with Bob, leaves ${city} as is since it's missing in row dict
            Assert.That(result, Is.EqualTo("Hello Bob, your city is ${city}."));
        }
    }
}
