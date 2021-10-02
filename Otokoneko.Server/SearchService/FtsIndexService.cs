using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Cjk;
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
    public class FtsIndexServiceBase
    {
        protected static LuceneVersion AppLuceneVersion { get; } = LuceneVersion.LUCENE_48;
        protected static Analyzer Analyzer { get; } = new CJKAnalyzer(AppLuceneVersion);
        private string IndexPath { get; }
        public FtsIndexServiceBase(string indexPath)
        {
            IndexPath = indexPath;
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            new IndexWriter(ftsDirectory, new IndexWriterConfig(AppLuceneVersion, Analyzer)).Dispose();
        }

        public void Create(List<Document> docs)
        {
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.AddDocuments(docs, Analyzer);
            ftsIndexWriter.Commit();
        }
        public void Update(Term term, Document doc)
        {
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.UpdateDocument(term, doc, Analyzer);
            ftsIndexWriter.Commit();
        }
        public void Delete(Term term)
        {
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.DeleteDocuments(term);
            ftsIndexWriter.Commit();
        }
        public void Clear()
        {
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, Analyzer);
            using var ftsIndexWriter = new IndexWriter(ftsDirectory, indexConfig);
            ftsIndexWriter.DeleteAll();
            ftsIndexWriter.Commit();
        }
        public List<TResult> Search<TResult>(Query query, Func<ScoreDoc, IndexSearcher, TResult> selector)
        {
            using var ftsDirectory = FSDirectory.Open(IndexPath);
            using var ftsIndexReader = DirectoryReader.Open(ftsDirectory);
            var ftsIndexSearch = new IndexSearcher(ftsIndexReader);
            return ftsIndexSearch
                .Search(query, int.MaxValue).ScoreDocs
                .Select(hit => selector(hit, ftsIndexSearch))
                .ToList();
        }
    }

    public class MangaFtsIndexService: FtsIndexServiceBase
    {
        public MangaFtsIndexService(): base(@"./data/mangaIndex") { }

        public void Create(List<Manga> mangas)
        {
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
            Create(docs);
        }

        public void Update(Manga manga)
        {
            var term = new Term("Id", manga.ObjectId.ToString());
            var doc = new Document()
            {
                new StringField("Id", manga.ObjectId.ToString(), Field.Store.YES),
                new TextField("Title", manga.Title, Field.Store.NO),
                new TextField("Description", manga.Description ?? "", Field.Store.NO),
                new TextField("Aliases", manga.Aliases ?? "", Field.Store.NO)
            };
            Update(term, doc);
        }

        public void Delete(long mangaId)
        {
            var term = new Term("Id", mangaId.ToString());
            Delete(term);
        }

        public List<long> Search(string queryString)
        {
            queryString = QueryParserBase.Escape(queryString);
            var titleParser = new QueryParser(AppLuceneVersion, "Title", Analyzer);
            var descriptionParser = new QueryParser(AppLuceneVersion, "Description", Analyzer);
            var aliasesParser = new QueryParser(AppLuceneVersion, "Aliases", Analyzer);
            var query = new BooleanQuery
            {
                {new BoostedQuery(titleParser.Parse(queryString), new DoubleConstValueSource(2.0)), Occur.SHOULD},
                {new BoostedQuery(descriptionParser.Parse(queryString), new DoubleConstValueSource(0.5)), Occur.SHOULD},
                {new BoostedQuery(aliasesParser.Parse(queryString), new DoubleConstValueSource(1.0)), Occur.SHOULD}
            };
            return Search(query, (hit, ftsIndexSearch) => long.Parse(ftsIndexSearch.Doc(hit.Doc).Get("Id")));
        }
    }
    
    public class TagFtsIndexService : FtsIndexServiceBase
    {
        public TagFtsIndexService() : base(@"./data/tagIndex") { }

        public void Create(List<Tag> tags)
        {
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
            Create(docs);
        }

        public void Update(Tag tag)
        {
            var term = new Term("Id", tag.ObjectId.ToString());
            var doc = new Document()
            {
                new StringField("Id", tag.ObjectId.ToString(), Field.Store.YES),
                new TextField("Name", tag.Name, Field.Store.NO),
                new TextField("Detail", tag.Detail ?? "", Field.Store.NO),
            };
            Update(term, doc);
        }

        public void Delete(long tagId)
        {
            var term = new Term("Id", tagId.ToString());
            Delete(term);
        }
        public List<long> Search(string queryString)
        {
            queryString = QueryParserBase.Escape(queryString);
            var nameParser = new QueryParser(AppLuceneVersion, "Name", Analyzer);
            var detailParser = new QueryParser(AppLuceneVersion, "Detail", Analyzer);
            var query = new BooleanQuery
            {
                {nameParser.Parse(queryString + "*"), Occur.SHOULD},
                {detailParser.Parse(queryString), Occur.SHOULD},
            };
            return Search(query, (hit, ftsIndexSearch) => long.Parse(ftsIndexSearch.Doc(hit.Doc).Get("Id")));
        }
    }
}