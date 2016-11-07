using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace LuceneNetDemo.Analyzers
{
    class RepositoryNamesAnalyzer : Analyzer
    {
        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var source = new StandardTokenizer(LuceneVersion.LUCENE_48, reader);
            TokenStream result = new WordDelimiterFilter(LuceneVersion.LUCENE_48, source, 255, CharArraySet.EMPTY_SET);
            result = new ASCIIFoldingFilter(result);
            result = new LowerCaseFilter(LuceneVersion.LUCENE_48, result);
            return new TokenStreamComponents(source, result);
        }
    }
}
