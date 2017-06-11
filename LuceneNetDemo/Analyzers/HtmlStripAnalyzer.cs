using Lucene.Net.Analysis;
using Lucene.Net.Analysis.CharFilters;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;
using System.IO;

namespace LuceneNetDemo.Analyzers
{
    class HtmlStripAnalyzer : Analyzer
    {
        private readonly LuceneVersion matchVersion;

        public HtmlStripAnalyzer(LuceneVersion matchVersion)
        {
            this.matchVersion = matchVersion;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            StandardTokenizer standardTokenizer = new StandardTokenizer(matchVersion, reader);
            TokenStream stream = new StandardFilter(matchVersion, standardTokenizer);
            stream = new LowerCaseFilter(matchVersion, stream);
            stream = new StopFilter(matchVersion, stream, StopAnalyzer.ENGLISH_STOP_WORDS_SET);
            return new TokenStreamComponents(standardTokenizer, stream);
        }

        protected override TextReader InitReader(string fieldName, TextReader reader)
        {
            return base.InitReader(fieldName, new HTMLStripCharFilter(reader));
        }
    }
}
