namespace RentACar.Core.Entities;

public class PublicSiteSettings : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string HeaderLinksJson { get; set; } = "[]";
    public string HeroLinksJson { get; set; } = "[]";
    public string QuickLinksJson { get; set; } = "[]";
    public string SocialLinksJson { get; set; } = "[]";
    public string FooterBottomLinksJson { get; set; } = "[]";
    public string ContactPageChannelsJson { get; set; } = "[]";
    public string ContactPageOfficesJson { get; set; } = "[]";
    public string ContactPageWorkingHoursJson { get; set; } = "[]";
    public string ContactPageMapTitle { get; set; } = string.Empty;
    public string ContactPageMapEmbedUrl { get; set; } = string.Empty;
    public bool ContactPageMapIsVisible { get; set; } = true;
    public string PagesJson { get; set; } = "[]";
}
