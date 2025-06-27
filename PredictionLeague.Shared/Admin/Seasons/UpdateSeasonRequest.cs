using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PredictionLeague.Shared.Admin.Seasons
{
    public class UpdateSeasonRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 4)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }
    }
}
