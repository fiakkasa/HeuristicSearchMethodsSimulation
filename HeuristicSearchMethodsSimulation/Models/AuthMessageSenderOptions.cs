using SendGrid.Helpers.Mail;
using System.ComponentModel.DataAnnotations;

namespace HeuristicSearchMethodsSimulation.Models
{
    public record AuthMessageSenderOptions
    {
        [Required]
        public string SendGridKey { get; set; } = string.Empty;

        [Required]
        public EmailAddress? From { get; set; }
    }
}
