using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBILET.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string CustomerNumber { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string? Phone { get; set; }
        public string Role { get; set; } = "Agent";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
