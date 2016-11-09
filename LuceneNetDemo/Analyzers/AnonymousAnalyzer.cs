using Lucene.Net.Analysis;
using System;
using System.IO;

namespace LuceneNetDemo.Analyzers
{
    public class AnonymousAnalyzer : Analyzer
    {
        private readonly Func<string, TextReader, TokenStreamComponents> createComponents;

        public AnonymousAnalyzer(Func<string, TextReader, TokenStreamComponents> createComponents)
        {
            this.createComponents = createComponents;
        }

        public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            return createComponents(fieldName, reader);
        }
    }
}
