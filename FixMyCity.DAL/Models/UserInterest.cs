using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class UserInterest
{
    public int UserInterestId { get; set; }
    public int UserId { get; set; }
    public short? CategoryId { get; set; }
    public int? PreferredLocalityId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; }
    public IssueCategory Category { get; set; }
    public Locality PreferredLocality { get; set; }
}
