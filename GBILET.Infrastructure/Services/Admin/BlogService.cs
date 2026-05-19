using GBILET.Core.DTOs.Blog;
using GBILET.Core.Entities;
using GBILET.Core.Service.Admin;
using GBILET.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GBILET.Infrastructure.Services.Admin;

public class BlogService(GTicketDbContext db) : IBlogService
{
    public async Task<List<BlogPostDto>> GetAllAsync(bool publishedOnly = false, CancellationToken ct = default)
    {
        var query = db.BlogPosts.AsQueryable();
        if (publishedOnly)
            query = query.Where(p => p.IsPublished);

        var posts = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return posts.Select(MapToDto).ToList();
    }

    public async Task<BlogPostDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var post = await db.BlogPosts
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        return post is null ? null : MapToDto(post);
    }

    public async Task<BlogPostDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var post = await db.BlogPosts.FindAsync([id], ct);
        return post is null ? null : MapToDto(post);
    }

    public async Task<BlogPostDto> CreateAsync(CreateBlogPostRequest req, CancellationToken ct = default)
    {
        if (await db.BlogPosts.AnyAsync(p => p.Slug == req.Slug, ct))
            throw new InvalidOperationException("Bu slug zaten kullanımda.");

        var post = new BlogPost
        {
            Slug = req.Slug,
            TitleTr = req.TitleTr, TitleEn = req.TitleEn,
            SummaryTr = req.SummaryTr, SummaryEn = req.SummaryEn,
            ContentTr = req.ContentTr, ContentEn = req.ContentEn,
            ThumbUrl = req.ThumbUrl,
            TagTr = req.TagTr, TagEn = req.TagEn,
            Author = req.Author, ReadTime = req.ReadTime,
            MetaTitleTr = req.MetaTitleTr, MetaTitleEn = req.MetaTitleEn,
            MetaDescriptionTr = req.MetaDescriptionTr, MetaDescriptionEn = req.MetaDescriptionEn,
            KeywordsTr = req.KeywordsTr, KeywordsEn = req.KeywordsEn,
            IsPublished = req.IsPublished,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    public async Task<BlogPostDto> UpdateAsync(int id, UpdateBlogPostRequest req, CancellationToken ct = default)
    {
        var post = await db.BlogPosts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Blog yazısı bulunamadı.");

        if (post.Slug != req.Slug && await db.BlogPosts.AnyAsync(p => p.Slug == req.Slug, ct))
            throw new InvalidOperationException("Bu slug zaten kullanımda.");

        post.Slug = req.Slug;
        post.TitleTr = req.TitleTr; post.TitleEn = req.TitleEn;
        post.SummaryTr = req.SummaryTr; post.SummaryEn = req.SummaryEn;
        post.ContentTr = req.ContentTr; post.ContentEn = req.ContentEn;
        post.ThumbUrl = req.ThumbUrl;
        post.TagTr = req.TagTr; post.TagEn = req.TagEn;
        post.Author = req.Author; post.ReadTime = req.ReadTime;
        post.MetaTitleTr = req.MetaTitleTr; post.MetaTitleEn = req.MetaTitleEn;
        post.MetaDescriptionTr = req.MetaDescriptionTr; post.MetaDescriptionEn = req.MetaDescriptionEn;
        post.KeywordsTr = req.KeywordsTr; post.KeywordsEn = req.KeywordsEn;
        post.IsPublished = req.IsPublished;
        post.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return MapToDto(post);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var post = await db.BlogPosts.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Blog yazısı bulunamadı.");

        db.BlogPosts.Remove(post);
        await db.SaveChangesAsync(ct);
    }

    private static BlogPostDto MapToDto(BlogPost p) => new()
    {
        Id = p.Id, Slug = p.Slug,
        TitleTr = p.TitleTr, TitleEn = p.TitleEn,
        SummaryTr = p.SummaryTr, SummaryEn = p.SummaryEn,
        ContentTr = p.ContentTr, ContentEn = p.ContentEn,
        ThumbUrl = p.ThumbUrl,
        TagTr = p.TagTr, TagEn = p.TagEn,
        Author = p.Author, ReadTime = p.ReadTime,
        MetaTitleTr = p.MetaTitleTr, MetaTitleEn = p.MetaTitleEn,
        MetaDescriptionTr = p.MetaDescriptionTr, MetaDescriptionEn = p.MetaDescriptionEn,
        KeywordsTr = string.IsNullOrWhiteSpace(p.KeywordsTr)
            ? [] : [.. p.KeywordsTr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        KeywordsEn = string.IsNullOrWhiteSpace(p.KeywordsEn)
            ? [] : [.. p.KeywordsEn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        IsPublished = p.IsPublished,
        CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt
    };
}
