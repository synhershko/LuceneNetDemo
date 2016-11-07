using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Util;

namespace LuceneNetDemo.Analyzers
{
    /// <summary>
    /// This analyzer does not tokenize the input, but does normalize it to be ascii lowercase
    /// </summary>
    class LowercaseKeywordAnalyzer : Analyzer
    {
        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var source = new KeywordTokenizer(reader);
            TokenStream result = new ASCIIFoldingFilter(source);
            result = new LowerCaseFilter(LuceneVersion.LUCENE_48, result);
            return new TokenStreamComponents(source, result);
        }
    }
}
