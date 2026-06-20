namespace FixMyCity.DAL.Models;

public partial class UserPoint
{
    public int PointsId { get; set; }
    public int UserId { get; set; }
    public int Points { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
}
