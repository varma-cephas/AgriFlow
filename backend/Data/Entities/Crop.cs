using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities
{
    [Table("crop")]
    public class Crop
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Name { get; set; } = string.Empty;

        public int MaturityDays { get; set; }
    }
}
