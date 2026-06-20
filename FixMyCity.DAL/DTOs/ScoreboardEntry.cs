namespace FixMyCity.DAL.DTOs;

public class ScoreboardEntry
{
    public int UserId { get; set; }
    public string FullName { get; set; }
    public string LocalityName { get; set; }
    public int Points { get; set; }
    public int Rank { get; set; }
    public DateTime SnapshotAt { get; set; }
}