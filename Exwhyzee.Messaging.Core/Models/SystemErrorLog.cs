using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Exwhyzee.Messaging.Core.Models
{
    [Table("SystemErrorLog")]
    public class SystemErrorLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string LogLevel { get; set; } = "Error"; // Error, Critical, Fatal, Warning

        [StringLength(300)]
        public string Source { get; set; }

        public string Message { get; set; }

        public string InnerErrorMessage { get; set; }

        public string StackTrace { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [StringLength(256)]
        public string UserName { get; set; }

        [StringLength(256)]
        public string UserEmail { get; set; }

        [StringLength(500)]
        public string RequestUrl { get; set; }

        [StringLength(20)]
        public string RequestMethod { get; set; }

        [StringLength(100)]
        public string ClientIp { get; set; }

        public string PayloadData { get; set; }

        public bool EmailAlertSent { get; set; }

        public string EmailAlertError { get; set; }

        public string DbLoggingError { get; set; }

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedDate { get; set; }

        [StringLength(256)]
        public string ResolvedBy { get; set; }

        public string ResolutionNotes { get; set; }
    }
}
