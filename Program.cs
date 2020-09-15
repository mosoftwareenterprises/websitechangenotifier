﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Abot2.Core;
using Abot2.Crawler;
using Abot2.Poco;
using Serilog;
using Serilog.Formatting.Json;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace websitechangenotifier
{
    class Program
    {
        private static Dictionary<Uri, string> allUrisFound = new Dictionary<Uri, string>();
        private static Dictionary<Uri, string> previouslyFoundUris = new Dictionary<Uri, string>();
        private static Dictionary<Uri, string> newPages = new Dictionary<Uri, string>();
        private static Dictionary<Uri, string> changedPages = new Dictionary<Uri, string>();

        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss:fff zzz}] [{ThreadId}] [{Level:u3}] - {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            ExternalDataManipulator dataManipulator = new ExternalDataManipulator();

            previouslyFoundUris = await dataManipulator.LoadPreviousResults();
            await CrawlPages();
            await dataManipulator.ExportData(@"ChangedUrisFound.txt", changedPages);
            await dataManipulator.ExportData(@"NewUrisFound.txt", newPages);
            await dataManipulator.ExportData(@"AllUrisFound.txt", allUrisFound);
        }

        private static async Task CrawlPages()
        {
            PoliteWebCrawler crawler = SetupCrawler();
            var crawlResult = await crawler.CrawlAsync(new Uri("https://www.kingsleighprimary.co.uk/parents/letters/"));
            var crawlResult2 = await crawler.CrawlAsync(new Uri("https://www.kingsleighprimary.co.uk/classes/reception/"));

        }

        private static PoliteWebCrawler SetupCrawler()
        {
            var config = new CrawlConfiguration
            {
                MaxPagesToCrawl = 0,
                MaxLinksPerPage = 0,
                MinCrawlDelayPerDomainMilliSeconds = 3000,
            };
            var crawler = new PoliteWebCrawler(config);

            crawler.PageCrawlCompleted += Crawler_PageCrawlCompleted;
            crawler.PageCrawlDisallowed += Crawler_PageCrawlDisallowed;
            return crawler;
        }

        private static void Crawler_PageCrawlDisallowed(object sender, PageCrawlDisallowedArgs e)
        {
            Log.Error($"Unable to parse {e.PageToCrawl.Uri.AbsoluteUri} because {e.DisallowedReason}");
        }

        private static void Crawler_PageCrawlCompleted(object sender, PageCrawlCompletedArgs e)
        {
            string pageContent = e.CrawledPage.AngleSharpHtmlDocument.Body.OuterHtml;
            string hash = ComputeCheckSum(pageContent);

            string retrievedValue = null;
            bool foundSomething = previouslyFoundUris.TryGetValue(e.CrawledPage.Uri, out retrievedValue);
            if (!foundSomething)
            {
                //Did not find it previously.
                //Alert about the new page
                newPages[e.CrawledPage.Uri] = pageContent;
            }
            else if (retrievedValue != hash)
            {
                //Found it previously, but the content has changed
                //Alert about content changed
                changedPages[e.CrawledPage.Uri] = pageContent;
            }

            allUrisFound[e.CrawledPage.Uri] = hash;
        }

        private static string ComputeCheckSum(string pageContent)
        {
            string hash;
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                hash = BitConverter.ToString(
                  md5.ComputeHash(Encoding.UTF8.GetBytes(pageContent))
                ).Replace("-", String.Empty);
            }

            return hash;
        }
    }
}
