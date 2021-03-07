using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries.Function;
using Lucene.Net.Queries.Function.ValueSources;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Otokoneko.DataType;

namespace Otokoneko.Server.SearchService
{
    public class FtsIndexService
    {
        private static LuceneVersion AppLuceneVersion { get; } = LuceneVersion.LUCENE_48;
        private static Analyzer Analyzer { get; } = new SimpleAnalyzer(AppLuceneVersion);
        private static string MangaIndexPath { get; } = @"./data/mangaIndex";
        private static string TagIndexPath { get; } = @"./data/tagIndex";

        public FtsIndexService()
        {
            using var mangaFtsDirectory = FSDirectory.Open(MangaIndexPath);
            using var tagFtsDirectory = FSDirectory.Open(TagIndexPath);
            new IndexWriter(mangaFtsDirectory, new IndexWriterConfig(AppLuceneVersion, Analyzer)).Dispose();
            new IndexWriter(tagFtsDirectory, new IndexWriterConfig(AppLuceneVersion, Analyzer)).Dispose();
        }

        public void CreateFtsIndex(List<Manga> mangas)
        {
            using var ftsDirectory = FSDirectory.Open(MangaIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            var docs = mangas.Select(it => new Document()
            {
                new StringField("Id",
                    it.ObjectId.ToString(),
                    Field.Store.YES),
                new TextField("Title",
                    it.Title,
                    Field.Store.NO),
                new TextField("Description",
                    it.Description??"",
                    Field.Store.NO),
                new TextField("Aliases",
                    it.Aliases??"",
                    Field.Store.NO),
            }).ToList();
            ftsIndexWriter.AddDocuments(docs, Analyzer);
            ftsIndexWriter.Commit();
        }

        public void CreateFtsIndex(List<Tag> tags)
        {
            using var ftsDirectory = FSDirectory.Open(TagIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            var docs = tags.Select(it => new Document()
            {
                new StringField("Id",
                    it.ObjectId.ToString(),
                    Field.Store.YES),
                new TextField("Name",
                    it.Name,
                    Field.Store.NO),
                new TextField("Detail",
                    it.Detail??"",
                    Field.Store.NO),
            }).ToList();
            ftsIndexWriter.AddDocuments(docs, Analyzer);
            ftsIndexWriter.Commit();
        }

        public void UpdateFtsIndex(Manga manga)
        {
            using var ftsDirectory = FSDirectory.Open(MangaIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.UpdateDocument(new Term("Id", manga.ObjectId.ToString()),
                new Document()
                {
                    new StringField("Id", manga.ObjectId.ToString(), Field.Store.YES),
                    new TextField("Title", manga.Title, Field.Store.NO),
                    new TextField("Description", manga.Description ?? "", Field.Store.NO),
                    new TextField("Aliases", manga.Aliases ?? "", Field.Store.NO)
                }, Analyzer);
            ftsIndexWriter.Commit();
        }

        public void UpdateFtsIndex(Tag tag)
        {
            using var ftsDirectory = FSDirectory.Open(TagIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.UpdateDocument(new Term("Id", tag.ObjectId.ToString()),
                new Document()
                {
                    new StringField("Id", tag.ObjectId.ToString(), Field.Store.YES),
                    new TextField("Name", tag.Name, Field.Store.NO),
                    new TextField("Detail", tag.Detail ?? "", Field.Store.NO),
                }, Analyzer);
            ftsIndexWriter.Commit();
        }

        public void DeleteMangaFtxIndex(long mangaId)
        {
            using var ftsDirectory = FSDirectory.Open(MangaIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.DeleteDocuments(new Term("Id", mangaId.ToString()));
            ftsIndexWriter.Commit();
        }

        public void DeleteTagFtxIndex(long tagId)
        {
            using var ftsDirectory = FSDirectory.Open(TagIndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.DeleteDocuments(new Term("Id", tagId.ToString()));
            ftsIndexWriter.Commit();
        }

        public List<long> MangaFts(string queryString)
        {
            queryString = QueryParserBase.Escape(queryString);
            using var ftsDirectory = FSDirectory.Open(MangaIndexPath);
            using var ftsIndexReader = DirectoryReader.Open(ftsDirectory);
            var ftsIndexSearch = new IndexSearcher(ftsIndexReader);
            var titleParser = new QueryParser(AppLuceneVersion, "Title", Analyzer);
            var descriptionParser = new QueryParser(AppLuceneVersion, "Description", Analyzer);
            var aliasesParser = new QueryParser(AppLuceneVersion, "Aliases", Analyzer);
            var query = new BooleanQuery
            {
                {new BoostedQuery(titleParser.Parse(queryString), new DoubleConstValueSource(2.0)), Occur.SHOULD},
                {new BoostedQuery(descriptionParser.Parse(queryString), new DoubleConstValueSource(0.5)), Occur.SHOULD},
                {new BoostedQuery(aliasesParser.Parse(queryString), new DoubleConstValueSource(1.0)), Occur.SHOULD}
            };
            var hits = ftsIndexSearch.Search(query, int.MaxValue).ScoreDocs.ToList();
            return hits.Select(hit => long.Parse(ftsIndexSearch.Doc(hit.Doc).Get("Id"))).ToList();
        }

        public List<long> TagFts(string queryString)
        {
            queryString = QueryParserBase.Escape(queryString);
            using var ftsDirectory = FSDirectory.Open(TagIndexPath);
            using var ftsIndexReader = DirectoryReader.Open(ftsDirectory);
            var ftsIndexSearch = new IndexSearcher(ftsIndexReader);
            var nameParser = new QueryParser(AppLuceneVersion, "Name", Analyzer);
            var detailParser = new QueryParser(AppLuceneVersion, "Detail", Analyzer);
            var query = new BooleanQuery
            {
                {nameParser.Parse(queryString + "*"), Occur.SHOULD},
                {detailParser.Parse(queryString), Occur.SHOULD},
            };
            var hits = ftsIndexSearch.Search(query, int.MaxValue).ScoreDocs.ToList();
            return hits.Select(hit => long.Parse(ftsIndexSearch.Doc(hit.Doc).Get("Id"))).Distinct().ToList();
        }
    }
}