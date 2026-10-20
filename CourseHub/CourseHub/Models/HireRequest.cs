using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CourseHub.Models
{
    public class HireRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RequesterId { get; set; } // student

        [Required]
        public string TargetId { get; set; } // tutor

        public bool IsAccepted { get; set; } = false;

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        [ForeignKey("RequesterId")]
        public ApplicationUser Requester { get; set; }

        [ForeignKey("TargetId")]
        public ApplicationUser Target { get; set; }
    }
}
