namespace ClaimSystem.Models
{
    // an enum is a set of named constants, works instead of using plain strings or numbers. this will clearly represnt what stage a claim is in
    public enum ClaimStatus { Draft, Submitted, PendingReview, Approved, Rejected }

    public class Claim
    {
        public int Id { get; set; }
        public string LecturerName { get; set; } = "";
        public string Month { get; set; } = "";
        public decimal HoursWorked { get; set; }
        public decimal HourlyRate { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentFileName { get; set; }
        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

        public decimal CalculatedAmount => HoursWorked * HourlyRate;

        public string? AttachmentStoredName { get; set; }

        public decimal CalculateTotalAmount()
        {
            return HoursWorked * HourlyRate;    
        }

    }
}
