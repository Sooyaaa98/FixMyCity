namespace FixMyCity.DAL.Models;

public partial class Role
{
    public Role()
    {
        Users = new HashSet<User>();
    }

    public byte RoleId { get; set; }
    public string RoleName { get; set; }   // 'SuperAdmin' | 'Citizen' | 'Solver' | 'PWG'

    // Navigation
    public ICollection<User> Users { get; set; }
}
