using System.Collections.Generic;

/// <summary>
/// Dữ liệu của một mẫu nhạc (Band Template) dùng trong Studio marketplace.
/// </summary>
public class BandTemplateData
{
    public string       templateId    { get; set; }
    public string       name          { get; set; }
    public string       creatorId     { get; set; }
    public string       creatorName   { get; set; }
    public int          price         { get; set; }
    public List<string> buyerIds      { get; set; } = new List<string>();
    public int          downloadCount { get; set; }
}
