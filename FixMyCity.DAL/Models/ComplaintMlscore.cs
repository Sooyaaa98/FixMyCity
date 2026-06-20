using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class ComplaintMlscore
{
    public int ScoreId { get; set; }
    public int ComplaintId { get; set; }
    public DateOnly? PredictedResolutionDate { get; set; }
    public decimal? ResolutionProbability { get; set; }
    public decimal? PriorityScore { get; set; }
    public string PredictionModelVersion { get; set; }
    public DateTime ScoredAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Complaint Complaint { get; set; }
}
