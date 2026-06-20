using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// Retry queue for complaints submitted while the AI service was offline.
/// <see cref="FixMyCity.API.Services.AIPendingQueueProcessor"/> polls this
/// table every 5 minutes and re-fires the AI scoring pipeline.
/// </summary>
public partial class AIPendingScoreQueue
{
    public int       QueueId      { get; set; }
    public int       ComplaintId  { get; set; }
    public byte      AttemptCount { get; set; }
    public DateTime? LastAttempt  { get; set; }
    public DateTime  CreatedAt    { get; set; }
    public string    ErrorMessage { get; set; }

    public Complaint Complaint { get; set; }
}
