using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class ComplaintStatusTransition
{
    public byte TransitionId { get; set; }
    public string FromStatus { get; set; }
    public string ToStatus { get; set; }
    public string AllowedRoles { get; set; }
}
