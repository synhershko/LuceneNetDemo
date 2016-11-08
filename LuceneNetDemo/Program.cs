using System;
using System.IO;
using Lucene.Net.Store;

namespace LuceneNetDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var indexDirectory = FSDirectory.Open(new DirectoryInfo(@"c:\github-index")))
            using (var ghi = new GitHubIndex(indexDirectory))
            {
                Console.WriteLine("Welcome to the Lucene.NET Demo!");

                Console.ForegroundColor = ConsoleColor.White;
                ConsoleKeyInfo cki;
                do
                {
                    Console.WriteLine();
                    Console.WriteLine("Please select an option:");
                    Console.WriteLine("1. Search in index");
                    Console.WriteLine("2. Index a GitHub organization");
                    Console.WriteLine("Q. Quit");
                    cki = Console.ReadKey(false); // show the key as you read it
                    Console.WriteLine();
                    switch (cki.KeyChar.ToString().ToLowerInvariant())
                    {
                        case "1":
                            Console.WriteLine();
                            Console.WriteLine("Please type a search query: ");
                            var query = Console.ReadLine();
                            if (string.IsNullOrWhiteSpace(query))
                                break;
                           
                            int totalHits = 0;
                            var results = ghi.Search(query, out totalHits);
    
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Found {0} results, showing top {1}", totalHits, results.Count);
                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            foreach (var result in results)
                            {
                                Console.WriteLine("* [{0}] {1}", result.Name, result.Description);
                            }
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                        case "2":
                            Console.WriteLine("Please type an organization name: ");
                            var org = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(org))
                            {
                                ghi.IndexRepositories(org).Wait();
                            }   
                            break;
                            break;
                        case "q":
                            return;
                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Unrecognized option");
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                    }
                } while (cki.Key != ConsoleKey.Escape);
            }

            Console.ReadKey();
        }
    }
}
