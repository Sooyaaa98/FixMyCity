namespace FixMyCity.API.Models
{
    using System.ComponentModel.DataAnnotations;

    public class SubmitComplaintRequest
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "CitizenUserId must be a valid user ID.")]
        public int CitizenUserId { get; set; }

        [Required]
        [Range(1, short.MaxValue, ErrorMessage = "CategoryId must be a valid category.")]
        public short CategoryId { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters.")]
        public string Title { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Description is required.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        public string Description { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "LocalityId must be a valid locality.")]
        public int LocalityId { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Address is required.")]
        [StringLength(500)]
        public string Address { get; set; }

        [Required]
        [RegularExpression("^(Low|Medium|High|Critical)$",
            ErrorMessage = "Criticality must be Low, Medium, High, or Critical.")]
        public string Criticality { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public int? FileSizeKB { get; set; }
    }

    // ── Unchanged classes below (kept for file completeness reference) ─────────
    // UpdateStatusRequest, UpdateEstDateRequest, RateComplaintRequest,
    // ReopenComplaintRequest, SearchComplaintsRequest, AddAttachmentRequest,
    // LinkDuplicateRequest — NO CHANGES TO THESE.

    public class UpdateStatusRequest
    {
        public int    ComplaintId          { get; set; }
        public int    SolverUserId         { get; set; }
        public string NewStatus            { get; set; }
        public string Remark               { get; set; }
        public string ResolutionFilePath   { get; set; }   // optional resolution photo
        public string ResolutionFileName   { get; set; }
        public int?   ResolutionFileSizeKB { get; set; }
    }

    public class UpdateEstDateRequest
    {
        public int      ComplaintId  { get; set; }
        public int      SolverUserId { get; set; }
        public DateTime EstDate      { get; set; }
    }

    public class RateComplaintRequest
    {
        public int    ComplaintId   { get; set; }
        public int    CitizenUserId { get; set; }
        public byte   Stars         { get; set; }   // 1–5
        public string Comment       { get; set; }
    }

    public class ReopenComplaintRequest
    {
        public int    ComplaintId   { get; set; }
        public int    CitizenUserId { get; set; }
        public string Reason        { get; set; }
    }

    public class SearchComplaintsRequest
    {
        public string  Keyword    { get; set; }
        public short?  CategoryId { get; set; }
        public int?    LocalityId { get; set; }
    }

    public class AddAttachmentRequest
    {
        public int    ComplaintId      { get; set; }
        public int    UploadedByUserId { get; set; }
        public string AttachmentType   { get; set; }   // Complaint / Resolution / PWGProgress / Evidence
        public string FilePath         { get; set; }
        public string FileName         { get; set; }
        public int?   FileSizeKB       { get; set; }
        public int?   TimelineId       { get; set; }
    }

    public class LinkDuplicateRequest
    {
        public int OriginalComplaintId { get; set; }
        public int LinkedComplaintId   { get; set; }
        public int LinkedByUserId      { get; set; }
    }
}
