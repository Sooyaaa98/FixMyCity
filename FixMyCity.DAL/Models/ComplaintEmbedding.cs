using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// Sentence-transformer 384-float vector per complaint, stored as a JSON array
/// in NVARCHAR(MAX). Populated by the Python AI service via
/// <c>POST /api/ML/SaveEmbedding</c> (callback) and consumed by the duplicate
/// detection endpoint.
/// Writes use <c>usp_SaveComplaintEmbedding</c> (MERGE on ComplaintId). Reads
/// can use EF directly via this entity.
/// </summary>
public partial class ComplaintEmbedding
{
    public int      EmbeddingId   { get; set; }
    public int      ComplaintId   { get; set; }
    public string   EmbeddingJson { get; set; }
    public string   ModelVersion  { get; set; }
    public DateTime GeneratedAt   { get; set; }

    public Complaint Complaint { get; set; }
}
