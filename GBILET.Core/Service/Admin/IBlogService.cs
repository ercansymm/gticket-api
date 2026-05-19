using GBILET.Core.DTOs.Blog;

namespace GBILET.Core.Service.Admin;

public interface IBlogService
{
    Task<List<BlogPostDto>> GetAllAsync(bool publishedOnly = false, CancellationToken ct = default);
    Task<BlogPostDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<BlogPostDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<BlogPostDto> CreateAsync(CreateBlogPostRequest request, CancellationToken ct = default);
    Task<BlogPostDto> UpdateAsync(int id, UpdateBlogPostRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
