using System.Collections.Generic;
using Vsky.Models;

namespace Vsky.Api.Models
{
    /// <summary>One page of a record's notes, newest first, as the chat-style notes thread reads them.</summary>
    public record NoteThreadModelV2
    {
        public List<NoteModel> Data { get; set; } = new List<NoteModel>();
        public bool HasMore { get; set; }
        public int Total { get; set; }
        public List<NoteTypeCountV2> CountsByType { get; set; } = new List<NoteTypeCountV2>();
    }
}
