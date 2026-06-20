using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// One row per AI inference (categorization, duplicate flag, priority score,
/// resolution prediction, image classification, auto-tag, recommendation,
/// toxicity flag). Writes via <c>usp_SaveAIDecision</c>.
/// Supports explainability ("why did AI flag this?") and override audit.
/// </summary>
public partial class AIDecisionLog
{
    public int      LogId         { get; set; }
    public int?     ComplaintId   { get; set; }
    public int?     UserId        { get; set; }
    public string   DecisionType  { get; set; }
    public string   InputSummary  { get; set; }
    public string   OutputSummary { get; set; }
    public decimal? Confidence    { get; set; }
    public string   ModelVersion  { get; set; }
    public bool     WasOverridden { get; set; }
    public int?     OverriddenBy  { get; set; }
    public DateTime CreatedAt     { get; set; }

    public Complaint Complaint    { get; set; }
    public User      User         { get; set; }
    public User      Overrider    { get; set; }
}
