namespace AJut.UnitTests.Core
{
    using System;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ParserRulesTests
    {
        [TestMethod]
        public void IgnorePrefixes_CanParseFileWithComments_NoExceptionsThrown()
        {
            var daRules = new ParserRules();
            daRules.CommentIndicators.Add(new Tuple<string,string>( "#", "\n" ));

            string jsonText = ResourceFetcher.GetText("_TestData/WithComments.json");
            Json json = JsonHelper.ParseText(jsonText, daRules);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
        }

        [TestMethod]
        public void IgnorePrefixes_CanParseFileWithComments_PoundComment_ParsedProperly()
        {
            var daRules = new ParserRules();
            daRules.CommentIndicators.Add(new Tuple<string, string>("#", "\n"));

            string jsonText = ResourceFetcher.GetText("_TestData/WithComments.json");

            Json result = JsonHelper.ParseText(jsonText, daRules);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Data);
            Assert.IsFalse(result.HasErrors);
        }

        [TestMethod]
        public void IgnorePrefixes_CanParseFileWithComments_BlockComment_ParsedProperly()
        {
            var daRules = new ParserRules();
            daRules.CommentIndicators.Add(new Tuple<string, string>("/*", "*/"));

            string jsonText = ResourceFetcher.GetText("_TestData/WithBlockComments.json");

            Json result = JsonHelper.ParseText(jsonText, daRules);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Data);
            Assert.IsFalse(result.HasErrors);
        }
    }
}
