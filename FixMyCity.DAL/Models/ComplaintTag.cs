using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// AI-generated or manually-added keyword tags per complaint. KeyBERT extracts
/// these from Title + Description. Source = 'AI' | 'Manual'.
/// Writes via <c>usp_SaveComplaintTags</c> (DELETE+INSERT, idempotent).
/// </summary>
public partial class ComplaintTag
{
    public int      TagId       { get; set; }
    public int      ComplaintId { get; set; }
    public string   Tag         { get; set; }
    public decimal? Score       { get; set; }
    public string   Source      { get; set; }   // 'AI' | 'Manual'

    public Complaint Complaint { get; set; }
}
