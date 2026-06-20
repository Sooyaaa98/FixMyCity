using System;
using System.Collections.Generic;

namespace FixMyCity.DAL.Models;

public partial class DepartmentCategory
{
    public int DeptCategoryId { get; set; }
    public int DeptId { get; set; }
    public short CategoryId { get; set; }
    public bool IsPrimary { get; set; }
    public byte Priority { get; set; }
    public DateTime CreatedAt { get; set; }

    public Department Department { get; set; }
    public IssueCategory Category { get; set; }
}
