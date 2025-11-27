using System.ComponentModel.DataAnnotations;

namespace WebApplication10.Models
{

    public class LoginModel
    {
        [Required]
        public string EmpCode { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

}
