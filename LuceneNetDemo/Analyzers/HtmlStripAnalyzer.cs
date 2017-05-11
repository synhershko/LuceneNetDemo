using Lucene.Net.Analysis;
using Lucene.Net.Analysis.CharFilters;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using System.IO;

namespace LuceneNetDemo.Analyzers
{
    class HtmlStripAnalyzer : StopwordAnalyzerBase
    {
        public HtmlStripAnalyzer(LuceneVersion matchVersion, CharArraySet stopWords)
            : base(matchVersion, stopWords)
        {
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            StandardTokenizer standardTokenizer = new StandardTokenizer(base.m_matchVersion, reader);
            TokenStream stream = new StandardFilter(base.m_matchVersion, standardTokenizer);
            stream = new LowerCaseFilter(base.m_matchVersion, stream);
            return new TokenStreamComponents(standardTokenizer, new StopFilter(base.m_matchVersion, stream, base.m_stopwords));
        }

        protected override TextReader InitReader(string fieldName, TextReader reader)
        {
            return base.InitReader(fieldName, new HTMLStripCharFilter(reader));
        }
    }
}
