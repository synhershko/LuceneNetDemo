using Lucene.Net.Analysis;
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
        /// <summary>
        /// The match version ensures you can upgrade Lucene.Net to a future version
        /// without having to upgrade the index at the same time. This is useful if
        /// you have many indexes and you want to migrate them one at a time
        /// after upgrading Lucene.Net.
        /// <para/>
        /// In this case, you should use a MatchVersion constant per index.
        /// MatchVersion would remain the same when Lucene.Net is upgraded,
        /// and would only need to be changed when the index format is 
        /// upgraded to the new version.
        /// </summary>
        private static readonly LuceneVersion MatchVersion = LuceneVersion.LUCENE_48;

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

        public GitHubIndex(Directory indexDirectory, string githubApiKey)
        {
            github = new GitHubClient(new ProductHeaderValue("LuceneNetDemo"))
            {
                Credentials = new Credentials(githubApiKey)
            };

            analyzer = new PerFieldAnalyzerWrapper(
                // Example of a pre-built custom analyzer
                defaultAnalyzer: new HtmlStripAnalyzer(GitHubIndex.MatchVersion),

                // Example of inline anonymous analyzers
                fieldAnalyzers: new Dictionary<string, Analyzer>
                {
                    // Field analyzer for owner
                    {
                        "owner",
                        Analyzer.NewAnonymous(createComponents: (fieldName, reader) =>
                        {
                            var source = new KeywordTokenizer(reader);
                            TokenStream result = new ASCIIFoldingFilter(source);
                            result = new LowerCaseFilter(GitHubIndex.MatchVersion, result);
                            return new TokenStreamComponents(source, result);
                        })
                    },
                    // Field analyzer for name
                    {
                        "name",
                        Analyzer.NewAnonymous(createComponents: (fieldName, reader) =>
                        {
                            var source = new StandardTokenizer(GitHubIndex.MatchVersion, reader);
                            TokenStream result = new WordDelimiterFilter(GitHubIndex.MatchVersion, source, ~WordDelimiterFlags.STEM_ENGLISH_POSSESSIVE, CharArraySet.EMPTY_SET);
                            result = new ASCIIFoldingFilter(result);
                            result = new LowerCaseFilter(GitHubIndex.MatchVersion, result);
                            return new TokenStreamComponents(source, result);
                        })
                    }
                });

            queryParser = new MultiFieldQueryParser(GitHubIndex.MatchVersion,
                new[] { "name", "description", "readme" }, analyzer);


            indexWriter = new IndexWriter(indexDirectory, new IndexWriterConfig(GitHubIndex.MatchVersion, analyzer));
            searcherManager = new SearcherManager(indexWriter, true, null);
        }

        #region Indexing
        public async Task IndexRepositories(string org)
        {
            Console.WriteLine("Reading repos...");

            var repos = await github.Repository.GetAllForOrg(org.ToLowerInvariant(), new ApiOptions { PageSize = 100, });

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

            var searcher = searcherManager.Acquire();
            try
            {
                var topDocs = searcher.Search(query, 10);
                _totalHits = topDocs.TotalHits;
                foreach (var result in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(result.Doc);
                    l.Add(new SearchResult
                    {
                        Name = doc.GetField("name")?.GetStringValue(),
                        Description = doc.GetField("description")?.GetStringValue(),
                        Url = doc.GetField("url")?.GetStringValue(),

                        // Results are automatically sorted by relevance
                        Score = result.Score,
                    });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                searcherManager.Release(searcher);
                searcher = null; // Don't use searcher after this point!
            }

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
