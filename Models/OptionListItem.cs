using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CrashReport.Models
{

    [Table("lkp_option_list")]
    public class OptionListItem
    {

        [Key]
        [Column("option_id")]
        public int OptionId { get; set; }

        [Required, MaxLength(200)]
        [Column("list_name")]
        public string ListName { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        [Column("option_value")]
        public string OptionValue { get; set; } = string.Empty;

        [Column("display_order")]
        public int DisplayOrder { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}
