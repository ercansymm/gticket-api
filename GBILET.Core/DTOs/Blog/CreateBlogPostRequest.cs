namespace GBILET.Core.DTOs.Blog;

public class CreateBlogPostRequest
{
    public string Slug { get; set; } = string.Empty;
    public string TitleTr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string SummaryTr { get; set; } = string.Empty;
    public string SummaryEn { get; set; } = string.Empty;
    public string ContentTr { get; set; } = string.Empty;
    public string ContentEn { get; set; } = string.Empty;
    public string? ThumbUrl { get; set; }
    public string TagTr { get; set; } = string.Empty;
    public string TagEn { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int ReadTime { get; set; }
    public string MetaTitleTr { get; set; } = string.Empty;
    public string MetaTitleEn { get; set; } = string.Empty;
    public string MetaDescriptionTr { get; set; } = string.Empty;
    public string MetaDescriptionEn { get; set; } = string.Empty;
    public string KeywordsTr { get; set; } = string.Empty;
    public string KeywordsEn { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = false;
}
