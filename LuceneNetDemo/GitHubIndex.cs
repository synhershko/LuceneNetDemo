using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;
using LuceneNetDemo.Analyzers;
using Octokit;
using Directory = Lucene.Net.Store.Directory;

namespace LuceneNetDemo
{
    public class GitHubIndex : IDisposable
    {
        private readonly GitHubClient github;

        private readonly PerFieldAnalyzerWrapper analyzer;
        private readonly IndexWriter indexWriter;
        private readonly SearcherManager searcherManager;

        public class SearchResult
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Url { get; set; }
        }

        public GitHubIndex(Directory indexDirectory, Credentials credentials)
        {
            github = new GitHubClient(new ProductHeaderValue("LuceneNetDemo"))
            {
                Credentials = credentials
            };

            analyzer = new PerFieldAnalyzerWrapper(new HtmlStripAnalyzerWrapper(new StandardAnalyzer(LuceneVersion.LUCENE_48)),
                new Dictionary<string, Analyzer>
                {
                    {"owner", new LowercaseKeywordAnalyzer()},
                    {"name", new RepositoryNamesAnalyzer()},
                });

            indexWriter = new IndexWriter(indexDirectory, new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer));
            searcherManager = new SearcherManager(indexWriter, true);
        }

        public List<SearchResult> Search(string queryString, out int totalHits)
        {
            searcherManager.MaybeRefreshBlocking();
            var l = new List<SearchResult>();

            var mfqp = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, new[] { "name","description", "readme"} , analyzer);
            var query = mfqp.Parse(queryString);

            var _totalHits = 0;
            searcherManager.ExecuteSearch(searcher =>
            {
                var topDocs = searcher.Search(query, 10);
                _totalHits = topDocs.TotalHits;
                foreach (var result in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(result.Doc);
                    l.Add(new SearchResult
                    {
                        Name = doc.GetField("name").StringValue,
                        Description = doc.GetField("description").StringValue,
                        Url = doc.GetField("url")?.StringValue,
                    });
                }
            }, exception => { Console.WriteLine(exception.ToString()); });

            totalHits = _totalHits;
            return l;
        }

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

            indexWriter.Commit();
            indexWriter.Flush(false, true);

            Console.WriteLine("Got {0} repos", repos.Count);
        }

        public void Dispose()
        {
            indexWriter?.Dispose();
            searcherManager?.Dispose();
        }
    }
}
