using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyBackend.Models
{
  [Table("users", Schema = "public")]
  public class User
  {
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required, EmailAddress]
    [Column("email")]
    public required string Email { get; set; } // Add required

    [Required]
    [Column("passwordhash")]
    public required string PasswordHash { get; set; } // Add required

    [Required]
    [Column("role")]
    public required string Role { get; set; } // Add required

    [Column("registrationid")]
    public string? RegistrationId { get; set; }
  }
}