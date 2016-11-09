using Lucene.Net.Analysis;
using Lucene.Net.Analysis.CharFilters;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using LuceneNetDemo.Analyzers;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Directory = Lucene.Net.Store.Directory;

namespace LuceneNetDemo
{
    public class GitHubIndex : IDisposable
    {
        private readonly GitHubClient github;

        private readonly PerFieldAnalyzerWrapper analyzer;
        private readonly IndexWriter indexWriter;
        private readonly SearcherManager searcherManager;
        private readonly QueryParser queryParser;

        public class SearchResult
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Url { get; set; }
            public float Score { get; set; }
        }

        public GitHubIndex(Directory indexDirectory)
        {
            github = new GitHubClient(new ProductHeaderValue("LuceneNetDemo"))
            {
                Credentials = new Credentials("<your GitHub API key here>")
            };

            analyzer = new PerFieldAnalyzerWrapper(
                new AnonymousAnalyzer((fieldName, reader) =>
                {
                    var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
                    return analyzer.CreateComponents(fieldName, new HTMLStripCharFilter(reader));
                }),
                new Dictionary<string, Analyzer>
                {
                    {
                        "owner",
                        new AnonymousAnalyzer((fieldName, reader) =>
                            {
                                var source = new KeywordTokenizer(reader);
                                TokenStream result = new ASCIIFoldingFilter(source);
                                result = new LowerCaseFilter(LuceneVersion.LUCENE_48, result);
                                return new Analyzer.TokenStreamComponents(source, result);
                            }
                        )
                    },
                    {
                        "name",
                        new AnonymousAnalyzer((fieldName, reader) =>
                            {
                                var source = new StandardTokenizer(LuceneVersion.LUCENE_48, reader);
                                TokenStream result = new WordDelimiterFilter(LuceneVersion.LUCENE_48, source, 255, CharArraySet.EMPTY_SET);
                                result = new ASCIIFoldingFilter(result);
                                result = new LowerCaseFilter(LuceneVersion.LUCENE_48, result);
                                return new Analyzer.TokenStreamComponents(source, result);
                            }
                        )
                    }
                });

            queryParser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48,
                new[] { "name", "description", "readme" }, analyzer);


            indexWriter = new IndexWriter(indexDirectory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer));
            searcherManager = new SearcherManager(indexWriter, true);
        }

        #region Indexing
        public async Task IndexRepositories(string org)
        {
            Console.WriteLine("Reading repos...");

            var repos = await github.Repository.GetAllForOrg(org.ToLowerInvariant(), new ApiOptions {PageSize = 100,});

            Console.ForegroundColor = ConsoleColor.DarkGreen;         
            foreach (var repo in repos)
            {
                Debug.Assert(repo.Url != null);
                Console.WriteLine("Indexing " + repo.Name + " " + repo.Url);

                string readmeHtml = "";
                try
                {
                    readmeHtml = await github.Repository.Content.GetReadmeHtml(repo.Id);
                }
                catch (Octokit.NotFoundException /*ignored*/)
                {
                }

                var doc = new Document
                {
                    new StringField("url", repo.Url, Field.Store.YES),
                    new TextField("name", repo.Name, Field.Store.YES),
                    new TextField("description", repo.Description ?? "", Field.Store.YES),
                    new TextField("readme", readmeHtml ?? "", Field.Store.NO),
                    new TextField("owner", repo.Owner?.Name ?? "", Field.Store.YES),
                };

                indexWriter.UpdateDocument(new Term("url", repo.Url), doc);
                
                // or ...
                //indexWriter.AddDocument(doc);   
            }
            Console.ForegroundColor = ConsoleColor.White;

            indexWriter.Flush(true, true);
            indexWriter.Commit();

            Console.WriteLine("Got {0} repos", repos.Count);
        }
        #endregion

        #region Search
        public List<SearchResult> Search(string queryString, out int totalHits)
        {
            var l = new List<SearchResult>();

            // Parse the query - assuming it's not a single term but an actual query string
            // Note the QueryParser used is using the same analyzer used for indexing
            var query = queryParser.Parse(queryString);

            var _totalHits = 0;

            // Execute the search with a fresh indexSearcher
            searcherManager.MaybeRefreshBlocking();
            searcherManager.ExecuteSearch(searcher =>
            {
                var topDocs = searcher.Search(query, 10);
                _totalHits = topDocs.TotalHits;
                foreach (var result in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(result.Doc);
                    l.Add(new SearchResult
                    {
                        Name = doc.GetField("name")?.StringValue,
                        Description = doc.GetField("description")?.StringValue,
                        Url = doc.GetField("url")?.StringValue,

                        // Results are automatically sorted by relevance
                        Score = result.Score,
                    });
                }
            }, exception => { Console.WriteLine(exception.ToString()); });

            totalHits = _totalHits;
            return l;
        }
        #endregion

        public void Dispose()
        {
            indexWriter?.Dispose();
            searcherManager?.Dispose();
        }
    }
}
