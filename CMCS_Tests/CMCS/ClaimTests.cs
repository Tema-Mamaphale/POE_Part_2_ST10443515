using ClaimSystem.Models;
using Xunit;

namespace CMCS
{
    public class ClaimTests
    {
        [Fact]
        public void CalculateTotalAmount()
        {
            //arrange phase 
            var claim = new Claim();

            claim.HoursWorked = 20; //will set hours to 20
            claim.HourlyRate = 670; // will set rate to

            // aCT phase
            var getResult = claim.CalculateTotalAmount();

            //assert phase 
            Assert.Equal(13400, getResult);

        }

        [Fact]
        public void AdditionalNotes_Simulation()
        {
            var claim = new Claim();

            claim.Notes = "Additional Notes submitted.";

            var notes = claim.Notes;

            Assert.Equal("Additional Notes submitted.", notes);
        }

        [Fact]

        public void FileProperties_IsStoredCorrectly()
        {
            var claim = new Claim();

            claim.AttachmentFileName = "invoice.pdf";
            claim.AttachmentStoredName = "workcontract.pdf";

            Assert.Equal("invoice.pdf", claim.AttachmentFileName);
            Assert.Equal("workcontract.pdf", claim.AttachmentStoredName);
        }

        [Fact]
        public void Status_ShouldDefaultToDraft()
        {

            var claim = new Claim();

            var status = claim.Status;

            Assert.Equal(ClaimStatus.Draft, status);
        }

    }
}
