using System.ComponentModel.DataAnnotations;

namespace ClaimSystem.Models.ViewModels
{
    public class ClaimFormViewModel
    {
        [Required, StringLength(120)]
        [Display(Name = "Lecturer Name")]
        public string LecturerName { get; set; } = "";

        [Required, StringLength(40)]
        [Display(Name = "Month")]
        public string Month { get; set; } = "";

        
        [Range(0.5, 9999.5, ErrorMessage = "Hours must be at least 0.5")]
        [Display(Name = "Hours Worked")]
        public decimal HoursWorked { get; set; }

   
        [Range(1, 1000000, ErrorMessage = "Rate must be at least 1")]
        [Display(Name = "Hourly Rate (R)")]
        public decimal HourlyRate { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes (optional)")]
        public string? Notes { get; set; }

      
        [DataType(DataType.Upload)]
        [Display(Name = "Upload Supporting Document")]
        public IFormFile? File { get; set; }

       
        public decimal CalculatedAmount => HoursWorked * HourlyRate;
    }
}
